#!/usr/bin/env bash
# Local R-3 rehearsal harness (option b, 8 October 2026).
#
# Restores a pg_dump custom-format dump into a disposable, egress-free Compose
# project and runs the exact candidate image through migrate, Luck conversion,
# production preflight and web/worker health, using fixtures only.
# Never touches production, /etc/bingo, production volumes or real providers.
#
# Usage:
#   local-rehearsal.sh --dump <file.dump> --candidate <40-hex SHA> --work <dir> \
#       [--history <migration-history.txt>] [--license-file <path>] [--keep-db [--keep-db-port N]]
#   local-rehearsal.sh --cleanup [--work <dir>]
#
# Bash 3.2 compatible (macOS /bin/bash).
set -Eeuo pipefail
umask 077

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)
REPO_ROOT=$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)
COMPOSE_FILE="$SCRIPT_DIR/compose.rehearsal.yml"
PROJECT=${REHEARSAL_PROJECT:-bingo-rehearsal}
KEEPDB_CONTAINER="${PROJECT}-keepdb"
MIGRATIONS_PATH=src/Bingo.Infrastructure/Persistence/Migrations
DB=bingo_rehearsal
DB_USER=bingo_rehearsal
PREMIGRATION_DB=bingo_rehearsal_premigration
HISTORICAL_SLUG=det-store-danske-sommerbingo-2026
HISTORICAL_ACTION=historical_import.applied

# Pinned fixture/tool images (digests recorded 8 October 2026).
POSTGRES_IMAGE=postgres:17-alpine@sha256:742f40ea20b9ff2ff31db5458d127452988a2164df9e17441e191f3b72252193
S3_IMAGE=versity/versitygw@sha256:30292fc2eeacc67a36993b01f7a7a5e3361a19cced0e80c1d71cfa2a4b0a2499
AWSCLI_IMAGE=amazon/aws-cli@sha256:48c3d4212e2f5b0e24bdc6af7708f9412ce65425a79575e0f78b8f8c0dcd70ab

dump=
candidate=
work=
history_file=
license_file="$REPO_ROOT/sixlabors.lic"
keep_db=false
keep_db_port=55433
cleanup=false

usage() { sed -n '2,16p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 64; }
while (($#)); do
    case "$1" in
        --dump) dump=${2:-}; shift 2 ;;
        --candidate) candidate=${2:-}; shift 2 ;;
        --work) work=${2:-}; shift 2 ;;
        --history) history_file=${2:-}; shift 2 ;;
        --license-file) license_file=${2:-}; shift 2 ;;
        --keep-db) keep_db=true; shift ;;
        --keep-db-port) keep_db_port=${2:-}; shift 2 ;;
        --cleanup) cleanup=true; shift ;;
        -h|--help) usage ;;
        *) echo "unknown argument: $1" >&2; usage ;;
    esac
done

