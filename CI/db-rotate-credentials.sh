#!/usr/bin/env bash
# CI/db-rotate-credentials.sh — Create or rotate a SQL Server login on the deployed VPS.
#
# Companion to deploy.sh / deploy-jobs.sh. Reuses deploy-config.sh for the SSH
# target and current MSSQL_SA_PASSWORD. Runs sqlcmd inside the mssql container,
# matching how deploy.sh's task_setup_mssql does it.
#
# USAGE
#   ./db-rotate-credentials.sh --login <name>                  # generate a password
#   ./db-rotate-credentials.sh --login <name> --password '<p>' # use provided password
#
# EXAMPLES
#   First time, create a non-sa app login:
#     ./db-rotate-credentials.sh --login monerica_app
#
#   Later, rotate that login's password:
#     ./db-rotate-credentials.sh --login monerica_app
#
#   Rotate the sa password (only after you've moved everything to monerica_app
#   and verified all services are healthy on it):
#     ./db-rotate-credentials.sh --login sa
#
# BEHAVIOR
#   - If the login does NOT exist:  CREATE LOGIN, CREATE USER on $DB_NAME, grant db_owner.
#   - If the login DOES exist:      ALTER LOGIN with new password (no permission changes).
#   - Either way, the script tests the new credentials with a SELECT 1 before
#     printing them.
#
# AFTER A SUCCESSFUL RUN
#   1. Update CI/deploy-config.sh with the new DB_USER / DB_PASSWORD /
#      DB_CONNECTION_STRING (the script prints them at the end).
#   2. Redeploy web + all jobs (commands also printed at the end).
#   3. Verify each service is healthy on the new credentials before rotating sa.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"
[[ -f "$CONFIG_PATH" ]] || { echo "ERROR: $CONFIG_PATH not found" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

# ----------------------------------------------------------------------------
# Args
# ----------------------------------------------------------------------------
LOGIN_NAME=""
NEW_PASSWORD=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --login)    LOGIN_NAME="$2";    shift 2 ;;
        --password) NEW_PASSWORD="$2";  shift 2 ;;
        -h|--help)  sed -n '2,35p' "$0"; exit 0 ;;
        *)          echo "Unknown arg: $1" >&2; exit 1 ;;
    esac
done

[[ -n "$LOGIN_NAME" ]] || { echo "ERROR: --login <name> is required" >&2; exit 1; }
: "${MSSQL_SA_PASSWORD:?missing in deploy-config.sh}"
: "${DB_NAME:?missing in deploy-config.sh}"
: "${SSH_HOST:?missing in deploy-config.sh}"
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

