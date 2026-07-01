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
# mssql-backup.sh
# ----------------------------------------------------------------------------
# Backs up every user database in the local SQL Server Docker container and
# pushes each one to Azure Blob Storage using a bounded grandfather-father-son
# (GFS) rotation. Storage never grows: each backup overwrites a deterministic
# slot keyed on weekday / ISO week / month, so old copies are recycled in
# place rather than accumulating.
#
# The backup is a pure copy of the database (COPY_ONLY) — it does not touch the
# differential/log backup chain or modify the database in any way. Every backup
# is integrity-checked (CHECKSUM) and verified (RESTORE VERIFYONLY) before it is
# uploaded.
#
# Runs ON the VPS, invoked by mssql-backup.timer. All config comes from
# /etc/mssql-backup/backup.env (root:root, chmod 600).
#
#   blob layout (per database):
#     <container>/<DB>/daily/<DB>.<Mon|Tue|...|Sun>.bak    (7 slots)
#     <container>/<DB>/weekly/<DB>.wNN.bak                 (WEEKLY_SLOTS slots)
#     <container>/<DB>/monthly/<DB>.mNN.bak                (MONTHLY_SLOTS slots)
# ----------------------------------------------------------------------------
set -euo pipefail

# ---- config (provided by the systemd EnvironmentFile) ----------------------
: "${MSSQL_CONTAINER:=mssql}"
: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD not set}"
: "${SQLCMD:=/opt/mssql-tools18/bin/sqlcmd}"
: "${SQLCMD_FLAGS:=-C}"                 # -C = trust server cert (2022 image / tools18)
: "${AZ_ACCOUNT:?AZ_ACCOUNT not set}"
: "${AZ_CONTAINER:?AZ_CONTAINER not set}"
: "${AZ_SAS:?AZ_SAS not set}"           # container SAS, must start with '?', perms racwl
: "${WEEKLY_SLOTS:=4}"
: "${MONTHLY_SLOTS:=12}"
: "${STAGING_DIR:=/var/backups/mssql/staging}"
: "${CONTAINER_BACKUP_DIR:=/var/opt/mssql/backups}"
: "${INCLUDE_SYSTEM_DBS:=false}"
: "${COMPRESSION:=false}"               # SQL Server Express does NOT support this
: "${VERIFY:=true}"

AZ_HOST="https://${AZ_ACCOUNT}.blob.core.windows.net"

log()  { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die()  { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] ERROR: $*" >&2; exit 1; }

# password is exported into the environment and forwarded into the container by
# NAME only (docker exec -e SQLCMDPASSWORD) so it never appears in any argv.
export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"

sqlc() {
  docker exec -e SQLCMDPASSWORD "$MSSQL_CONTAINER" \
    "$SQLCMD" -S localhost -U sa $SQLCMD_FLAGS -b "$@"
}

upload() {  # local-file  blob-path
  azcopy copy "$1" "${AZ_HOST}/${AZ_CONTAINER}/${2}${AZ_SAS}" \
    --overwrite=true --log-level=ERROR
}

cleanup() { rm -f "${STAGING_DIR:?}"/*.bak 2>/dev/null || true; }
trap cleanup EXIT

# ---- preflight -------------------------------------------------------------
command -v docker  >/dev/null 2>&1 || die "docker not found on PATH"
command -v azcopy  >/dev/null 2>&1 || die "azcopy not found on PATH (install: https://aka.ms/downloadazcopy-v10-linux)"
docker exec "$MSSQL_CONTAINER" test -x "$SQLCMD" 2>/dev/null \
  || die "sqlcmd not found at '$SQLCMD' inside container '$MSSQL_CONTAINER'. Set SQLCMD in backup.env to the path your CI/db-rotate-credentials.sh uses."

mkdir -p "$STAGING_DIR" && chmod 700 "$STAGING_DIR"
docker exec "$MSSQL_CONTAINER" mkdir -p "$CONTAINER_BACKUP_DIR" 2>/dev/null || true

# ---- rotation slots (UTC, to match the project's UTC timers) ---------------
DOW=$(LC_ALL=C date -u +%a)                                  # Mon..Sun
WEEK=$(date -u +%V);  WEEK=$((10#$WEEK))                     # ISO week 1..53
MONTH=$(date -u +%m); MONTH=$((10#$MONTH))                   # 1..12
WEEK_SLOT=$(printf '%02d' $(( (WEEK  - 1) % WEEKLY_SLOTS  )))
MONTH_SLOT=$(printf '%02d' $(( (MONTH - 1) % MONTHLY_SLOTS )))

# ---- enumerate databases ---------------------------------------------------
if [[ "$INCLUDE_SYSTEM_DBS" == "true" ]]; then
  DB_FILTER="database_id <> 2"          # everything except tempdb (cannot be backed up)
else
  DB_FILTER="database_id > 4"           # user databases only
fi

mapfile -t DBS < <(
  sqlc -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE ${DB_FILTER} AND state = 0;" \
  | sed '/^$/d'
)
[[ ${#DBS[@]} -gt 0 ]] || die "no databases found to back up"
log "databases: ${DBS[*]}"
log "slots -> daily/${DOW}  weekly/w${WEEK_SLOT}  monthly/m${MONTH_SLOT}"

WITH_OPTS="COPY_ONLY, INIT, FORMAT, CHECKSUM, STATS=5"
[[ "$COMPRESSION" == "true" ]] && WITH_OPTS="$WITH_OPTS, COMPRESSION"

# ---- back up each database -------------------------------------------------
rc=0
for db in "${DBS[@]}"; do
  safe=$(echo "$db" | tr -c 'A-Za-z0-9._-' '_')      # blob/path-safe name
  cbak="${CONTAINER_BACKUP_DIR}/${safe}.bak"          # scratch path inside container
  lbak="${STAGING_DIR}/${safe}.bak"                   # staged copy on the host

  log "[$db] BACKUP DATABASE (COPY_ONLY)…"
  if ! sqlc -Q "BACKUP DATABASE [$db] TO DISK=N'${cbak}' WITH ${WITH_OPTS};"; then
    log "[$db] BACKUP FAILED — skipping"; rc=1; continue
  fi

  if [[ "$VERIFY" == "true" ]]; then
    log "[$db] RESTORE VERIFYONLY…"
    if ! sqlc -Q "RESTORE VERIFYONLY FROM DISK=N'${cbak}' WITH CHECKSUM;"; then
      log "[$db] VERIFY FAILED — not uploading"; rc=1
      docker exec "$MSSQL_CONTAINER" rm -f "$cbak" || true
      continue
    fi
  fi

  if ! docker cp "${MSSQL_CONTAINER}:${cbak}" "$lbak"; then
    log "[$db] docker cp failed"; rc=1
    docker exec "$MSSQL_CONTAINER" rm -f "$cbak" || true
    continue
  fi
  docker exec "$MSSQL_CONTAINER" rm -f "$cbak" || true

  log "[$db] uploading $(du -h "$lbak" | cut -f1) to all three tiers…"
  if upload "$lbak" "${safe}/daily/${safe}.${DOW}.bak"   \
  && upload "$lbak" "${safe}/weekly/${safe}.w${WEEK_SLOT}.bak"  \
  && upload "$lbak" "${safe}/monthly/${safe}.m${MONTH_SLOT}.bak"; then
    log "[$db] uploaded"
  else
    log "[$db] one or more uploads FAILED"; rc=1
  fi
  rm -f "$lbak"
done

if [[ $rc -eq 0 ]]; then log "all backups completed"; else log "completed WITH ERRORS"; fi
exit $rc