die() { echo "rehearsal: $*" >&2; exit 1; }
sha256_of() { shasum -a 256 "$1" | awk '{print $1}'; }
utc_now() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# Redact anything that could identify participants or secrets before text leaves
# the private work directory (report, console). Aggregates are never affected.
redact() {
    perl -pe '
        s/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/[EMAIL]/g;
        s/\d{15,}/[LONG-ID]/g;
        s/\b\d{3}-\d{3}-\d{3}\b/[CODE]/g;
        s/((?:password|secret|token|access[_-]?key|api[_-]?key|client[_-]?secret)"?\s*[=:]\s*"?)[^\s";,]+/$1\[REDACTED\]/gi;
    '
}

compose() {
    docker compose --project-name "$PROJECT" --project-directory "$SCRIPT_DIR" \
        --file "$COMPOSE_FILE" --env-file "$work/private/fixture.env" "$@"
}

# shellcheck disable=SC2086 # id lists below are intentionally word-split
if [[ "$cleanup" == true ]]; then
    # Removes only this project's containers, network and volumes, plus the
    # optional keep-db container. Never touches the work directory or images.
    docker rm -f "$KEEPDB_CONTAINER" >/dev/null 2>&1 || true
    if [[ -n "$work" && -f "$work/private/fixture.env" ]]; then
        compose --profile tools down --volumes --remove-orphans
    else
        ids=$(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT")
        [[ -z "$ids" ]] || docker rm -f $ids >/dev/null
        vols=$(docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT")
        [[ -z "$vols" ]] || docker volume rm $vols >/dev/null
        nets=$(docker network ls -q --filter "label=com.docker.compose.project=$PROJECT")
        [[ -z "$nets" ]] || docker network rm $nets >/dev/null
    fi
    echo "rehearsal project $PROJECT removed (containers, network, volumes). Work directory left in place."
    exit 0
fi

# ---------------------------------------------------------------- input checks
[[ -n "$dump" && -n "$candidate" && -n "$work" ]] || usage
[[ "$candidate" =~ ^[0-9a-f]{40}$ ]] || die "--candidate must be a full lowercase 40-character SHA"
git -C "$REPO_ROOT" cat-file -e "${candidate}^{commit}" 2>/dev/null || die "candidate $candidate is not a commit in $REPO_ROOT (fetch it first)"
[[ -f "$dump" && -r "$dump" ]] || die "dump file is missing or unreadable"
dump=$(cd "$(dirname "$dump")" && pwd -P)/$(basename "$dump")
[[ -z "$history_file" || -f "$history_file" ]] || die "--history file is missing"
[[ -f "$license_file" ]] || die "license file is missing (expected the gitignored sixlabors.lic)"
[[ "$keep_db_port" =~ ^[0-9]+$ && "$keep_db_port" != 5310 && "$keep_db_port" != 5320 ]] || die "--keep-db-port must be numeric and not 5310/5320"
[[ "$work" == /* ]] || die "--work must be an absolute path"
case "$dump/" in "$REPO_ROOT"/*) die "the dump must live outside the repository" ;; esac
mkdir -p "$work"
work=$(cd "$work" && pwd -P)
case "$work/" in "$REPO_ROOT"/*) die "--work must be outside the repository" ;; esac
[[ -z "$(ls -A "$work")" ]] || die "--work must be empty (use a new directory per run)"
chmod 700 "$work"
mkdir -p "$work/private" "$work/logs" "$work/fixtures/s3" "$work/fixtures/ca"
for tool in docker jq openssl shasum git; do command -v "$tool" >/dev/null || die "missing command: $tool"; done
existing=$(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT"; docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT")
[[ -z "$existing" ]] || die "project $PROJECT already has containers or volumes; run '$0 --cleanup --work <previous work dir>' first (fresh volumes are required)"
docker ps -aq --filter "name=^${KEEPDB_CONTAINER}$" | grep -q . && die "container $KEEPDB_CONTAINER exists; run --cleanup first"

STAGES="$work/stages.tsv"
REPORT="$work/report.md"
FACTS="$work/facts.tsv"
printf 'stage\tstatus\texit\tstarted_utc\tfinished_utc\n' >"$STAGES"
: >"$FACTS"
current_stage=none
stage_started=
fact() { printf '%s\t%s\n' "$1" "$2" >>"$FACTS"; }
fact_get() { awk -F'\t' -v k="$1" '$1==k {v=$2} END {print v}' "$FACTS"; }
stage_begin() { current_stage=$1; stage_started=$(utc_now); echo "== stage $1"; }
stage_end() { printf '%s\tpassed\t0\t%s\t%s\n' "$current_stage" "$stage_started" "$(utc_now)" >>"$STAGES"; echo "   passed"; current_stage=none; }

# run <log-name> <command...>: output goes to a private log, never the console.
run() {
    local name=$1; shift
    local log="$work/logs/$name.log" status=0
    "$@" >"$log" 2>&1 || status=$?
    fact "exit.$name" "$status"
    if ((status != 0)); then
        echo "   command '$name' failed with exit $status; private log: $log" >&2
        return "$status"
    fi
}

psql_q() { compose exec -T postgres psql -X -A -t -q -v ON_ERROR_STOP=1 -U "$DB_USER" -d "${2:-$DB}" -c "$1"; }
table_exists() { [[ "$(psql_q "SELECT to_regclass('public.\"$1\"') IS NOT NULL;")" == t ]]; }
column_exists() { [[ "$(psql_q "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='$1' AND column_name='$2');")" == t ]]; }
count_or_na() { # <fact-key> <required-table> <required-column|-> <sql>
    local key=$1 table=$2 column=$3 sql=$4 value
    if ! table_exists "$table" || { [[ "$column" != - ]] && ! column_exists "$table" "$column"; }; then
        value="n/a (schema absent)"
    else
        value=$(psql_q "$sql")
    fi
    fact "$key" "$value"
}
history_to() { psql_q 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' >"$1"; }
historical_counts() { # <suffix>
    local events audits
    events=$(psql_q "SELECT count(*) FROM events WHERE slug = '$HISTORICAL_SLUG';")
    audits=$(psql_q "SELECT count(*) FROM audit_entries a JOIN events e ON e.id = a.event_id WHERE e.slug = '$HISTORICAL_SLUG' AND a.action = '$HISTORICAL_ACTION';")
    fact "historical.events.$1" "$events"
    fact "historical.audits.$1" "$audits"
}
event_state_counts() { psql_q "SELECT state || '=' || count(*) FROM events GROUP BY state ORDER BY state;" | paste -sd ' ' -; }

write_report() {
    local outcome=$1
    {
        echo "# Local rehearsal report"
        echo
        echo "- Outcome: **$outcome**"
        echo "- Generated: $(utc_now)"
        echo "- Candidate source SHA: \`$candidate\`"
        echo "- Candidate image: \`$(fact_get image.tag)\` id \`$(fact_get image.id)\` (OCI revision label \`$(fact_get image.revision)\`)"
        echo "- Dump SHA-256: \`$(fact_get dump.sha256)\` ($(fact_get dump.bytes) bytes)"
        echo "- Fixture images: postgres \`$POSTGRES_IMAGE\`, S3 \`$S3_IMAGE\`, aws-cli \`$AWSCLI_IMAGE\`"
        echo "- Compose project: \`$PROJECT\` (internal network, no published ports)"
        echo
        echo "## Stages"
        echo
        echo "| Stage | Status | Exit | Started (UTC) | Finished (UTC) |"
        echo "| --- | --- | --- | --- | --- |"
        tail -n +2 "$STAGES" | awk -F'\t' '{printf "| %s | %s | %s | %s | %s |\n", $1, $2, $3, $4, $5}'
        echo
        echo "## Recorded facts (aggregates only)"
        echo
        echo "| Key | Value |"
        echo "| --- | --- |"
        awk -F'\t' '{printf "| %s | %s |\n", $1, $2}' "$FACTS" | redact
        for f in history-before history-after candidate-migrations; do
            if [[ -s "$work/$f.txt" ]]; then
                echo
                echo "## $f ($(wc -l <"$work/$f.txt" | tr -d ' ') entries)"
                echo
                echo '```'
                cat "$work/$f.txt"
                echo '```'
            fi
        done
        echo
        echo "## Not covered (accepted limits)"
        echo
        echo "No VM isolation (Docker Desktop internal network only), no real R2/WOM/Discord, no host bingo-deploy wrapper,"
        echo "no public TLS/Caddy, no restic/GHCR, no production Data Protection keys, no G4 Down/re-Up clone check."
        echo
        echo "Private data (restored dump copy in Docker volumes, logs, G4 id sets) stays in \`$work/private\`, \`$work/logs\`"
        echo "and the \`${PROJECT}_*\` volumes until \`$0 --cleanup --work $work\` and deletion of the work directory."
    } >"$REPORT"
}

on_exit() {
    local status=$?
    trap - EXIT
    if [[ "$current_stage" != none ]]; then
        printf '%s\tfailed\t%s\t%s\t%s\n' "$current_stage" "$status" "$stage_started" "$(utc_now)" >>"$STAGES"
        echo "rehearsal FAILED at stage $current_stage (exit $status)" >&2
        compose --profile tools down --remove-orphans >"$work/logs/teardown-after-failure.log" 2>&1 || true
        write_report "FAILED at stage $current_stage" || true
        echo "report: $REPORT (volumes kept for inspection; --cleanup removes them)" >&2
    fi
    exit "$status"
}
trap on_exit EXIT

fact dump.sha256 "$(sha256_of "$dump")"
fact dump.bytes "$(wc -c <"$dump" | tr -d ' ')"
fact candidate "$candidate"
git -C "$REPO_ROOT" ls-tree --name-only "$candidate" "$MIGRATIONS_PATH/" | sed 's#.*/##' |
    grep -E '^[0-9]{14}_[A-Za-z0-9_]+\.cs$' | grep -v '\.Designer\.cs$' | sed 's/\.cs$//' | sort >"$work/candidate-migrations.txt"
fact candidate.migrations "$(wc -l <"$work/candidate-migrations.txt" | tr -d ' ')"

# ------------------------------------------------------------ 1. build image
stage_begin 1-build-candidate-image
image_tag="bingo-rehearsal-web:$candidate"
build() {
    git -C "$REPO_ROOT" archive --format=tar "$candidate" |
        DOCKER_BUILDKIT=1 docker build --progress=plain \
            --secret "id=sixlabors_license_key,src=$license_file" \
            --file src/Bingo.Web/Dockerfile \
            --label "org.opencontainers.image.revision=$candidate" \
            --tag "$image_tag" -
}
run 01-build build
fact image.tag "$image_tag"
fact image.id "$(docker image inspect --format '{{.Id}}' "$image_tag")"
fact image.revision "$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image_tag")"
[[ "$(fact_get image.revision)" == "$candidate" ]] || die "image revision label does not match the candidate"
stage_end

# ------------------------------------------------ 2. isolated project and fixtures
stage_begin 2-isolated-project
s3_access=$(openssl rand -hex 10)
s3_secret=$(openssl rand -hex 20)
{
    echo "REHEARSAL_POSTGRES_IMAGE=$POSTGRES_IMAGE"
    echo "REHEARSAL_S3_IMAGE=$S3_IMAGE"
    echo "REHEARSAL_AWSCLI_IMAGE=$AWSCLI_IMAGE"
    echo "REHEARSAL_WEB_IMAGE=$image_tag"
    echo "REHEARSAL_POSTGRES_PASSWORD=$(openssl rand -hex 24)"
    echo "REHEARSAL_S3_ACCESS_KEY=$s3_access"
    echo "REHEARSAL_S3_SECRET_KEY=$s3_secret"
    echo "REHEARSAL_FIXTURE_DIR=$work/fixtures"
} >"$work/private/fixture.env"
chmod 600 "$work/private/fixture.env"
# Per-run fixture CA and S3 server certificate (never trusted by the host).
make_certs() {
    local d="$work/fixtures"
    openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj "/CN=Bingo rehearsal fixture CA" \
        -keyout "$work/private/ca.key" -out "$d/ca/ca.crt" || return
    printf 'basicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nsubjectAltName=DNS:s3-fixture\n' >"$work/private/s3-ext.cnf"
    openssl req -newkey rsa:2048 -nodes -subj "/CN=s3-fixture" -keyout "$d/s3/s3-fixture.key" -out "$work/private/s3.csr" || return
    openssl x509 -req -in "$work/private/s3.csr" -CA "$d/ca/ca.crt" -CAkey "$work/private/ca.key" -CAcreateserial \
        -days 2 -extfile "$work/private/s3-ext.cnf" -out "$d/s3/s3-fixture.crt" || return
    printf '[default]\ns3 =\n    addressing_style = path\n' >"$d/ca/aws-config"
    chmod 755 "$d" "$d/ca" "$d/s3"; chmod 644 "$d/ca/ca.crt" "$d/ca/aws-config" "$d/s3/s3-fixture.crt" "$d/s3/s3-fixture.key"
}
run 02-certs make_certs
# Inspect the rendered configuration before anything starts.
compose --profile tools config --format json >"$work/private/compose-rendered.json" 2>"$work/logs/02-compose-config.log"
jq -e '
    (.networks | to_entries | all(.value.internal == true)) and
    ([.services[] | select(has("ports") and (.ports | length) > 0)] | length == 0) and
    ([.services[] | select(.network_mode != null)] | length == 0) and
    ([.services[] | (.volumes // [])[] | select(.type == "bind") | select(.read_only != true)] | length == 0) and
    ([.services[] | (.volumes // [])[] | select((.source // "") | test("docker.sock|/etc/bingo|bingo-production"))] | length == 0) and
    ([.services[] | (.env_file // [])[]] | length == 0)
' "$work/private/compose-rendered.json" >/dev/null || die "rendered Compose configuration failed the isolation checks"
fact isolation.compose "internal network only; no ports; no host network; read-only binds; no production paths/env files"
run 02-up compose up -d postgres s3-fixture wom-refusal
wait_healthy() { # <service> <tries>
    local cid status i
    cid=$(compose ps -q "$1")
    for ((i = 0; i < $2; i++)); do
        status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2>/dev/null || true)
        [[ "$status" == healthy ]] && return 0
        [[ "$status" == unhealthy || "$status" == exited ]] && break
        sleep 3
    done
    echo "$1 not healthy: $status" >&2
    return 1
}
wait_healthy postgres 40
# Egress-deny evidence: no default route, and a documentation-range address is unreachable.
egress_probe() {
    compose --profile tools run --rm --no-deps probe '
        if ip route | grep -q "^default"; then echo "default route present"; exit 1; fi
        echo "routes:"; ip route
        if wget -q -T 5 -O /dev/null http://198.51.100.1/ 2>/dev/null; then echo "external address reachable"; exit 1; fi
        echo "external address unreachable (expected)"'
}
run 02-egress-deny egress_probe
fact isolation.egress "no default route; 198.51.100.1 unreachable"
wom_probe() {
    # shellcheck disable=SC2016 # the script runs inside the probe container
    compose --profile tools run --rm --no-deps probe '
        out=$(wget -S -q -T 5 -O /dev/null http://wom-refusal:8080/v2/players/x 2>&1 || true)
        echo "$out" | grep -q "503" && echo "WOM refusal fixture answers 503 (expected)"'
}
run 02-wom-refusal wom_probe
fact fixture.wom "refusal fixture returns 503 for every request"
s3_setup() {
    local i
    for ((i = 0; i < 20; i++)); do
        compose --profile tools run --rm --no-deps s3-tool --endpoint-url https://s3-fixture:7070 s3api list-buckets >/dev/null 2>&1 && break
        sleep 2
    done
    compose --profile tools run --rm --no-deps s3-tool --endpoint-url https://s3-fixture:7070 s3api create-bucket --bucket bingo-rehearsal-evidence || return
    compose --profile tools run --rm --no-deps s3-tool --endpoint-url https://s3-fixture:7070 s3api head-bucket --bucket bingo-rehearsal-evidence
}
run 02-s3-fixture s3_setup
fact fixture.s3 "HTTPS (fixture CA, validation on), path-style, region auto; HeadBucket ok"
stage_end

# ----------------------------------------------------------------- 3. restore
stage_begin 3-restore-dump
restore() {
    compose exec -T postgres pg_restore --exit-on-error --no-owner --no-privileges -U "$DB_USER" -d "$DB" <"$dump"
}
run 03-restore restore
table_exists __EFMigrationsHistory || die "restored database has no __EFMigrationsHistory"
history_to "$work/history-before.txt"
fact history.before.count "$(wc -l <"$work/history-before.txt" | tr -d ' ')"
if [[ -n "$history_file" ]]; then
    if cmp -s <(grep -v '^$' "$history_file") "$work/history-before.txt"; then
        fact history.before.manifest-match "identical to supplied migration-history.txt"
    else
        die "restored migration history differs from the supplied backup manifest history"
    fi
else
    fact history.before.manifest-match "not checked (no --history supplied)"
fi
unknown=$(comm -23 "$work/history-before.txt" "$work/candidate-migrations.txt" | paste -sd ' ' -)
[[ -z "$unknown" ]] || die "restored history contains migrations the candidate does not know: $unknown"
comm -13 "$work/history-before.txt" "$work/candidate-migrations.txt" >"$work/pending-migrations.txt"
fact migrations.pending "$(wc -l <"$work/pending-migrations.txt" | tr -d ' ')"
stage_end

# -------------------------------------------------------------- 4. gate counts
stage_begin 4-gate-counts
check_time=$(utc_now)
fact gates.check_time_utc "$check_time"
au20=$(psql_q "SELECT count(*) FROM events WHERE state = 'AwaitingFinalReview';")
fact gate.au20.awaiting_final_review "$au20"
count_or_na gate.drop_tile_ehb_overrides.before tile_templates manual_ehb_override \
    "SELECT count(*) FROM tile_templates WHERE objective_type = 'DropRequirements' AND manual_ehb_override IS NOT NULL;"
count_or_na gate.luck_v1.before event_stats_luck_checkpoints schema_version \
    "SELECT count(*) FROM event_stats_luck_checkpoints WHERE schema_version = 1;"
count_or_na gate.completion_corrections team_completion_corrections - "SELECT count(*) FROM team_completion_corrections;"
if table_exists boards && table_exists draft_publication_cycles && table_exists draft_sessions && table_exists draft_publication_rosters; then
    fact gate.published_boards_without_active_roster "$(psql_q "SELECT count(*) FROM boards b WHERE b.state = 'Published' AND NOT EXISTS (SELECT 1 FROM draft_publication_cycles c JOIN draft_sessions d ON d.id = c.draft_session_id WHERE d.event_id = b.event_id AND d.state = 'Finalized' AND c.superseded_at IS NULL AND EXISTS (SELECT 1 FROM draft_publication_rosters r WHERE r.draft_publication_cycle_id = c.id));")"
else
    fact gate.published_boards_without_active_roster "n/a (schema absent)"
fi
count_or_na gate.future_effective_switches event_participant_character_swaps effective_at_utc \
    "SELECT count(*) FROM event_participant_character_swaps WHERE effective_at_utc > TIMESTAMPTZ '$check_time';"
count_or_na gate.g4_setup_with_first_pick.before draft_sessions first_pick_recorded_at \
    "SELECT count(*) FROM draft_sessions WHERE state = 'Setup' AND first_pick_recorded_at IS NOT NULL;"
if column_exists draft_sessions first_pick_recorded_at; then
    psql_q "SELECT id FROM draft_sessions WHERE state = 'Setup' AND first_pick_recorded_at IS NOT NULL ORDER BY id;" >"$work/private/g4-eligible-ids.txt"
fi
count_or_na gate.cat1_conditional_on_parent source_drops conditional_on_parent \
    "SELECT count(*) FROM source_drops WHERE conditional_on_parent;"
count_or_na gate.cat1_non_default_context source_drops assumed_participants \
    "SELECT count(*) FROM source_drops WHERE assumed_participants <> 1 OR probability_scope <> 'Participant';"
if grep -q '_RetireEventBanners$' "$work/history-before.txt"; then
    fact gate.banner_cleanup "RetireEventBanners already applied (history entry present)"
else
    count_or_na gate.banner_assets event_banner_assets - "SELECT count(*) FROM event_banner_assets;"
    count_or_na gate.banner_cleanups event_banner_cleanups - "SELECT count(*) FROM event_banner_cleanups;"
    count_or_na gate.banner_event_refs events banner_asset_id "SELECT count(*) FROM events WHERE banner_asset_id IS NOT NULL;"
fi
fact gate.active_super_admins "$(psql_q "SELECT count(*) FROM accounts WHERE active AND global_role = 'SuperAdmin';")"
fact events.by_state.before "$(event_state_counts)"
historical_counts before
[[ "$au20" == 0 ]] || die "AU20 gate failed: $au20 event(s) AwaitingFinalReview; stop for a planner/user decision"
if [[ "$(fact_get historical.events.before)" != 0 && "$(fact_get historical.audits.before)" == 0 ]]; then
    die "historical event $HISTORICAL_SLUG exists without its $HISTORICAL_ACTION audit record; stop and report"
fi
# Pre-migration copy for reruns (template clone; nothing else is connected yet).
run 04-premigration-clone psql_q "CREATE DATABASE $PREMIGRATION_DB TEMPLATE $DB;" postgres
fact premigration.clone "$PREMIGRATION_DB ($(psql_q "SELECT count(*) FROM \"__EFMigrationsHistory\";" "$PREMIGRATION_DB") history rows)"
stage_end

# ----------------------------------------------------------------- 5. migrate
stage_begin 5-migrate
run 05-web-init compose run --rm --no-deps web-init
run 05-migrate compose run --rm --no-deps web --migrate
history_to "$work/history-after.txt"
fact history.after.count "$(wc -l <"$work/history-after.txt" | tr -d ' ')"
cmp -s "$work/history-after.txt" "$work/candidate-migrations.txt" || die "post-migration history does not equal the candidate's migration set"
comm -23 "$work/history-before.txt" "$work/history-after.txt" | grep -q . && die "a previously applied migration disappeared from the history"
fact history.after.matches_candidate "yes (all $(wc -l <"$work/candidate-migrations.txt" | tr -d ' ') candidate migrations applied, none extra)"
count_or_na gate.drop_tile_ehb_overrides.after tile_templates manual_ehb_override \
    "SELECT count(*) FROM tile_templates WHERE objective_type = 'DropRequirements' AND manual_ehb_override IS NOT NULL;"
ehb_before=$(fact_get gate.drop_tile_ehb_overrides.before); ehb_after=$(fact_get gate.drop_tile_ehb_overrides.after)
if [[ "$ehb_before" =~ ^[0-9]+$ && "$ehb_after" =~ ^[0-9]+$ ]]; then
    # The migration's RAISE NOTICE is not surfaced by --migrate; derive the cleared count instead.
    fact migrate.ehb_overrides_cleared "$((ehb_before - ehb_after)) (before $ehb_before, after $ehb_after)"
    [[ "$ehb_after" == 0 ]] || die "drop-tile EHB overrides remain after migration: $ehb_after"
fi
if [[ -f "$work/private/g4-eligible-ids.txt" ]] && column_exists draft_sessions requires_fresh_order; then
    psql_q "SELECT id FROM draft_sessions WHERE requires_fresh_order ORDER BY id;" >"$work/private/g4-fresh-order-ids.txt"
    if cmp -s "$work/private/g4-eligible-ids.txt" "$work/private/g4-fresh-order-ids.txt"; then
        fact gate.g4_backfill "eligible set == requires_fresh_order set ($(wc -l <"$work/private/g4-eligible-ids.txt" | tr -d ' ') rows)"
    elif grep -q '_AllowCancelledDraftRestart$' "$work/history-before.txt"; then
        fact gate.g4_backfill "migration already applied in backup; eligible=$(wc -l <"$work/private/g4-eligible-ids.txt" | tr -d ' ') fresh_order=$(wc -l <"$work/private/g4-fresh-order-ids.txt" | tr -d ' ')"
    else
        die "G4 backfill mismatch: eligible set differs from requires_fresh_order set"
    fi
    fact gate.g4_ineligible_default_false "$(psql_q "SELECT count(*) FROM draft_sessions WHERE NOT requires_fresh_order;")"
fi
historical_counts after-migrate
for k in events audits; do
    [[ "$(fact_get historical.$k.before)" == "$(fact_get historical.$k.after-migrate)" ]] || die "historical import $k count changed during migration"
done
stage_end

# ------------------------------------------------------- 6. Luck conversion
stage_begin 6-convert-luck-checkpoints
run 06-convert compose run --rm --no-deps web --convert-luck-checkpoints
summary=$(grep -E '^Luck checkpoint conversion summary:' "$work/logs/06-convert.log" | tail -1)
fact luck.summary "$summary"
[[ "$summary" == *"Could not convert=0."* ]] || die "Luck conversion did not report Could not convert=0"
count_or_na gate.luck_v1.after event_stats_luck_checkpoints schema_version \
    "SELECT count(*) FROM event_stats_luck_checkpoints WHERE schema_version = 1;"
count_or_na gate.luck_converted_from_v1 event_stats_luck_checkpoints converted_from_schema_version \
    "SELECT count(*) FROM event_stats_luck_checkpoints WHERE converted_from_schema_version = 1;"
historical_counts after-convert
for k in events audits; do
    [[ "$(fact_get historical.$k.before)" == "$(fact_get historical.$k.after-convert)" ]] || die "historical import $k count changed during conversion"
done
stage_end

# -------------------------------------------------- 7. production preflight
stage_begin 7-production-preflight
run 07-preflight compose run --rm --no-deps web --production-preflight
grep -q 'Production preflight passed.' "$work/logs/07-preflight.log" || die "preflight did not report success"
fact preflight "Production preflight passed."
fact events.by_state.before-web "$(event_state_counts)"
stage_end

# ------------------------------------------------------- 8. web and workers
stage_begin 8-web-health
run 08-up-web compose up -d --no-deps web
wait_healthy web 40
fact web.container_health "healthy (Docker healthcheck = --health-probe)"
run 08-health-probe compose exec -T web dotnet Bingo.Web.dll --health-probe
fact web.health_probe_exit "0"
http_probe() {
    # shellcheck disable=SC2016 # the script runs inside the probe container
    compose --profile tools run --rm --no-deps probe '
        for p in /health/live /health/ready; do
            code=$(wget -S -q -T 10 -O /dev/null "http://web:8080$p" 2>&1 | awk "/HTTP\\//{c=\$2} END{print c}")
            echo "$p $code"; [ "$code" = 200 ] || exit 1
        done'
}
run 08-http-health http_probe
fact web.health_live "200"
fact web.health_ready "200 (includes storage HeadBucket and fresh lifecycle + competition-sync worker heartbeats, max age 90 s)"
sleep 20
run 08-http-health-again http_probe
fact web.health_ready_after_20s "200"
compose logs --no-color web 2>&1 | tail -n 400 | redact >"$work/logs/08-web-startup.redacted.log"
fact web.log_errors "$(grep -Eci '"LogLevel":"(Error|Critical)"|fail:|crit:' "$work/logs/08-web-startup.redacted.log" || true) error/critical lines in last 400 (redacted copy: logs/08-web-startup.redacted.log)"
fact events.by_state.after-web "$(event_state_counts)"
stage_end

# ------------------------------------------------------------- 9. stop/report
stage_begin 9-stop-and-report
run 09-down compose --profile tools down --remove-orphans
if [[ "$keep_db" == true ]]; then
    volume="${PROJECT}_postgres-data"
    run 09-keep-db docker run -d --name "$KEEPDB_CONTAINER" \
        --label "com.docker.compose.project=$PROJECT" \
        -p "127.0.0.1:${keep_db_port}:5432" -v "$volume:/var/lib/postgresql/data" "$POSTGRES_IMAGE"
    fact keep_db "postgres only, 127.0.0.1:$keep_db_port (container $KEEPDB_CONTAINER); web not running"
fi
stage_end
write_report PASSED
echo "rehearsal PASSED; report: $REPORT"
if [[ "$keep_db" == true ]]; then
    cat <<EOF

Rehearsal database kept on 127.0.0.1:$keep_db_port (no web container). To browse it with a local
Development app from this checkout (the password is in $work/private/fixture.env):

  export ASPNETCORE_ENVIRONMENT=Development
  export ConnectionStrings__Database="Host=127.0.0.1;Port=$keep_db_port;Database=$DB;Username=$DB_USER;Password=\$(sed -n 's/^REHEARSAL_POSTGRES_PASSWORD=//p' $work/private/fixture.env)"
  export WiseOldMan__DevelopmentFake__Enabled=false WiseOldMan__BaseUrl=http://127.0.0.1:1/
  export DiscordAuthentication__ClientId= DiscordAuthentication__ClientSecret=
  dotnet run --no-launch-profile --project src/Bingo.Web --urls http://127.0.0.1:5330

The WOM settings keep the app from calling Wise Old Man or writing fake sync data. Workers still
run and may change this disposable copy; never reuse it as evidence. Sign-in needs an account in
the restored data (see scripts/rehearsal/README.md). When done:
  $0 --cleanup --work $work
EOF
fi