# Reject login names with characters that would need T-SQL identifier escaping.
# A-Z a-z 0-9 underscore is plenty for what we need.
if [[ ! "$LOGIN_NAME" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
    echo "ERROR: login name must match [A-Za-z_][A-Za-z0-9_]*" >&2
    exit 1
fi

# ----------------------------------------------------------------------------
# Password generation / validation
# ----------------------------------------------------------------------------
if [[ -z "$NEW_PASSWORD" ]]; then
    # 32 chars from [A-Za-z0-9]: ~190 bits of entropy. Stays safe in JSON,
    # bash single-quoted strings, T-SQL string literals, and connection
    # strings — no escaping required anywhere downstream.
    #
    # Disable pipefail for just this line: head closes the pipe after 32
    # bytes which sends SIGPIPE to tr (exit 141). With pipefail+set -e
    # the whole script would die silently before printing anything.
    set +o pipefail
    NEW_PASSWORD="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32)"
    set -o pipefail
    [[ ${#NEW_PASSWORD} -eq 32 ]] || { echo "ERROR: password generation failed" >&2; exit 1; }
fi

# Single quotes in passwords would break the inline T-SQL we send. Refuse them.
if [[ "$NEW_PASSWORD" == *"'"* ]]; then
    echo "ERROR: passwords containing single quotes (') are not supported by this script" >&2
    exit 1
fi

# SQL Server requires CHECK_POLICY-compliant passwords if CHECK_POLICY=ON.
# Our generated passwords meet typical complexity rules, but we set
# CHECK_POLICY=OFF on creation (matching task_setup_mssql) so user-supplied
# passwords aren't blocked by Windows password policy on the host.

# ----------------------------------------------------------------------------
# sqlcmd helpers (mirror deploy.sh's mssql_exec but pipe SQL via stdin so we
# don't have to escape the SQL through two layers of shell quoting)
# ----------------------------------------------------------------------------

# Run a SQL payload as sa, return its stdout.
mssql_exec_sa() {
    local sql="$1"
    ssh "$SSH_TARGET" \
        "sudo docker exec -i mssql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -h -1 -W" \
        <<< "$sql"
}

# Test login by running SELECT 1 as that user. Returns 0 on success.
mssql_test_login() {
    local user="$1" pass="$2"
    ssh "$SSH_TARGET" \
        "sudo docker exec mssql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U '$user' -P '$pass' -C -b -Q 'SELECT 1'" \
        >/dev/null 2>&1
}

echo "==> Target:   $SSH_TARGET"
echo "==> Database: $DB_NAME"
echo "==> Login:    $LOGIN_NAME"

# ----------------------------------------------------------------------------
# Detect whether the login already exists
# ----------------------------------------------------------------------------
echo "==> Checking whether login exists..."
exists_count=$(mssql_exec_sa \
    "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.server_principals WHERE name = '$LOGIN_NAME';" \
    | tr -d '[:space:]\r')

case "$exists_count" in
    0)
        echo "==> Login does not exist — creating and granting db_owner on $DB_NAME"
        mssql_exec_sa "
CREATE LOGIN [$LOGIN_NAME] WITH PASSWORD = '$NEW_PASSWORD', CHECK_POLICY = OFF;
GO
USE [$DB_NAME];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$LOGIN_NAME')
    CREATE USER [$LOGIN_NAME] FOR LOGIN [$LOGIN_NAME];
ALTER ROLE db_owner ADD MEMBER [$LOGIN_NAME];
GO
"
        ;;
    1)
        echo "==> Login exists — rotating password (permissions left unchanged)"
        mssql_exec_sa "
ALTER LOGIN [$LOGIN_NAME] WITH PASSWORD = '$NEW_PASSWORD';
GO
"
        ;;
    *)
        echo "ERROR: unexpected response from sys.server_principals (got '$exists_count')" >&2
        exit 1
        ;;
esac

# ----------------------------------------------------------------------------
# Verify the new credentials before printing anything the user will rely on
# ----------------------------------------------------------------------------
echo "==> Testing new credentials..."
if mssql_test_login "$LOGIN_NAME" "$NEW_PASSWORD"; then
    echo "==> Login works."
else
    echo "ERROR: could not authenticate as '$LOGIN_NAME' with the new password." >&2
    echo "       The change MAY have applied — check sys.sql_logins manually." >&2
    exit 1
fi

# ----------------------------------------------------------------------------
# Print the values to paste into deploy-config.sh + the redeploy command list
# ----------------------------------------------------------------------------
NEW_CONN="Server=127.0.0.1,1433;Database=$DB_NAME;User Id=$LOGIN_NAME;Password=$NEW_PASSWORD;TrustServerCertificate=true;Encrypt=false;"

cat <<EOF

================================================================================
SUCCESS

Update CI/deploy-config.sh with:

  DB_USER='$LOGIN_NAME'
  DB_PASSWORD='$NEW_PASSWORD'
  DB_CONNECTION_STRING='$NEW_CONN'

If you rotated the sa password, also update:

  MSSQL_SA_PASSWORD='<new sa password>'

Then redeploy everything that holds the connection string:

  cd "$SCRIPT_DIR"
  ./deploy.sh web
  for j in newsletter-sender sponsored-listing-opening \\
           sponsored-listing-reminder email-message-maker site-checker; do
      ./deploy-jobs.sh "\$j"
  done

VERIFY each background job picked up the new credentials by triggering a run
and reading the journal. For example:

  ssh $SSH_TARGET 'sudo systemctl start dm-job-sponsored-listing-reminder.service \
      && sudo journalctl -u dm-job-sponsored-listing-reminder.service -n 20 --no-pager'

Look for the normal "Found N listings expiring within X hours." line — that
proves the DB connection works on the new credentials.

The new password is shown once, here. Save it to your password manager now;
this script does not log it anywhere.
================================================================================
EOF
