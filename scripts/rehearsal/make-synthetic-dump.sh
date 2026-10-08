#!/usr/bin/env bash
# Builds a SYNTHETIC production-like dump for dry-running local-rehearsal.sh.
# No real data: an empty database is brought to the state a real host has after
# main's deploy path (--migrate, catalogue snapshot, owner bootstrap), plus the
# historical import applied with fictional operator input, one version-1 Luck
# checkpoint and one drop-tile EHB override. (No G4-eligible Setup draft is seeded.)
#
# Usage: make-synthetic-dump.sh --image <main-state web image> --work <empty dir> --out <file.dump>
# Output: <file.dump> (pg_dump -Fc) and <file.dump>.migration-history.txt
set -Eeuo pipefail
umask 077

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)
COMPOSE_FILE="$SCRIPT_DIR/compose.rehearsal.yml"
PROJECT=bingo-rehearsal-synth
POSTGRES_IMAGE=postgres:17-alpine@sha256:742f40ea20b9ff2ff31db5458d127452988a2164df9e17441e191f3b72252193
OWNER=rehearsal-owner
EVENT_TITLE="Det Store Danske Sommerbingo 2026"

image='' work='' out=''
while (($#)); do
    case "$1" in
        --image) image=${2:-}; shift 2 ;;
        --work) work=${2:-}; shift 2 ;;
        --out) out=${2:-}; shift 2 ;;
        *) echo "usage: $0 --image <tag> --work <dir> --out <file.dump>" >&2; exit 64 ;;
    esac
