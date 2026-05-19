#!/usr/bin/env bash
# db-backup.sh — back up monerica_db on the VPS and download to a local folder.
#
# Usage:
#   ./db-backup.sh                                # → ~/backups/monerica/<db>_<ts>.bak
#   ./db-backup.sh ~/Documents/db-backups         # custom destination folder
#   ./db-backup.sh ~/Documents/db-backups otherdb # custom destination + db name
#
# Reads connection details from deploy-config.sh:
#   SSH_HOST, SSH_USER, SSH_PASSWORD (optional), MSSQL_SA_PASSWORD, DB_NAME

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"

ARG_DEST="${1:-}"
ARG_DB="${2:-}"

[[ -f "$CONFIG_PATH" ]] || { echo "Missing $CONFIG_PATH" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

LOCAL_DEST="${ARG_DEST:-$HOME/backups/monerica}"
DB="${ARG_DB:-${DB_NAME:-monerica_db}}"

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
ssh_run()  { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'"; }
scp_send() { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" "$1" "$SSH_TARGET:$2"; }
scp_recv() { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" "$SSH_TARGET:$1" "$2"; }

# Run a SQL script in the mssql container by uploading it as a file first.
# Avoids shell-quoting hell when SQL contains single quotes (N'...' literals).
# `docker cp` lands the file as root:root inside the container, but sqlcmd
# runs as the `mssql` user — so chmod 644 it (as root) before -i, and rm it
# (as root) after.
run_sql_file() {
    local local_sql="$1"
    local fname; fname="$(basename "$local_sql")"
    scp_send "$local_sql" "/tmp/$fname"
    ssh_run "docker cp /tmp/$fname mssql:/tmp/$fname && docker exec -u root mssql chmod 644 /tmp/$fname && docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -i /tmp/$fname && docker exec -u root mssql rm -f /tmp/$fname && rm -f /tmp/$fname"
}

TS=$(date -u +%Y%m%d_%H%M%S)
BAK="${DB}_${TS}.bak"
REMOTE_CONTAINER_PATH="/var/opt/mssql/backup/$BAK"
REMOTE_HOST_PATH="/tmp/$BAK"
mkdir -p "$LOCAL_DEST"
LOCAL_PATH="$LOCAL_DEST/$BAK"

echo
echo "${C_CYAN}----- Backup [$DB] from $SSH_HOST -> $LOCAL_PATH -----${C_RESET}"

TMP_BACKUP_SQL="$(mktemp /tmp/dbbackup-XXXXXX.sql)"
TMP_VERIFY_SQL="$(mktemp /tmp/dbverify-XXXXXX.sql)"
trap 'rm -f "$TMP_BACKUP_SQL" "$TMP_VERIFY_SQL"' EXIT

cat > "$TMP_BACKUP_SQL" <<SQL
BACKUP DATABASE [$DB] TO DISK = N'$REMOTE_CONTAINER_PATH' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
SQL

cat > "$TMP_VERIFY_SQL" <<SQL
RESTORE VERIFYONLY FROM DISK = N'$REMOTE_CONTAINER_PATH';
SQL

write_step "BACKUP DATABASE (compressed, checksummed)"
run_sql_file "$TMP_BACKUP_SQL"
write_ok "Written: $REMOTE_CONTAINER_PATH"

write_step "RESTORE VERIFYONLY (integrity check)"
run_sql_file "$TMP_VERIFY_SQL"
write_ok "Verified"

write_step "Copying out of container onto VPS host"
ssh_run "docker cp mssql:$REMOTE_CONTAINER_PATH $REMOTE_HOST_PATH && chmod 644 $REMOTE_HOST_PATH"
write_ok "Staged: $REMOTE_HOST_PATH"

write_step "Downloading to local"
scp_recv "$REMOTE_HOST_PATH" "$LOCAL_PATH"
write_ok "Saved: $LOCAL_PATH ($(du -h "$LOCAL_PATH" | awk '{print $1}'))"

write_step "Cleaning up remote temp files"
ssh_run "rm -f $REMOTE_HOST_PATH && docker exec mssql rm -f $REMOTE_CONTAINER_PATH"
write_ok "Remote cleanup done"

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Backup complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo "  ${C_GRAY}File:${C_RESET}    $LOCAL_PATH"
echo "  ${C_GRAY}Size:${C_RESET}    $(du -h "$LOCAL_PATH" | awk '{print $1}')"
echo "  ${C_GRAY}Restore:${C_RESET} RESTORE DATABASE [$DB] FROM DISK = N'<path>' WITH REPLACE;"
echo
