#!/usr/bin/env bash
# db-sync-local.sh — Pull latest prod DB and restore to local Docker SQL Server.
#
# Usage:
#   ./db-sync-local.sh                       # fresh backup from prod, restore local
#   ./db-sync-local.sh path/to/file.bak      # skip backup, use existing .bak
#
# Reads prod connection from deploy-config.sh.
# Local target defaults match your connection string:
#   Server=127.0.0.1,1433;Database=monerica_db;User Id=sa;Password=YourStrong!Pass1
#
# Override locals via env if needed:
#   LOCAL_CONTAINER=mssql LOCAL_SA_PASSWORD='YourStrong!Pass1' LOCAL_DB=monerica_db ./db-sync-local.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/deploy-config.sh"

LOCAL_CONTAINER="${LOCAL_CONTAINER:-mssql}"
LOCAL_SA_PASSWORD="${LOCAL_SA_PASSWORD:-YourStrong!Pass1}"
LOCAL_DB="${LOCAL_DB:-${DB_NAME:-monerica_db}}"
BACKUP_DIR="${BACKUP_DIR:-$HOME/backups/monerica}"

ARG_BAK="${1:-}"

C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_YELLOW=$'\e[33m' C_RED=$'\e[31m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
say()  { echo "${C_CYAN}==> $1${C_RESET}"; }
ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
err()  { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

# 1. Get a .bak (fresh from prod, or supplied)
if [[ -n "$ARG_BAK" ]]; then
    [[ -f "$ARG_BAK" ]] || { err "File not found: $ARG_BAK"; exit 1; }
    BAK="$ARG_BAK"
    say "Using existing backup: $BAK"
else
    say "Pulling fresh backup from $SSH_HOST..."
    "$SCRIPT_DIR/db-backup.sh" "$BACKUP_DIR"
    BAK=$(ls -t "$BACKUP_DIR"/${DB_NAME:-monerica_db}_*.bak 2>/dev/null | head -1)
    [[ -n "$BAK" ]] || { err "Couldn't locate downloaded .bak in $BACKUP_DIR"; exit 1; }
    ok "Downloaded: $BAK"
fi

# 2. Verify local container is running
docker inspect "$LOCAL_CONTAINER" >/dev/null 2>&1 \
    || { err "Local container '$LOCAL_CONTAINER' not found. Set LOCAL_CONTAINER or start it."; exit 1; }
[[ "$(docker inspect -f '{{.State.Running}}' "$LOCAL_CONTAINER")" == "true" ]] \
    || { err "Local container '$LOCAL_CONTAINER' isn't running."; exit 1; }

# 3. Copy .bak into the container
say "Copying .bak into '$LOCAL_CONTAINER'..."
docker exec -u root "$LOCAL_CONTAINER" mkdir -p /var/opt/mssql/backup
docker cp "$BAK" "$LOCAL_CONTAINER:/var/opt/mssql/backup/refresh.bak"
docker exec -u root "$LOCAL_CONTAINER" chown mssql:mssql /var/opt/mssql/backup/refresh.bak || true
ok "Copied"

# 4. Build restore SQL — auto-detects logical file names via FILELISTONLY
say "Restoring $LOCAL_DB..."
restore_sql="$(mktemp /tmp/dm-local-restore-XXXXXX.sql)"
trap 'rm -f "$restore_sql"' EXIT

cat > "$restore_sql" <<EOF
SET NOCOUNT ON;
USE [master];
GO

IF DB_ID(N'$LOCAL_DB') IS NOT NULL
BEGIN
    PRINT 'Existing database found — kicking connections';
    ALTER DATABASE [$LOCAL_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
GO

DECLARE @data sysname, @log sysname;
DECLARE @files TABLE (
    LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1),
    FileGroupName nvarchar(128) NULL, Size numeric(20,0), MaxSize numeric(20,0),
    FileId int, CreateLSN numeric(25,0), DropLSN numeric(25,0) NULL,
    UniqueId uniqueidentifier, ReadOnlyLSN numeric(25,0) NULL, ReadWriteLSN numeric(25,0) NULL,
    BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int,
    LogGroupGUID uniqueidentifier NULL, DifferentialBaseLSN numeric(25,0) NULL,
    DifferentialBaseGUID uniqueidentifier NULL,
    IsReadOnly bit, IsPresent bit, TDEThumbprint varbinary(32) NULL, SnapshotURL nvarchar(360) NULL
);
INSERT INTO @files
EXEC('RESTORE FILELISTONLY FROM DISK = ''/var/opt/mssql/backup/refresh.bak''');

SELECT TOP 1 @data = LogicalName FROM @files WHERE Type = 'D';
SELECT TOP 1 @log  = LogicalName FROM @files WHERE Type = 'L';

PRINT 'Data file: ' + @data;
PRINT 'Log file:  ' + @log;

DECLARE @sql nvarchar(max) = N'
RESTORE DATABASE [$LOCAL_DB]
  FROM DISK = ''/var/opt/mssql/backup/refresh.bak''
  WITH MOVE ''' + @data + ''' TO ''/var/opt/mssql/data/$LOCAL_DB.mdf'',
       MOVE ''' + @log  + ''' TO ''/var/opt/mssql/data/${LOCAL_DB}_log.ldf'',
       REPLACE, STATS = 10';
EXEC(@sql);
GO

ALTER DATABASE [$LOCAL_DB] SET MULTI_USER;
GO

PRINT 'Local DB refresh complete.';
EOF

docker cp "$restore_sql" "$LOCAL_CONTAINER:/tmp/dm-local-restore.sql"
docker exec -u root "$LOCAL_CONTAINER" chmod 644 /tmp/dm-local-restore.sql
docker exec "$LOCAL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$LOCAL_SA_PASSWORD" -C -b -i /tmp/dm-local-restore.sql
ok "Restored"

# 5. Cleanup
docker exec -u root "$LOCAL_CONTAINER" rm -f /var/opt/mssql/backup/refresh.bak /tmp/dm-local-restore.sql

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Local DB sync complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo "  ${C_GRAY}From:${C_RESET}  $BAK"
echo "  ${C_GRAY}To:${C_RESET}    $LOCAL_DB @ 127.0.0.1,1433 (sa)"
echo
