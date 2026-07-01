#!/usr/bin/env bash
# db-sync-local.sh — Pull the latest prod PostgreSQL DB and restore it into a
# local Docker Postgres for development.
#
# Usage:
#   ./db-sync-local.sh                        # fresh pg_dump from prod, restore local
#   ./db-sync-local.sh path/to/file.dump      # skip pull, use an existing .dump
#
# Reads prod SSH + PG_* connection from deploy-config.sh.
# Local target (override via env):
#   LOCAL_CONTAINER=monerica-pg   LOCAL_DB=monerica   LOCAL_USER=postgres

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/deploy-config.sh"

LOCAL_CONTAINER="${LOCAL_CONTAINER:-monerica-pg}"
LOCAL_DB="${LOCAL_DB:-${PG_DB:-monerica}}"
LOCAL_USER="${LOCAL_USER:-postgres}"
BACKUP_DIR="${BACKUP_DIR:-$HOME/backups/monerica}"

ARG_DUMP="${1:-}"

C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_RED=$'\e[31m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
say()  { echo "${C_CYAN}==> $1${C_RESET}"; }
ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
err()  { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

# 1. Get a .dump (fresh from prod, or supplied)
if [[ -n "$ARG_DUMP" ]]; then
    [[ -f "$ARG_DUMP" ]] || { err "File not found: $ARG_DUMP"; exit 1; }
    DUMP="$ARG_DUMP"
    say "Using existing dump: $DUMP"
else
    say "Pulling fresh pg_dump from $SSH_HOST..."
    "$SCRIPT_DIR/db-backup.sh" "$BACKUP_DIR"
    DUMP=$(ls -t "$BACKUP_DIR"/${PG_DB:-monerica}_*.dump 2>/dev/null | head -1)
    [[ -n "$DUMP" ]] || { err "Couldn't locate downloaded .dump in $BACKUP_DIR"; exit 1; }
    ok "Downloaded: $DUMP"
fi

# 2. Verify local container is running
docker inspect "$LOCAL_CONTAINER" >/dev/null 2>&1 \
    || { err "Local container '$LOCAL_CONTAINER' not found. Set LOCAL_CONTAINER or start a local Postgres (e.g. docker run -d --name monerica-pg -e POSTGRES_PASSWORD=pg -p 5432:5432 postgres:17)."; exit 1; }
[[ "$(docker inspect -f '{{.State.Running}}' "$LOCAL_CONTAINER")" == "true" ]] \
    || { err "Local container '$LOCAL_CONTAINER' isn't running."; exit 1; }

# 3. Copy the dump into the container
say "Copying dump into '$LOCAL_CONTAINER'..."
docker cp "$DUMP" "$LOCAL_CONTAINER:/tmp/refresh.dump"
ok "Copied"

# 4. Recreate the local DB and restore (--no-owner maps everything to LOCAL_USER)
say "Restoring $LOCAL_DB (drop + recreate)..."
docker exec -u postgres "$LOCAL_CONTAINER" bash -c "
    psql -v ON_ERROR_STOP=1 -c \"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$LOCAL_DB' AND pid<>pg_backend_pid();\" >/dev/null 2>&1 || true
    dropdb --if-exists '$LOCAL_DB'
    createdb -O '$LOCAL_USER' '$LOCAL_DB'
    pg_restore --no-owner --role='$LOCAL_USER' -d '$LOCAL_DB' /tmp/refresh.dump
    rm -f /tmp/refresh.dump
"
ok "Restored"

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Local DB sync complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo "  ${C_GRAY}From:${C_RESET}  $DUMP"
echo "  ${C_GRAY}To:${C_RESET}    $LOCAL_DB @ $LOCAL_CONTAINER ($LOCAL_USER)"
echo
