#!/usr/bin/env bash

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

[[ -f "$project" ]] || fail "test project does not exist: $project"
[[ "$shard_count" =~ ^[1-9][0-9]*$ ]] || fail "shard count must be a positive integer"
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

case_classes="$temporary_root/case-classes.tsv"
: > "$case_classes"
while IFS= read -r case_id; do
    [[ -n "$case_id" ]] || fail "empty test case ID"
    prefix='Bingo.IntegrationTests.'
    [[ "$case_id" == "$prefix"* ]] || fail "test case is outside the Integration namespace: $case_id"
    remainder=${case_id#$prefix}
    [[ "$remainder" == *.* ]] || fail "cannot parse owning class from test case: $case_id"
    class_name=${remainder%%.*}
    [[ "$class_name" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || fail "unsupported owning class name in test case: $case_id"
    printf '%s\t%s\n' "$case_id" "$class_name" >> "$case_classes"
done < "$all_cases"

total_cases=$(wc -l < "$all_cases" | tr -d ' ')
(( total_cases > 0 )) || fail "discovery returned no Integration cases"

class_table="$temporary_root/classes.tsv"
awk -F '\t' '{ counts[$2]++ } END { for (class_name in counts) print class_name "\t" counts[class_name] }' "$case_classes" | sort -t $'\t' -k2,2nr -k1,1 > "$class_table"
class_total=$(wc -l < "$class_table" | tr -d ' ')
(( class_total > 0 )) || fail "discovery returned no Integration classes"

declare -a shard_case_counts=()
declare -a shard_class_lists=()
for (( shard = 0; shard < shard_count; shard++ )); do
    shard_case_counts[shard]=0
    shard_class_lists[shard]=''
done

class_to_shard="$temporary_root/class-to-shard.tsv"
: > "$class_to_shard"
while IFS=$'\t' read -r class_name class_case_count; do
    [[ -n "$class_name" && "$class_case_count" =~ ^[0-9]+$ ]] || fail "malformed class discovery row"
    target_shard=0
    for (( shard = 1; shard < shard_count; shard++ )); do
        if (( shard_case_counts[shard] < shard_case_counts[target_shard] )); then
            target_shard=$shard
        fi
    done
    shard_case_counts[target_shard]=$(( shard_case_counts[target_shard] + class_case_count ))
    shard_class_lists[target_shard]+="$class_name"$'\n'
    printf '%s\t%d\n' "$class_name" "$target_shard" >> "$class_to_shard"
done < "$class_table"

for (( shard = 0; shard < shard_count; shard++ )); do
    [[ -n "${shard_class_lists[shard]}" ]] || fail "shard $((shard + 1)) has no assigned classes"
done

build_filter() {
    local class_list=$1
    local filter=''
    local class_name

    while IFS= read -r class_name; do
        [[ -n "$class_name" ]] || continue
        term="FullyQualifiedName~Bingo.IntegrationTests.${class_name}."
        if [[ -n "$filter" ]]; then
            filter+='|'
        fi
        filter+="$term"
    done <<< "$class_list"

    [[ -n "$filter" ]] || fail "empty shard filter"
    printf '%s' "$filter"
}

matrix_items='[]'
selected_class_rows="$temporary_root/selected-class-rows.tsv"
: > "$selected_class_rows"
selected_total=0

for (( shard = 0; shard < shard_count; shard++ )); do
    filter=$(build_filter "${shard_class_lists[shard]}")
    shard_raw="$temporary_root/shard-$shard.raw"
    shard_cases="$temporary_root/shard-$shard.cases"
    discover "shard-$((shard + 1))" "$shard_raw" "$shard_cases" "$filter"

    if ! awk -v expected_shard="$shard" '
        NR == FNR {
            class_shard[$1] = $2
            next
        }
        {
            case_id = $0
            sub(/^Bingo[.]IntegrationTests[.]/, "", case_id)
            if (case_id == $0 || case_id !~ /[.]/) {
                exit 1
            }
            class_name = case_id
            sub(/[.].*$/, "", class_name)
            if (!(class_name in class_shard)) {
                exit 1
            }
            if (class_shard[class_name] != expected_shard) {
                exit 2
            }
        }
    ' "$class_to_shard" "$shard_cases"; then
        fail "shard $((shard + 1)) selected an omitted or overlapping case"
    fi

    selected_count=$(wc -l < "$shard_cases" | tr -d ' ')
    (( selected_count == shard_case_counts[shard] )) || fail "shard $((shard + 1)) expected ${shard_case_counts[shard]} cases but selected $selected_count"
    selected_total=$(( selected_total + selected_count ))
    awk '
        {
            case_id = $0
            sub(/^Bingo[.]IntegrationTests[.]/, "", case_id)
            class_name = case_id
            sub(/[.].*$/, "", class_name)
            counts[class_name]++
        }
        END {
            for (class_name in counts) {
                print class_name "\t" counts[class_name]
            }
        }
    ' "$shard_cases" >> "$selected_class_rows"

    matrix_items=$(jq -c \
        --arg shard "$((shard + 1))" \
        --arg filter "$filter" \
        --argjson expected "${shard_case_counts[shard]}" \
        '. + [{shard: ($shard | tonumber), filter: $filter, expected: $expected}]' <<< "$matrix_items")
done

(( selected_total == total_cases )) || fail "partition selected $selected_total of $total_cases discovered cases"
awk -F '\t' '{ counts[$1] += $2 } END { for (class_name in counts) print class_name "\t" counts[class_name] }' "$selected_class_rows" | sort -t $'\t' -k1,1 > "$temporary_root/selected-classes.tsv"
sort -t $'\t' -k1,1 "$class_table" > "$temporary_root/discovered-classes.tsv"
cmp -s "$temporary_root/selected-classes.tsv" "$temporary_root/discovered-classes.tsv" || fail "partition omitted or duplicated cases by class"

matrix=$(jq -cn --argjson include "$matrix_items" '{include: $include}')
printf 'Discovered %d Integration cases across %d classes into %d non-empty shards.\n' "$total_cases" "$class_total" "$shard_count" >&2
for (( shard = 0; shard < shard_count; shard++ )); do
    printf 'Shard %d: %d cases\n' "$((shard + 1))" "${shard_case_counts[shard]}" >&2
done

if [[ -n "$output_file" ]]; then
    printf 'matrix=%s\n' "$matrix" >> "$output_file"
    printf 'total_cases=%d\n' "$total_cases" >> "$output_file"
    printf 'class_count=%d\n' "$class_total" >> "$output_file"
else
    printf '%s\n' "$matrix"
fi