done
[[ -n "$image" && -n "$work" && -n "$out" ]] || { echo "missing arguments" >&2; exit 64; }
die() { echo "synthetic: $*" >&2; exit 1; }
mkdir -p "$work"; work=$(cd "$work" && pwd -P)
[[ -z "$(ls -A "$work")" ]] || die "--work must be empty"
[[ -z "$(docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT")" ]] || die "project $PROJECT already has volumes"
mkdir -p "$work/fixtures/s3" "$work/fixtures/ca" "$work/input" "$work/logs"
{
    echo "REHEARSAL_POSTGRES_IMAGE=$POSTGRES_IMAGE"
    echo "REHEARSAL_S3_IMAGE=unused"
    echo "REHEARSAL_AWSCLI_IMAGE=unused"
    echo "REHEARSAL_WEB_IMAGE=$image"
    echo "REHEARSAL_POSTGRES_PASSWORD=$(openssl rand -hex 24)"
    echo "REHEARSAL_S3_ACCESS_KEY=unused"
    echo "REHEARSAL_S3_SECRET_KEY=unused"
    echo "REHEARSAL_FIXTURE_DIR=$work/fixtures"
} >"$work/fixture.env"
compose() { docker compose --project-name "$PROJECT" --project-directory "$SCRIPT_DIR" --file "$COMPOSE_FILE" --env-file "$work/fixture.env" "$@"; }
psql_q() { compose exec -T postgres psql -X -A -t -q -v ON_ERROR_STOP=1 -U bingo_rehearsal -d bingo_rehearsal -c "$1"; }
cleanup() { compose --profile tools down --volumes --remove-orphans >"$work/logs/down.log" 2>&1 || true; }
trap cleanup EXIT
step() { local name=$1; shift; echo "== $name"; "$@" >"$work/logs/$name.log" 2>&1 || { echo "step $name failed; see $work/logs/$name.log" >&2; exit 1; }; }

step up compose up -d postgres
for i in $(seq 1 40); do
    [[ "$(docker inspect -f '{{.State.Health.Status}}' "$(compose ps -q postgres)")" == healthy ]] && break
    [[ "$i" == 40 ]] && die "postgres not healthy"; sleep 3
done
step web-init compose run --rm --no-deps web-init
# The same order main's bingo-deploy uses on a new host.
step migrate compose run --rm --no-deps web --migrate
step catalogue compose run --rm --no-deps web --apply-catalogue-snapshot
owner_password=$(openssl rand -hex 16)
step bootstrap-owner env Slice1__BootstrapOwnerPassword="$owner_password" \
    docker compose --project-name "$PROJECT" --project-directory "$SCRIPT_DIR" --file "$COMPOSE_FILE" --env-file "$work/fixture.env" \
    run --rm --no-deps -e Slice1__BootstrapOwnerPassword web --slice1-bootstrap-owner --username "$OWNER" --confirm-username "$OWNER"

# Fictional operator input (same shape as the committed integration test fixture).
python3 - "$work/input/historical-input.json" <<'PY'
import json, sys
teams = ["touch-kids-not-grass", "saeh-cs", "morytania-monkeys", "the-agency", "xen0-d-rops", "zalamalikum"]
def acct(u, s, e, g):
    return {"username": u, "startEhb": s, "endEhb": e, "gainedEhb": g,
            "fetchedAt": "2026-07-20T12:00:00Z", "upstreamUpdatedAt": "2026-07-20T12:00:00Z"}
participants = []
for t, slug in enumerate(teams):
    for m in range(15):
        n = t * 15 + m + 1
        accounts = [acct(f"Import Player {n:02d}", 100 + n, 101 + n, 1)]
        if t == 0 and m == 0: accounts = [acct("Ezzi", 200, 201, 1), acct("Also Ezzi", 20, 21, 1)]
        elif t == 0 and m == 1: accounts = [acct("wolles", 210, 211, 1), acct("w olles", 21, 22, 1)]
        elif t == 4 and m == 0: accounts = [acct("Xen Import Player", 220, 221, 1), acct("Coxophobia", 0, 0, 0)]
        participants.append({"participantKey": f"import-{n:02d}", "displayName": f"Import Player {n:02d}", "teamSlug": slug, "accounts": accounts})
json.dump({"sourceEventId": "dkl-sommerbingo-2026", "competitionId": 145197,
           "competitionTitle": "Det Store Danske Sommerbingo 2026",
           "startsAt": "2026-07-14T16:00:00Z", "endsAt": "2026-07-19T16:00:00Z",
           "accounts": participants}, open(sys.argv[1], "w"))
PY
chmod 644 "$work/input/historical-input.json"; chmod 755 "$work/input"
step historical-import compose run --rm --no-deps -v "$work/input:/rehearsal-input:ro" web \
    --apply-historical-import --historical-import-input /rehearsal-input/historical-input.json \
    --historical-import-actor "$OWNER" --confirm-historical-import "$EVENT_TITLE"

# Synthetic retained-data shapes the rehearsal gates look for.
event_id=$(psql_q "SELECT id FROM events WHERE slug = 'det-store-danske-sommerbingo-2026';")
[[ -n "$event_id" ]] || die "historical event missing after import"
step seed-sql psql_q "
BEGIN;
-- One version-1 Luck checkpoint with a minimal valid retained payload (PascalCase, numeric enums).
INSERT INTO event_stats_luck_checkpoints (event_id, schema_version, evidence_revision, competition_id, generation,
    activity_batch_id, assignment_fingerprint, source_fingerprint, lifecycle_fingerprint, calculated_at, fetched_at,
    upstream_updated_at, payload)
VALUES ('$event_id', 1, 0, 145197, 1, gen_random_uuid(), 'synthetic', 'synthetic', 'synthetic',
    TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-20 12:00:00+00',
    '{\"Result\":{\"Received\":0,\"Expected\":null,\"Percentage\":null,\"Status\":1,\"Estimated\":false,\"ZeroRecordedApproximation\":false},\"Teams\":[],\"Sources\":[],\"Stale\":false,\"CalculatedAt\":\"2026-07-20T12:00:00+00:00\",\"FetchedAt\":\"2026-07-20T12:00:00+00:00\",\"UpstreamUpdatedAt\":\"2026-07-20T12:00:00+00:00\",\"EvidenceRevision\":0,\"ActivityBatchId\":null,\"Generation\":1}'::jsonb);
-- One drop-tile EHB override (cleared by ClearLegacyDropTileEhbOverrides).
UPDATE tile_templates SET manual_ehb_override = 25.0000
WHERE id = (SELECT id FROM tile_templates WHERE objective_type = 'DropRequirements' ORDER BY id LIMIT 1);
COMMIT;"
psql_q "SELECT 'luck_v1=' || count(*) FROM event_stats_luck_checkpoints WHERE schema_version = 1;" >"$work/logs/seed-check.log"
psql_q "SELECT 'ehb_overrides=' || count(*) FROM tile_templates WHERE objective_type = 'DropRequirements' AND manual_ehb_override IS NOT NULL;" >>"$work/logs/seed-check.log"
cat "$work/logs/seed-check.log"

mkdir -p "$(dirname "$out")"
compose exec -T postgres pg_dump --format=custom --no-owner --no-privileges -U bingo_rehearsal -d bingo_rehearsal >"$out" 2>"$work/logs/pg_dump.log"
psql_q 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' >"$out.migration-history.txt"
echo "synthetic dump: $out ($(wc -c <"$out" | tr -d ' ') bytes, $(wc -l <"$out.migration-history.txt" | tr -d ' ') migrations)"
