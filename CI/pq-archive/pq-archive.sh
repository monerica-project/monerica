#!/usr/bin/env bash
#
# pq-archive.sh — archive + drop old MoneroPriceNow PriceQuotes partitions.
# ----------------------------------------------------------------------------
# Raw per-exchange price quotes (cryptopricenow.PriceQuotes) are monthly RANGE
# partitions. The charts run off the rolled-up PriceBuckets (kept forever), so
# raw quotes older than the retention window are cold. This job, for each
# monthly partition entirely older than PQ_RETENTION_DAYS:
#
#   1. pg_dump -Fc that one partition  (compressed, restorable)
#   2. verify (pg_restore --list)
#   3. upload to Bunny.net storage  <prefix>/pricequotes-archive/<db>/<part>.dump
#   4. ONLY after a verified upload -> DROP the partition (instant reclaim)
#
# Config is shared with pg-backup: /etc/pg-backup/backup.env.
# Env knobs: PQ_RETENTION_DAYS (default 90), PQ_DRY_RUN=1 (report only).
# Invoked by pq-archive.timer (monthly). Runs as root (uses runuser + the
# root-only backup.env).
# ----------------------------------------------------------------------------
set -euo pipefail

ENV_FILE=/etc/pg-backup/backup.env
if [[ -f "$ENV_FILE" ]]; then set -a; . "$ENV_FILE"; set +a; fi

: "${PG_SUPERUSER:=postgres}"
: "${BUNNY_HOST:=storage.bunnycdn.com}"
: "${BUNNY_ZONE:?BUNNY_ZONE not set}"
: "${BUNNY_KEY:?BUNNY_KEY not set}"
: "${BUNNY_PREFIX:=postgres}"

DB="${PQ_DB:=cryptopricenow}"
PARENT="${PQ_PARENT:=PriceQuotes}"
RETENTION_DAYS="${PQ_RETENTION_DAYS:=90}"
ARCHIVE_SUBDIR="${PQ_ARCHIVE_SUBDIR:=pricequotes-archive}"
STAGING="${PQ_STAGING_DIR:=/var/backups/postgres/pq-archive}"
DRY_RUN="${PQ_DRY_RUN:=0}"
BASE="https://${BUNNY_HOST}/${BUNNY_ZONE}/${BUNNY_PREFIX}"

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] ERROR: $*" >&2; exit 1; }
pg()  { runuser -u "$PG_SUPERUSER" -- "$@"; }

bput() {  # local-file  rel-path
  local code
  code=$(curl -sS --retry 3 --retry-delay 4 -X PUT \
           -H "AccessKey: ${BUNNY_KEY}" --data-binary @"$1" \
           -o /dev/null -w '%{http_code}' "${BASE}/$2")
  [[ "$code" == 201 || "$code" == 200 ]] || { log "  PUT $2 -> HTTP $code"; return 1; }
}

command -v curl       >/dev/null 2>&1 || die "curl not found"
command -v pg_dump    >/dev/null 2>&1 || die "pg_dump not found"
command -v pg_restore >/dev/null 2>&1 || die "pg_restore not found"
command -v runuser    >/dev/null 2>&1 || die "runuser not found"
mkdir -p "$STAGING" && chmod 700 "$STAGING"

cutoff=$(date -u -d "${RETENTION_DAYS} days ago" +%Y-%m-%d)
log "db=${DB} parent=${PARENT} retention=${RETENTION_DAYS}d cutoff=${cutoff} dry_run=${DRY_RUN}"
log "archive target: ${BASE}/${ARCHIVE_SUBDIR}/${DB}/"

mapfile -t PARTS < <(
  pg psql -d "$DB" -tAc "
    select c.relname
    from pg_inherits i
    join pg_class c on c.oid=i.inhrelid
    join pg_class p on p.oid=i.inhparent
    where p.relname='${PARENT}' and c.relname ~ '^${PARENT}_[0-9]{4}_[0-9]{2}\$'
    order by c.relname;" | sed '/^$/d'
)
[[ ${#PARTS[@]} -gt 0 ]] || { log "no monthly partitions found"; exit 0; }

rc=0
processed=0
for part in "${PARTS[@]}"; do
  ym=${part#${PARENT}_}          # 2026_06
  y=${ym%_*}; m=${ym#*_}
  monthend=$(date -u -d "${y}-${m}-01 +1 month" +%Y-%m-%d)  # exclusive upper bound of the month
  if [[ "$monthend" > "$cutoff" ]]; then
    continue   # not entirely older than cutoff -> keep
  fi

  rows=$(pg psql -d "$DB" -tAc "select count(*) from public.\"${part}\";" | tr -d '[:space:]')
  rows=${rows:-0}
  size=$(pg psql -d "$DB" -tAc "select pg_size_pretty(pg_total_relation_size('public.\"${part}\"'));" | tr -d '[:space:]')

  if [[ "$DRY_RUN" == "1" ]]; then
    log "[DRY] would archive+drop ${part} (ends ${monthend}, ${rows} rows, ${size})"
    processed=$((processed+1))
    continue
  fi

  if [[ "$rows" -gt 0 ]]; then
    dumpfile="${STAGING}/${part}.dump"
    rel="${ARCHIVE_SUBDIR}/${DB}/${part}.dump"
    log "[$part] ${rows} rows (${size}), ends ${monthend} <= ${cutoff}: dumping…"
    if ! pg pg_dump -Fc -d "$DB" -t "public.\"${part}\"" > "$dumpfile"; then
      log "[$part] DUMP FAILED — skipping"; rc=1; rm -f "$dumpfile"; continue
    fi
    if ! pg_restore --list "$dumpfile" >/dev/null 2>&1; then
      log "[$part] VERIFY FAILED — not uploading/dropping"; rc=1; rm -f "$dumpfile"; continue
    fi
    log "[$part] uploading $(du -h "$dumpfile" | cut -f1) -> ${rel}"
    if ! bput "$dumpfile" "$rel"; then
      log "[$part] UPLOAD FAILED — not dropping (data kept)"; rc=1; rm -f "$dumpfile"; continue
    fi
    rm -f "$dumpfile"
  else
    log "[$part] empty, ends ${monthend}: dropping without archive"
  fi

  if pg psql -d "$DB" -c "DROP TABLE public.\"${part}\";" >/dev/null 2>&1; then
    log "[$part] dropped — space reclaimed"
    processed=$((processed+1))
  else
    log "[$part] DROP FAILED"; rc=1
  fi
done

log "pq-archive done: ${processed} partition(s) processed$([[ $rc -ne 0 ]] && echo ' (WITH ERRORS)')"
exit $rc
