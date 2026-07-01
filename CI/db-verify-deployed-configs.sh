#!/usr/bin/env bash
# CI/db-verify-deployed-configs.sh — Audit which PostgreSQL role every deployed
# Monerica service is configured to use, confirm Postgres sees only the expected
# role connecting, and check each background job runs cleanly.
#
# USAGE
#   ./db-verify-deployed-configs.sh
#
# Reads SSH_HOST/SSH_USER and the PG_* parts (parsed from DB_CONNECTION_STRING)
# from deploy-config.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"
[[ -f "$CONFIG_PATH" ]] || { echo "ERROR: $CONFIG_PATH not found" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

: "${PG_DB:?missing (parsed from DB_CONNECTION_STRING)}"
: "${PG_USER:?missing (parsed from DB_CONNECTION_STRING)}"
: "${SSH_HOST:?missing in deploy-config.sh}"
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

EXPECTED_USER="$PG_USER"

echo "=============================================================="
echo "Expected DB role: $EXPECTED_USER"
echo "Database:         $PG_DB"
echo "VPS:              $SSH_TARGET"
echo "=============================================================="

# ----------------------------------------------------------------------------
# 1. Find every appsettings*.json under /var/www and report its Username.
# ----------------------------------------------------------------------------
echo
echo "=== File audit: every deployed appsettings*.json under /var/www ==="
echo
ssh "$SSH_TARGET" "sudo bash -s" <<'REMOTE'
set -e
shopt -s nullglob
mapfile -t files < <(sudo find /var/www -maxdepth 4 -type f -name "appsettings*.json" 2>/dev/null | sort)
if [[ ${#files[@]} -eq 0 ]]; then
    echo "  (no appsettings*.json files found under /var/www)"
    exit 0
fi
printf "%-70s  %s\n" "FILE" "Username"
printf "%-70s  %s\n" "----" "--------"
for f in "${files[@]}"; do
    # Pull "Username=<value>" from the Npgsql connection string. Empty if not set
    # (e.g. the empty-placeholder appsettings.json that ships with templates).
    uid=$(sudo grep -oiE 'Username=[^;"]+' "$f" 2>/dev/null | head -1 | cut -d= -f2- || true)
    [[ -z "$uid" ]] && uid="(none)"
    printf "%-70s  %s\n" "$f" "$uid"
done
REMOTE

# ----------------------------------------------------------------------------
# 2. Ask PostgreSQL which roles are actually connected (pg_stat_activity).
#    Run as the postgres superuser so it can see every session, not just its own.
# ----------------------------------------------------------------------------
echo
echo "=== Live audit: PostgreSQL sessions on $PG_DB ==="
echo
ssh "$SSH_TARGET" "sudo -u postgres psql -d '$PG_DB' -P pager=off -c \"
SELECT usename AS login_role, count(*) AS sessions, max(backend_start) AS most_recent_connect
FROM pg_stat_activity
WHERE datname = '$PG_DB' AND usename IS NOT NULL
GROUP BY usename ORDER BY usename;\"" 2>&1 | grep -v 'could not change directory'

# ----------------------------------------------------------------------------
# 3. Trigger every background job, then read its journal and exit code.
# ----------------------------------------------------------------------------
JOBS=(
    newsletter-sender
    sponsored-listing-opening
    sponsored-listing-reminder
    email-message-maker
    site-checker
)

echo
echo "=== Functional audit: trigger each background job, check exit + log ==="
echo

for j in "${JOBS[@]}"; do
    unit="dm-job-$j.service"
    echo "--- $unit ---"
    ssh "$SSH_TARGET" "sudo systemctl start $unit; sleep 8; \
        sudo systemctl show $unit -p Result -p ExecMainStatus --value | paste -sd' / ' -; \
        sudo journalctl -u $unit -n 6 --no-pager --output=cat"
    echo
done

echo "=============================================================="
echo "DONE. Quick read of results:"
echo
echo "  File audit:  every real Production.json should show Username = $EXPECTED_USER"
echo "               (placeholder appsettings.json with '(none)' are normal)"
echo
echo "  Live audit:  login_role should be only $EXPECTED_USER (plus 'postgres'"
echo "               for this audit query itself). Anything else means some"
echo "               service is connecting with an unexpected role."
echo
echo "  Functional:  each job's Result should be 'success' and ExecMainStatus 0,"
echo "               and the journal tail should NOT contain a connection/auth error."
echo "=============================================================="
