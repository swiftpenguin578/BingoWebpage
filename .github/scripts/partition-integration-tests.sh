#!/usr/bin/env bash

# Partitions the Integration test cases into duration-balanced CI shards.
#
# Weights come from a committed timing file (default: integration-test-timings.tsv
# next to this script; override with INTEGRATION_TIMINGS). Each row is
# "<unit><TAB><seconds>":
#   Class         seconds for the class (or for the rest of it, see below)
#   Class.Method  this method runs as its own unit, possibly on another shard;
#                 the Class row then weighs only the remaining methods
# Classes without a row weigh DEFAULT_SECONDS_PER_CASE per discovered case.
# Rows for classes or methods that no longer exist are ignored.
#
# Every discovered case must be selected by exactly one shard: each shard's
# filter is re-discovered and must select exactly its assigned cases, and the
# union of all shards must equal the full discovery.

set -Eeuo pipefail
export LC_ALL=C

usage() {
    echo "Usage: $0 <project> <configuration> <shard-count> [github-output-file]" >&2
    exit 64
}

fail() {
    echo "Integration test partition failed: $*" >&2
    exit 1
}

(( $# >= 3 && $# <= 4 )) || usage

project=$1
configuration=$2
shard_count=$3
output_file=${4:-${GITHUB_OUTPUT:-}}
timings_file=${INTEGRATION_TIMINGS:-"$(dirname "$0")/integration-test-timings.tsv"}
default_seconds_per_case=${DEFAULT_SECONDS_PER_CASE:-2}
namespace_prefix='Bingo.IntegrationTests.'

[[ -f "$project" ]] || fail "test project does not exist: $project"
[[ "$shard_count" =~ ^[1-9][0-9]*$ ]] || fail "shard count must be a positive integer"
[[ -f "$timings_file" ]] || fail "timing file does not exist: $timings_file"
[[ "$default_seconds_per_case" =~ ^[0-9]+([.][0-9]+)?$ ]] || fail "default seconds per case must be a non-negative number"
command -v dotnet >/dev/null 2>&1 || fail "dotnet is not available"
command -v jq >/dev/null 2>&1 || fail "jq is not available"

temporary_root=$(mktemp -d)
trap 'rm -rf -- "$temporary_root"' EXIT

extract_cases() {
    local raw_file=$1
    local cases_file=$2
    local label=$3

    awk -v label="$label" '
        BEGIN { count = 0; invalid = 0 }
        {
            sub(/\r$/, "", $0)
            if ($0 ~ /^    /) {
                line = $0
                sub(/^    /, "", line)
                if (line !~ /^Bingo[.]IntegrationTests[.]/) {
                    printf "%s: unexpected indented discovery line: %s\n", label, line > "/dev/stderr"
                    invalid = 1
                } else {
                    print line
                    count++
                }
            }
        }
        END {
            if (invalid || count == 0) {
                exit 1
            }
        }
    ' "$raw_file" > "$cases_file" || fail "$label did not produce a valid non-empty test list"
}

discover() {
    local label=$1
    local raw_file=$2
    local cases_file=$3
    local filter=${4:-}
    local -a command=(dotnet test "$project" --configuration "$configuration" --no-build --list-tests --logger "console;verbosity=quiet")

    if [[ -n "$filter" ]]; then
        command+=(--filter "$filter")
    fi

    if ! "${command[@]}" > "$raw_file" 2>&1; then
        fail "$label discovery command failed"
    fi
    extract_cases "$raw_file" "$cases_file" "$label"
}

all_raw="$temporary_root/all.raw"
all_cases="$temporary_root/all.cases"
discover "all" "$all_raw" "$all_cases"

# One row per discovered case: case ID, owning class, method (the display-name
# arguments of theory cases removed).
case_rows="$temporary_root/case-rows.tsv"
awk -v prefix="$namespace_prefix" '
    {
        case_id = $0
        if (case_id == "" || index(case_id, prefix) != 1) {
            printf "test case is outside the Integration namespace: %s\n", case_id > "/dev/stderr"
            exit 1
        }
        remainder = substr(case_id, length(prefix) + 1)
        dot = index(remainder, ".")
        if (dot == 0) {
            printf "cannot parse owning class from test case: %s\n", case_id > "/dev/stderr"
            exit 1
        }
        class_name = substr(remainder, 1, dot - 1)
        method = substr(remainder, dot + 1)
        sub(/[(].*$/, "", method)
        if (class_name !~ /^[A-Za-z_][A-Za-z0-9_]*$/ || method !~ /^[A-Za-z_][A-Za-z0-9_]*$/) {
            printf "unsupported class or method name in test case: %s\n", case_id > "/dev/stderr"
            exit 1
        }
        print case_id "\t" class_name "\t" method
    }
' "$all_cases" > "$case_rows" || fail "discovered cases could not be parsed"

total_cases=$(wc -l < "$all_cases" | tr -d ' ')
(( total_cases > 0 )) || fail "discovery returned no Integration cases"
class_total=$(cut -f2 "$case_rows" | sort -u | wc -l | tr -d ' ')

# Units: each method with its own timing row, plus the rest of every class.
# Columns: unit, kind (class|method), class, method, seconds, cases.
units="$temporary_root/units.tsv"
awk -F '\t' -v default_per_case="$default_seconds_per_case" -v timings_label="$timings_file" '
    FNR == NR {
        sub(/\r$/, "", $0)
        if ($0 ~ /^[[:space:]]*(#|$)/) next
        if (NF != 2 || $1 !~ /^[A-Za-z_][A-Za-z0-9_]*([.][A-Za-z_][A-Za-z0-9_]*)?$/ || $2 !~ /^[0-9]+([.][0-9]+)?$/) {
            printf "%s:%d: malformed timing row: %s\n", timings_label, FNR, $0 > "/dev/stderr"
            bad = 1
            exit 1
        }
        if ($1 in weight) {
            printf "%s:%d: duplicate timing row: %s\n", timings_label, FNR, $1 > "/dev/stderr"
            bad = 1
            exit 1
        }
        weight[$1] = $2 + 0
        next
    }
    {
        class_cases[$2]++
        key = $2 "." $3
        if (key in weight) method_cases[key]++
    }
    END {
        if (bad) exit 1
        for (key in method_cases) {
            split(key, part, ".")
            isolated_cases[part[1]] += method_cases[key]
            printf "%s\tmethod\t%s\t%s\t%s\t%d\n", key, part[1], part[2], weight[key], method_cases[key]
        }
        for (class_name in class_cases) {
            rest = class_cases[class_name] - isolated_cases[class_name]
            if (rest <= 0) continue
            seconds = (class_name in weight) ? weight[class_name] : rest * default_per_case
            printf "%s\tclass\t%s\t-\t%s\t%d\n", class_name, class_name, seconds, rest
        }
    }
' "$timings_file" "$case_rows" | sort -t $'\t' -k5,5gr -k1,1 > "$units" || fail "units could not be built from $timings_file"
unit_total=$(wc -l < "$units" | tr -d ' ')
(( unit_total >= shard_count )) || fail "only $unit_total units for $shard_count shards"

# Longest unit first, each to the shard with the fewest assigned seconds.
# Column 7 is the zero-based shard.
assignment="$temporary_root/assignment.tsv"
awk -F '\t' -v shards="$shard_count" '
    {
        target = 0
        for (shard = 1; shard < shards; shard++) {
            if (load[shard] < load[target]) target = shard
        }
        load[target] += $5
        print $0 "\t" target
    }
' "$units" > "$assignment"

build_filter() {
    local shard=$1
    awk -F '\t' -v shard="$shard" -v prefix="$namespace_prefix" '
        FNR == NR {
            if ($2 == "method") excluded[$3] = excluded[$3] "&FullyQualifiedName!=" prefix $1
            next
        }
        $7 != shard { next }
        {
            if ($2 == "method") {
                term = "FullyQualifiedName=" prefix $1
            } else if ($3 in excluded) {
                term = "(FullyQualifiedName~" prefix $3 "." excluded[$3] ")"
            } else {
                term = "FullyQualifiedName~" prefix $3 "."
            }
            filter = filter (filter == "" ? "" : "|") term
        }
        END { printf "%s", filter }
    ' "$assignment" "$assignment"
}

# The cases each shard must select, derived from the assignment.
awk -F '\t' -v dir="$temporary_root" '
    FNR == NR {
        if ($2 == "method") method_shard[$1] = $7
        else class_shard[$3] = $7
        next
    }
    {
        key = $2 "." $3
        shard = (key in method_shard) ? method_shard[key] : class_shard[$2]
        if (shard == "") {
            printf "case has no assigned shard: %s\n", $1 > "/dev/stderr"
            exit 1
        }
        print $1 > (dir "/expected-" shard ".cases")
    }
' "$assignment" "$case_rows" || fail "partition omitted a discovered case"

matrix_items='[]'
selected_all="$temporary_root/selected-all.cases"
: > "$selected_all"
plan="$temporary_root/plan.txt"
: > "$plan"

for (( shard = 0; shard < shard_count; shard++ )); do
    expected="$temporary_root/expected-$shard.cases"
    [[ -s "$expected" ]] || fail "shard $((shard + 1)) has no assigned cases"
    filter=$(build_filter "$shard")
    [[ -n "$filter" ]] || fail "empty filter for shard $((shard + 1))"

    shard_raw="$temporary_root/shard-$shard.raw"
    shard_cases="$temporary_root/shard-$shard.cases"
    discover "shard-$((shard + 1))" "$shard_raw" "$shard_cases" "$filter"
    cmp -s <(sort "$expected") <(sort "$shard_cases") || fail "shard $((shard + 1)) selected an omitted, extra or overlapping case"
    cat "$shard_cases" >> "$selected_all"

    expected_count=$(wc -l < "$expected" | tr -d ' ')
    seconds=$(awk -F '\t' -v shard="$shard" '$7 == shard { total += $5 } END { printf "%d", total + 0.5 }' "$assignment")
    unit_count=$(awk -F '\t' -v shard="$shard" '$7 == shard { count++ } END { print count + 0 }' "$assignment")
    printf 'Shard %d: %d cases, %d units, ~%ds\n' "$((shard + 1))" "$expected_count" "$unit_count" "$seconds" >> "$plan"
    awk -F '\t' -v shard="$shard" '$7 == shard { printf "    %5.0fs %4d cases  %s\n", $5, $6, $1 }' "$assignment" >> "$plan"

    matrix_items=$(jq -c \
        --arg shard "$((shard + 1))" \
        --arg filter "$filter" \
        --argjson expected "$expected_count" \
        --argjson seconds "$seconds" \
        '. + [{shard: ($shard | tonumber), filter: $filter, expected: $expected, seconds: $seconds}]' <<< "$matrix_items")
done

cmp -s <(sort "$all_cases") <(sort "$selected_all") || fail "partition omitted or duplicated cases"

matrix=$(jq -cn --argjson include "$matrix_items" '{include: $include}')
printf 'Discovered %d Integration cases across %d classes into %d units and %d non-empty shards (timings: %s).\n' \
    "$total_cases" "$class_total" "$unit_total" "$shard_count" "$timings_file" >&2
cat "$plan" >&2

if [[ -n "$output_file" ]]; then
    printf 'matrix=%s\n' "$matrix" >> "$output_file"
    printf 'total_cases=%d\n' "$total_cases" >> "$output_file"
    printf 'class_count=%d\n' "$class_total" >> "$output_file"
else
    printf '%s\n' "$matrix"
fi
