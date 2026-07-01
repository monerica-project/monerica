#!/usr/bin/env bash

# ============================================================================
# SUPERSEDED (2026-07-01): monerica is now on PostgreSQL. Daily backups run on
# the VPS via `pg-backup.timer` -> /usr/local/bin/pg-backup.sh (all Postgres
# dbs -> Azure Blob, GFS rotation) — already installed and verified. For an
# on-demand local Postgres backup use CI/db-backup.sh (pg_dump).
# This SQL Server -> Azure script targets the RETIRED monerica_db and is kept
# only for reference / rollback. Do NOT run it as the live backup path.
# ============================================================================
#
# deploy-backup.sh — install/manage the SQL Server -> Azure Blob backup on the VPS.
# Lives in CI/ alongside deploy.sh and deploy-jobs.sh. Run from your dev machine.
# Sources CI/deploy-config.sh for the SSH target, the SA password, and the Azure
# settings (add the AZ_* lines listed in README-backups.md to deploy-config.sh).
#
#   ./deploy-backup.sh                # install (default) — copy script + units, write env, enable timer
#   ./deploy-backup.sh run-now        # trigger one backup now and tail the log
#   ./deploy-backup.sh status         # timer schedule + last service result
#   ./deploy-backup.sh logs           # last 100 journal lines
#   ./deploy-backup.sh remove         # uninstall units (leaves script + env in place)
#
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck disable=SC1091
source ./deploy-config.sh

: "${SSH_HOST:?set SSH_HOST in deploy-config.sh}"
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

TASK="${1:-install}"

case "$TASK" in
  install)
    : "${MSSQL_SA_PASSWORD:?set in deploy-config.sh}"
    : "${AZ_ACCOUNT:?add to deploy-config.sh}"
    : "${AZ_CONTAINER:?add to deploy-config.sh}"
    : "${AZ_SAS:?add to deploy-config.sh}"

    echo "==> ensuring azcopy is present on the VPS"
    ssh "$SSH_TARGET" 'command -v azcopy >/dev/null 2>&1 || {
        set -e; cd /tmp
        curl -sL https://aka.ms/downloadazcopy-v10-linux -o azcopy.tgz
        tar -xzf azcopy.tgz
        sudo install -m 0755 ./azcopy_linux_*/azcopy /usr/local/bin/azcopy
        rm -rf azcopy.tgz azcopy_linux_*
        echo "installed azcopy $(azcopy --version)"; }'

    echo "==> installing /usr/local/bin/mssql-backup.sh"
    scp -q mssql-backup.sh "$SSH_TARGET":/tmp/mssql-backup.sh
    ssh "$SSH_TARGET" 'sudo install -m 0755 /tmp/mssql-backup.sh /usr/local/bin/mssql-backup.sh && rm -f /tmp/mssql-backup.sh'

    echo "==> writing /etc/mssql-backup/backup.env (600, never touches /tmp)"
    {
      echo "MSSQL_CONTAINER=${MSSQL_CONTAINER:-mssql}"
      echo "MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}"
      echo "SQLCMD=${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}"
      echo "SQLCMD_FLAGS=${SQLCMD_FLAGS:--C}"
      echo "AZ_ACCOUNT=${AZ_ACCOUNT}"
      echo "AZ_CONTAINER=${AZ_CONTAINER}"
      echo "AZ_SAS=${AZ_SAS}"
      echo "WEEKLY_SLOTS=${WEEKLY_SLOTS:-4}"
      echo "MONTHLY_SLOTS=${MONTHLY_SLOTS:-12}"
      echo "INCLUDE_SYSTEM_DBS=${INCLUDE_SYSTEM_DBS:-false}"
      echo "COMPRESSION=${COMPRESSION:-false}"
      echo "VERIFY=${VERIFY:-true}"
    } | ssh "$SSH_TARGET" 'sudo mkdir -p /etc/mssql-backup && sudo tee /etc/mssql-backup/backup.env >/dev/null && sudo chmod 600 /etc/mssql-backup/backup.env && sudo chown root:root /etc/mssql-backup/backup.env'

    echo "==> installing systemd units"
    scp -q mssql-backup.service mssql-backup.timer "$SSH_TARGET":/tmp/
    ssh "$SSH_TARGET" 'sudo mv /tmp/mssql-backup.service /tmp/mssql-backup.timer /etc/systemd/system/ \
        && sudo systemctl daemon-reload \
        && sudo systemctl enable --now mssql-backup.timer'

    echo "==> done. next scheduled run:"
    ssh "$SSH_TARGET" 'systemctl list-timers mssql-backup.timer --no-pager'
    echo "   test it now with: ./deploy-backup.sh run-now"
    ;;

  run-now)
    ssh "$SSH_TARGET" 'sudo systemctl start mssql-backup.service; journalctl -u mssql-backup.service -n 80 --no-pager'
    ;;

  status)
    ssh "$SSH_TARGET" 'systemctl list-timers mssql-backup.timer --all --no-pager; echo; systemctl status mssql-backup.service --no-pager || true'
    ;;

  logs)
    ssh "$SSH_TARGET" 'journalctl -u mssql-backup.service -n 100 --no-pager'
    ;;

  remove)
    ssh "$SSH_TARGET" 'sudo systemctl disable --now mssql-backup.timer 2>/dev/null || true
        sudo rm -f /etc/systemd/system/mssql-backup.service /etc/systemd/system/mssql-backup.timer
        sudo systemctl daemon-reload
        echo "removed units (left /usr/local/bin/mssql-backup.sh and /etc/mssql-backup/backup.env in place)"'
    ;;

  *)
    echo "usage: $0 [install|run-now|status|logs|remove]"; exit 1 ;;
esac
