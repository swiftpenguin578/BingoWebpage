#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

OPS_CONFIG=/etc/bingo/production-ops.env
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/bingo-ops-validation.sh"
validate_root_owned_file "$OPS_CONFIG" || { echo "Operations configuration must be a regular root:root 0600 file." >&2; exit 1; }
# shellcheck disable=SC1090
source "$OPS_CONFIG"
BINGO_OPS_CONFIG=$OPS_CONFIG

: "${BINGO_ROOT:?BINGO_ROOT is required}"
: "${BINGO_COMPOSE_FILE:?BINGO_COMPOSE_FILE is required}"
: "${BINGO_CADDY_FILE:?BINGO_CADDY_FILE is required}"
: "${BINGO_ENV_FILE:?BINGO_ENV_FILE is required}"
: "${BINGO_RESTIC_ENV_FILE:?BINGO_RESTIC_ENV_FILE is required}"
: "${BINGO_RESTIC_PASSWORD_FILE:?BINGO_RESTIC_PASSWORD_FILE is required}"
: "${BINGO_GHCR_ENV_FILE:?BINGO_GHCR_ENV_FILE is required}"
: "${BINGO_BACKUP_ROOT:?BINGO_BACKUP_ROOT is required}"
: "${BINGO_LOG_DIR:?BINGO_LOG_DIR is required}"
: "${BINGO_IMAGE_NAME:?BINGO_IMAGE_NAME is required}"
: "${BINGO_DOMAIN:?BINGO_DOMAIN is required}"
: "${BINGO_BOOTSTRAP_OWNER_USERNAME:?BINGO_BOOTSTRAP_OWNER_USERNAME is required}"
: "${BINGO_DATA_PROTECTION_DIR:?BINGO_DATA_PROTECTION_DIR is required}"
: "${BINGO_RETAINED_LEGACY_PREFLIGHT:?BINGO_RETAINED_LEGACY_PREFLIGHT is required}"
: "${BINGO_POSTGRES_IMAGE:?BINGO_POSTGRES_IMAGE is required}"
: "${BINGO_KEEP_DAILY:?BINGO_KEEP_DAILY is required}"
: "${BINGO_KEEP_WEEKLY:?BINGO_KEEP_WEEKLY is required}"
: "${BINGO_KEEP_MONTHLY:?BINGO_KEEP_MONTHLY is required}"

LOCK_FILE=${BINGO_LOCK_FILE:-/run/lock/bingo-production.lock}
DEPLOYMENT_ROOT=${BINGO_DEPLOYMENT_ROOT:-/var/lib/bingo/deployments}
RECEIPT_ROOT=${BINGO_RECEIPT_ROOT:-/var/lib/bingo/backup-receipts}
LOG_FILE=${BINGO_LOG_FILE:-${BINGO_LOG_DIR}/operations.log}

