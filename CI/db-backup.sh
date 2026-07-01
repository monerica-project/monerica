#!/usr/bin/env bash
# db-backup.sh — pg_dump the monerica PostgreSQL DB on the VPS and download locally.
#
# Usage:
#   ./db-backup.sh                                # → ~/backups/monerica/<db>_<ts>.dump
#   ./db-backup.sh ~/Documents/db-backups         # custom destination folder
#   ./db-backup.sh ~/Documents/db-backups otherdb # custom destination + db name
#
# Reads connection details from deploy-config.sh:
#   SSH_HOST, SSH_USER, SSH_PASSWORD (optional), and the PG_* parts parsed from
#   DB_CONNECTION_STRING (PG_HOST, PG_PORT, PG_DB, PG_USER, PG_PASSWORD).
#
# NOTE: daily backups already run on the VPS via pg-backup.timer (all PG dbs →
# Azure Blob, GFS rotation). This is for an on-demand local copy.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"

ARG_DEST="${1:-}"
ARG_DB="${2:-}"

[[ -f "$CONFIG_PATH" ]] || { echo "Missing $CONFIG_PATH" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

LOCAL_DEST="${ARG_DEST:-$HOME/backups/monerica}"
DB="${ARG_DB:-${PG_DB:-monerica}}"

for tool in ssh scp; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool missing: $tool" >&2; exit 1; }
done

C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
write_step() { echo "${C_CYAN}==> $1${C_RESET}"; }
write_ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }

SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { echo "SSH_PASSWORD set but sshpass missing" >&2; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o ServerAliveInterval=30)
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"
ssh_run()  { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "$1"; }
scp_recv() { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" "$SSH_TARGET:$1" "$2"; }

TS=$(date -u +%Y%m%d_%H%M%S)
DUMP="${DB}_${TS}.dump"           # pg_dump custom format (-Fc): compressed + restorable via pg_restore
REMOTE_PATH="/tmp/$DUMP"
mkdir -p "$LOCAL_DEST"
LOCAL_PATH="$LOCAL_DEST/$DUMP"

echo
echo "${C_CYAN}----- Backup [$DB] from $SSH_HOST -> $LOCAL_PATH -----${C_RESET}"

write_step "pg_dump (custom format, compressed)"
ssh_run "PGPASSWORD='$PG_PASSWORD' pg_dump -h '$PG_HOST' -p '$PG_PORT' -U '$PG_USER' -Fc '$DB' > '$REMOTE_PATH'"
write_ok "Written: $REMOTE_PATH"

write_step "pg_restore --list (integrity check)"
ssh_run "pg_restore --list '$REMOTE_PATH' >/dev/null"
write_ok "Verified"

write_step "Downloading to local"
scp_recv "$REMOTE_PATH" "$LOCAL_PATH"
write_ok "Saved: $LOCAL_PATH ($(du -h "$LOCAL_PATH" | awk '{print $1}'))"

write_step "Cleaning up remote temp file"
ssh_run "rm -f '$REMOTE_PATH'"
write_ok "Remote cleanup done"

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Backup complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo "  ${C_GRAY}File:${C_RESET}    $LOCAL_PATH"
echo "  ${C_GRAY}Size:${C_RESET}    $(du -h "$LOCAL_PATH" | awk '{print $1}')"
echo "  ${C_GRAY}Restore:${C_RESET} pg_restore -h <host> -U <user> -d <db> --clean --if-exists '$DUMP'"
echo
