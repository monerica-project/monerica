#!/usr/bin/env bash
# CI/db-verify-deployed-configs.sh — Audit which DB login every deployed
# Monerica service is configured to use, and confirm SQL Server sees only
# the expected logins connecting.
#
# USAGE
#   ./db-verify-deployed-configs.sh
#
# Reads MSSQL_SA_PASSWORD, DB_NAME, DB_USER (the expected new login), and
# SSH_HOST/SSH_USER from deploy-config.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"
[[ -f "$CONFIG_PATH" ]] || { echo "ERROR: $CONFIG_PATH not found" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

: "${MSSQL_SA_PASSWORD:?missing in deploy-config.sh}"
: "${DB_NAME:?missing in deploy-config.sh}"
: "${DB_USER:?missing in deploy-config.sh}"
: "${SSH_HOST:?missing in deploy-config.sh}"
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

EXPECTED_USER="$DB_USER"

echo "=============================================================="
echo "Expected DB login: $EXPECTED_USER"
echo "Database:          $DB_NAME"
echo "VPS:               $SSH_TARGET"
echo "=============================================================="

# ----------------------------------------------------------------------------
# 1. Find every appsettings*.json under /var/www and report its User Id.
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
printf "%-70s  %s\n" "FILE" "User Id"
printf "%-70s  %s\n" "----" "-------"
for f in "${files[@]}"; do
    # Pull "User Id=<value>" from the connection string. Empty if not set
    # (e.g. the empty-placeholder appsettings.json that ships with templates).
    uid=$(sudo grep -oE 'User Id=[^;"]+' "$f" 2>/dev/null | head -1 | cut -d= -f2- || true)
    [[ -z "$uid" ]] && uid="(none)"
    printf "%-70s  %s\n" "$f" "$uid"
done
REMOTE

# ----------------------------------------------------------------------------
# 2. Ask SQL Server which logins have actually connected recently.
#    sys.dm_exec_sessions shows current connections; sys.dm_exec_connections
#    is similar. We use sessions because it includes login_name + program_name.
# ----------------------------------------------------------------------------
echo
echo "=== Live audit: SQL Server sessions on $DB_NAME ==="
echo
ssh "$SSH_TARGET" \
    "sudo docker exec -i mssql /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -h -1 -W" <<SQL
SET NOCOUNT ON;
SELECT
    login_name,
    COUNT(*) AS sessions,
    MAX(login_time) AS most_recent_login
FROM sys.dm_exec_sessions
WHERE database_id = DB_ID('$DB_NAME')
  AND is_user_process = 1
GROUP BY login_name
ORDER BY login_name;
GO
SQL

# ----------------------------------------------------------------------------
# 3. Trigger every background job, then read its journal and check for the
#    canonical "Processing complete." marker. Failures surface as either an
#    auth error in the journal or a non-zero systemd exit code.
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

for j in "\${JOBS[@]}"; do
    : # placeholder so the heredoc below works in some editors; real loop next
done

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
echo "  File audit:  every row should show User Id = $EXPECTED_USER"
echo "               (placeholder appsettings.json files with '(none)'"
echo "                are normal — only Production.json carries the real value)"
echo
echo "  Live audit:  login_name should be only $EXPECTED_USER"
echo "               (if 'sa' appears, something is still using sa — track"
echo "                down which service from program_name in dm_exec_sessions"
echo "                or the file audit above)"
echo
echo "  Functional:  each job's Result should be 'success' and ExecMainStatus 0,"
echo "               and the journal tail should NOT contain 'Login failed' or"
echo "               'Cannot open database'"
echo "=============================================================="