die() { echo "bingo production operation failed: $*" >&2; exit 1; }
require_root() { [[ ${EUID} -eq 0 ]] || die "root is required"; }
require_command() { command -v "$1" >/dev/null 2>&1 || die "missing command: $1"; }
require_absolute() { [[ "$1" == /* && "$1" != *$'\n'* ]] || die "path must be absolute"; }
require_root_file() {
    local path=$1
    validate_root_owned_file "$path" || die "file must be a regular root:root 0600 file: $path"
}
require_root_path() {
    local path=$1 mode owner metadata
    [[ -e "$path" ]] || die "missing path: $path"
    metadata=$(file_metadata "$path") || die "could not inspect path: $path"
    owner=${metadata%%:*}
    mode=${metadata##*:}
    [[ "$owner" == 0 ]] || die "path must be root-owned: $path"
    [[ "$mode" =~ ^[0-7][^2367][^2367]$ ]] || die "path permissions are too broad: $path"
}
redact() {
    sed -E \
        -e 's/((password|secret|token|access[_-]?key|client[_-]?secret)[=:][[:space:]]*)[^[:space:]]+/\1[REDACTED]/Ig' \
        -e 's/(ConnectionStrings[^=]*=)[^[:space:]]+/\1[REDACTED]/Ig' \
        -e 's#(https?://[^/@[:space:]]+):[^/@[:space:]]+@#\1:[REDACTED]@#Ig'
}
log_line() { mkdir -p "$BINGO_LOG_DIR"; printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >> "$LOG_FILE"; chmod 600 "$LOG_FILE"; }
run_logged() {
    local label=$1 tmp status
    shift
    tmp=$(mktemp /tmp/bingo-command.XXXXXX)
    if "$@" >"$tmp" 2>&1; then status=0; else status=$?; fi
    redact <"$tmp" >>"$LOG_FILE"
    rm -f "$tmp"
    (( status == 0 )) || die "$label failed; see the redacted root-only log"
}
capture_logged() {
    local label=$1 output_file=$2 tmp status
    shift 2
    tmp=$(mktemp /tmp/bingo-command.XXXXXX)
    if "$@" >"$output_file" 2>"$tmp"; then status=0; else status=$?; fi
    redact <"$tmp" >>"$LOG_FILE"
    rm -f "$tmp"
    if (( status != 0 )); then rm -f "$output_file"; die "$label failed; see the redacted root-only log"; fi
}
acquire_lock() {
    require_command flock
    mkdir -p "$(dirname "$LOCK_FILE")"
    exec 9>"$LOCK_FILE"
    flock -n 9 || die "another production operation is active"
}
ensure_layout() {
    require_absolute "$BINGO_ROOT"; require_absolute "$BINGO_COMPOSE_FILE"; require_absolute "$BINGO_ENV_FILE"
    require_absolute "$BINGO_DATA_PROTECTION_DIR"
    require_root_path "$BINGO_COMPOSE_FILE"
    require_root_path "$BINGO_CADDY_FILE"
    require_root_file "$BINGO_ENV_FILE"; require_root_file "$BINGO_RESTIC_ENV_FILE"; require_root_file "$BINGO_RESTIC_PASSWORD_FILE"; require_root_file "$BINGO_GHCR_ENV_FILE"
    [[ -d "$BINGO_ROOT" && -d "$(dirname "$BINGO_COMPOSE_FILE")" ]] || die "production root is incomplete"
    mkdir -p "$BINGO_BACKUP_ROOT" "$DEPLOYMENT_ROOT" "$RECEIPT_ROOT" "$BINGO_LOG_DIR"
    chmod 700 "$BINGO_BACKUP_ROOT" "$DEPLOYMENT_ROOT" "$RECEIPT_ROOT" "$BINGO_LOG_DIR"
    # Production values are host-side and are never printed by this library.
    # shellcheck disable=SC1090
    source "$BINGO_ENV_FILE"
}
compose() { docker compose --project-directory "$BINGO_ROOT" --env-file "$BINGO_ENV_FILE" -f "$BINGO_COMPOSE_FILE" "$@"; }
validate_sha() { [[ "$1" =~ ^[0-9a-f]{40}$ ]] || die "source SHA must be lowercase full SHA"; }
validate_digest() { [[ "$1" =~ ^sha256:[0-9a-f]{64}$ ]] || die "image digest must be immutable sha256 digest"; }
validate_id() { [[ "$1" =~ ^[0-9]+$ ]] || die "run/deployment ID must be numeric"; }
utc_now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
json_array_file() { jq -R -s 'split("\n") | map(select(length > 0))' "$1"; }
directory_sha256() { tar --sort=name --mtime='UTC 1970-01-01' --owner=0 --group=0 --numeric-owner -C "$1" -cf - . | sha256sum | awk '{print $1}'; }
table_exists() {
    local result
    result=$(compose exec -T postgres psql -X -A -t -v ON_ERROR_STOP=1 -U "$BINGO_POSTGRES_USER" -d "$BINGO_POSTGRES_DB" -c "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$1') THEN '1' ELSE '0' END;") || die "could not inspect PostgreSQL schema"
    [[ "$result" == 1 ]]
}
migration_history() {
    local output=$1
    : >"$output"
    if table_exists '__EFMigrationsHistory'; then
        capture_logged "migration history" "$output" compose exec -T postgres psql -X -A -t -v ON_ERROR_STOP=1 -U "$BINGO_POSTGRES_USER" -d "$BINGO_POSTGRES_DB" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
    fi
}
health_wait() {
    local container=$1 i status
    for i in $(seq 1 60); do
        status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}unknown{{end}}' "$container" 2>/dev/null || true)
        [[ "$status" == healthy ]] && return 0
        [[ "$status" == unhealthy ]] && break
        sleep 2
    done
    return 1
}
