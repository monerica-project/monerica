#!/usr/bin/env bash
# db-refresh.sh — Refresh the production PostgreSQL DB on the VPS from a
# pg_dump custom-format .dump file (as produced by db-backup.sh).
#
# Usage:
#   ./db-refresh.sh path/to/monerica.dump                  # default: stop dm-web during restore
#   ./db-refresh.sh path/to/monerica.dump dm-web,dm-api    # also stop these
#
# Produce a .dump with:  ./db-backup.sh    (or) pg_dump -Fc <db> > file.dump
#
# This script:
#   1. Uploads the .dump to the server
#   2. Stops the named services (so no concurrent writes)
#   3. pg_restore --clean --if-exists into the prod DB (drops+recreates objects)
#   4. Restarts the services
#
# WARNING: this REPLACES the contents of the production database. It requires
# typing the database name to confirm.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/deploy-config.sh"

DUMP_FILE="${1:-}"
SERVICES_CSV="${2:-dm-web}"

[[ -z "$DUMP_FILE" ]] && { echo "Usage: $0 path/to/monerica.dump [service1,service2,...]" >&2; exit 1; }
[[ -f "$DUMP_FILE" ]] || { echo "File not found: $DUMP_FILE" >&2; exit 1; }
: "${PG_DB:?missing (parsed from DB_CONNECTION_STRING)}"

C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_YELLOW=$'\e[33m' C_RED=$'\e[31m' C_RESET=$'\e[0m'
say()   { echo "${C_CYAN}==> $1${C_RESET}"; }
ok()    { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
err()   { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { err "sshpass missing"; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10)
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"
ssh_run()  { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "$1" 2>&1 | grep -v 'could not change directory'; }
scp_send() { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" "$1" "$SSH_TARGET:$2"; }

echo
echo "${C_YELLOW}This will REPLACE database '$PG_DB' on $SSH_HOST.${C_RESET}"
echo "Source:   $DUMP_FILE  ($(du -h "$DUMP_FILE" | cut -f1))"
echo "Services: $SERVICES_CSV (will be stopped/started)"
read -rp "Type the database name to continue: " confirm
[[ "$confirm" == "$PG_DB" ]] || { err "Confirmation didn't match. Aborting."; exit 1; }

# 1. Upload the dump
say "Uploading .dump..."
remote_dump="/tmp/$(basename "$DUMP_FILE")"
scp_send "$DUMP_FILE" "$remote_dump"
ok "Uploaded"

# 2. Stop services (no concurrent writes during restore)
IFS=',' read -ra SVCS <<< "$SERVICES_CSV"
for s in "${SVCS[@]}"; do
    say "Stopping $s..."
    ssh_run "sudo systemctl stop $s 2>/dev/null || true"
done

# 3. Restore into the prod DB as the app role. --clean --if-exists drops+recreates
#    objects; --no-owner keeps everything owned by the connecting role.
say "Restoring database $PG_DB..."
ssh_run "PGPASSWORD='$PG_PASSWORD' pg_restore --clean --if-exists --no-owner \
    -h '$PG_HOST' -p '$PG_PORT' -U '$PG_USER' -d '$PG_DB' '$remote_dump'"
ok "Database refreshed"

# 4. Cleanup
ssh_run "rm -f '$remote_dump'"

# 5. Restart services
for s in "${SVCS[@]}"; do
    say "Starting $s..."
    ssh_run "sudo systemctl start $s 2>/dev/null || true"
done

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} DB refresh complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
