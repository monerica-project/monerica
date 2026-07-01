#!/usr/bin/env bash
# CI/db-rotate-credentials.sh — Create or rotate a PostgreSQL role on the deployed VPS.
#
# Companion to deploy.sh / deploy-jobs.sh. Reuses deploy-config.sh for the SSH
# target and the PG_* connection parts. Runs psql as the postgres superuser
# (peer auth via `sudo -u postgres`), matching how the VPS Postgres is administered.
#
# USAGE
#   ./db-rotate-credentials.sh --login <role>                  # generate a password
#   ./db-rotate-credentials.sh --login <role> --password '<p>' # use provided password
#
# EXAMPLES
#   Rotate the app role's password:
#     ./db-rotate-credentials.sh --login monerica
#
# BEHAVIOR
#   - If the role does NOT exist:  CREATE ROLE (LOGIN) + grant full privileges on
#     the app database + schema public (db_owner equivalent).
#   - If the role DOES exist:      ALTER ROLE with the new password (privileges
#     left unchanged).
#   - Either way, the new credentials are tested with SELECT 1 before printing.
#
# AFTER A SUCCESSFUL RUN
#   1. Update CI/deploy-config.sh DB_CONNECTION_STRING with the new password
#      (the script prints the full string at the end).
#   2. Redeploy web + all jobs (commands printed at the end).
#   3. Verify each service is healthy on the new credentials.

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
        --login)    LOGIN_NAME="$2";   shift 2 ;;
        --password) NEW_PASSWORD="$2"; shift 2 ;;
        -h|--help)  sed -n '2,30p' "$0"; exit 0 ;;
        *)          echo "Unknown arg: $1" >&2; exit 1 ;;
    esac
done

[[ -n "$LOGIN_NAME" ]] || { echo "ERROR: --login <role> is required" >&2; exit 1; }
: "${PG_DB:?missing (parsed from DB_CONNECTION_STRING)}"
: "${PG_HOST:?missing}"; : "${PG_PORT:?missing}"
: "${SSH_HOST:?missing in deploy-config.sh}"
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

# Role names limited to a safe identifier set (no quoting headaches).
if [[ ! "$LOGIN_NAME" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
    echo "ERROR: role name must match [A-Za-z_][A-Za-z0-9_]*" >&2
    exit 1
fi

# ----------------------------------------------------------------------------
# Password generation / validation
# ----------------------------------------------------------------------------
if [[ -z "$NEW_PASSWORD" ]]; then
    set +o pipefail
    NEW_PASSWORD="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32)"
    set -o pipefail
    [[ ${#NEW_PASSWORD} -eq 32 ]] || { echo "ERROR: password generation failed" >&2; exit 1; }
fi
if [[ "$NEW_PASSWORD" == *"'"* ]]; then
    echo "ERROR: passwords containing single quotes (') are not supported by this script" >&2
    exit 1
fi

SSH_PREFIX=()
[[ -n "${SSH_PASSWORD:-}" ]] && SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10)

# Run SQL as the postgres superuser (peer auth).
pg_super()    { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo -u postgres psql -v ON_ERROR_STOP=1 -tA $*" 2>&1 | grep -v 'could not change directory'; }
pg_super_db() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo -u postgres psql -v ON_ERROR_STOP=1 -tA -d '$PG_DB' $*" 2>&1 | grep -v 'could not change directory'; }
# Test login as the role over TCP with the new password.
pg_test_login() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "PGPASSWORD='$2' psql -h '$PG_HOST' -p '$PG_PORT' -U '$1' -d '$PG_DB' -tAc 'SELECT 1'" >/dev/null 2>&1; }

echo "==> Target:   $SSH_TARGET"
echo "==> Database: $PG_DB"
echo "==> Role:     $LOGIN_NAME"

echo "==> Checking whether role exists..."
exists=$(pg_super -c "SELECT 1 FROM pg_roles WHERE rolname='$LOGIN_NAME';" | tr -d '[:space:]')

if [[ "$exists" == "1" ]]; then
    echo "==> Role exists — rotating password (privileges left unchanged)"
    pg_super -c "ALTER ROLE \"$LOGIN_NAME\" WITH LOGIN PASSWORD '$NEW_PASSWORD';" >/dev/null
else
    echo "==> Role does not exist — creating and granting full privileges on $PG_DB"
    pg_super -c "CREATE ROLE \"$LOGIN_NAME\" WITH LOGIN PASSWORD '$NEW_PASSWORD';" >/dev/null
    pg_super -c "GRANT ALL PRIVILEGES ON DATABASE \"$PG_DB\" TO \"$LOGIN_NAME\";" >/dev/null
    pg_super_db -c "GRANT ALL ON SCHEMA public TO \"$LOGIN_NAME\";
                    GRANT ALL ON ALL TABLES IN SCHEMA public TO \"$LOGIN_NAME\";
                    GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO \"$LOGIN_NAME\";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO \"$LOGIN_NAME\";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO \"$LOGIN_NAME\";" >/dev/null
fi

echo "==> Testing new credentials..."
if pg_test_login "$LOGIN_NAME" "$NEW_PASSWORD"; then
    echo "==> Login works."
else
    echo "ERROR: could not authenticate as '$LOGIN_NAME' with the new password." >&2
    echo "       The change MAY have applied — check with: sudo -u postgres psql -c '\\du'" >&2
    exit 1
fi

NEW_CONN="Host=$PG_HOST;Port=$PG_PORT;Database=$PG_DB;Username=$LOGIN_NAME;Password=$NEW_PASSWORD"

cat <<EOF

================================================================================
SUCCESS

Update CI/deploy-config.sh with:

  DB_CONNECTION_STRING='$NEW_CONN'

Then redeploy everything that holds the connection string:

  cd "$SCRIPT_DIR"
  ./deploy.sh web
  for j in newsletter-sender sponsored-listing-opening \\
           sponsored-listing-reminder email-message-maker site-checker; do
      ./deploy-jobs.sh "\$j"
  done

VERIFY each background job picked up the new credentials by triggering a run
and reading the journal, e.g.:

  ssh $SSH_TARGET 'sudo systemctl start dm-job-sponsored-listing-reminder.service \
      && sudo journalctl -u dm-job-sponsored-listing-reminder.service -n 20 --no-pager'

The new password is shown once, here. Save it now; this script does not log it.
================================================================================
EOF
