#!/usr/bin/env bash

file_metadata() {
    local path=$1 metadata
    if metadata=$(stat -c '%u:%g:%a' "$path" 2>/dev/null); then
        printf '%s\n' "$metadata"
    else
        stat -f '%u:%g:%Lp' "$path"
    fi
}

validate_root_owned_file() {
    local path=${1:-} metadata
    [[ -n "$path" && -f "$path" && ! -L "$path" ]] || return 1
    metadata=$(file_metadata "$path") || return 1
    [[ "$metadata" == 0:0:600 ]]
}

validate_ops_config_content() {
    local path=$1 line key required
    while IFS= read -r line || [[ -n "$line" ]]; do
        [[ -z "$line" || "$line" == \#* ]] && continue
        [[ "$line" =~ ^BINGO_[A-Z0-9_]+=([^[:space:]\'\"\$\;\|\&\<\>\(\)\`\\]*)$ ]] || return 1
        key=${line%%=*}
        case "$key" in
            BINGO_ROOT|BINGO_COMPOSE_FILE|BINGO_CADDY_FILE|BINGO_ENV_FILE|BINGO_RESTIC_ENV_FILE|BINGO_RESTIC_PASSWORD_FILE|BINGO_GHCR_ENV_FILE|BINGO_BACKUP_ROOT|BINGO_LOG_DIR|BINGO_DEPLOYMENT_ROOT|BINGO_RECEIPT_ROOT|BINGO_LOCK_FILE|BINGO_IMAGE_NAME|BINGO_DOMAIN|BINGO_BOOTSTRAP_OWNER_USERNAME|BINGO_DATA_PROTECTION_DIR|BINGO_RETAINED_LEGACY_PREFLIGHT|BINGO_RETAINED_OWNER|BINGO_POSTGRES_IMAGE|BINGO_KEEP_DAILY|BINGO_KEEP_WEEKLY|BINGO_KEEP_MONTHLY) ;;
            *) return 1 ;;
        esac
    done <"$path"
    for required in BINGO_ROOT BINGO_COMPOSE_FILE BINGO_CADDY_FILE BINGO_ENV_FILE BINGO_RESTIC_ENV_FILE BINGO_RESTIC_PASSWORD_FILE BINGO_GHCR_ENV_FILE BINGO_BACKUP_ROOT BINGO_LOG_DIR BINGO_IMAGE_NAME BINGO_DOMAIN BINGO_BOOTSTRAP_OWNER_USERNAME BINGO_DATA_PROTECTION_DIR BINGO_RETAINED_LEGACY_PREFLIGHT BINGO_POSTGRES_IMAGE BINGO_KEEP_DAILY BINGO_KEEP_WEEKLY BINGO_KEEP_MONTHLY; do
        grep -q "^${required}=" "$path" || return 1
    done
}
