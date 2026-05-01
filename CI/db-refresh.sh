#!/usr/bin/env bash
# db-refresh.sh — Refresh the production SQL Server DB on the Linux server
# from a Windows .bak file.
#
# Usage:
#   ./db-refresh.sh path/to/monerica.bak                   # default: stop dm-web during restore
#   ./db-refresh.sh path/to/monerica.bak dm-web,dm-api     # also stop these
#
# How to produce the .bak on the Windows source server:
#
#   sqlcmd -S localhost -E -Q "BACKUP DATABASE [monerica] TO DISK='C:\backups\monerica.bak' WITH FORMAT, INIT, COMPRESSION"
#
# or in SSMS:
#   Right-click DB → Tasks → Back Up... → Full → Disk
#   Use "WITH COPY_ONLY" if you don't want to break an existing backup chain.
#
# Then SCP it to your workstation, then point this script at it.
#
# This script:
#   1. Uploads the .bak to the server
#   2. Copies it into the mssql container's backup volume
#   3. Stops the named services (so no concurrent writes)
#   4. RESTORE FILELISTONLY to detect logical file names inside the .bak
#   5. RESTORE DATABASE WITH MOVE clauses pointing to /var/opt/mssql/data/
#   6. Re-grants the app user db_owner (the .bak resets users)
#   7. Restarts the services

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/deploy-config.sh"

BAK_FILE="${1:-}"
SERVICES_CSV="${2:-dm-web}"

[[ -z "$BAK_FILE" ]] && { echo "Usage: $0 path/to/monerica.bak [service1,service2,...]" >&2; exit 1; }
[[ -f "$BAK_FILE" ]] || { echo "File not found: $BAK_FILE" >&2; exit 1; }

C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_YELLOW=$'\e[33m' C_RED=$'\e[31m' C_RESET=$'\e[0m'
say()   { echo "${C_CYAN}==> $1${C_RESET}"; }
ok()    { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
warn()  { echo "    ${C_YELLOW}WARN: $1${C_RESET}"; }
err()   { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { err "sshpass missing"; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10)
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"
ssh_run()  { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "$1"; }
scp_send() { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" "$1" "$SSH_TARGET:$2"; }

echo
echo "${C_YELLOW}This will REPLACE database '$DB_NAME' on $SSH_HOST.${C_RESET}"
echo "Source:   $BAK_FILE  ($(du -h "$BAK_FILE" | cut -f1))"
echo "Services: $SERVICES_CSV (will be stopped/started)"
read -rp "Type the database name to continue: " confirm
[[ "$confirm" == "$DB_NAME" ]] || { err "Confirmation didn't match. Aborting."; exit 1; }

# 1. Upload to host, then copy into container
say "Uploading .bak..."
remote_bak="/tmp/$(basename "$BAK_FILE")"
scp_send "$BAK_FILE" "$remote_bak"

say "Copying into mssql container..."
ssh_run "docker exec mssql mkdir -p /var/opt/mssql/backup"
ssh_run "docker cp $remote_bak mssql:/var/opt/mssql/backup/refresh.bak"
ssh_run "docker exec mssql chown mssql:mssql /var/opt/mssql/backup/refresh.bak || true"
ok "Uploaded"

# 2. Stop services
IFS=',' read -ra SVCS <<< "$SERVICES_CSV"
for s in "${SVCS[@]}"; do
    say "Stopping $s..."
    ssh_run "systemctl stop $s 2>/dev/null || true"
done

# 3. Restore via T-SQL — single self-contained script.
#    Reads logical file names from the .bak so MOVE clauses don't have to be hardcoded.
say "Restoring database $DB_NAME..."
restore_sql="/tmp/dm-restore.sql.local"
cat > "$restore_sql" <<EOF
SET NOCOUNT ON;
USE [master];
GO

IF DB_ID(N'$DB_NAME') IS NOT NULL
BEGIN
    PRINT 'Existing database found — kicking connections';
    ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
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
RESTORE DATABASE [$DB_NAME]
  FROM DISK = ''/var/opt/mssql/backup/refresh.bak''
  WITH MOVE ''' + @data + ''' TO ''/var/opt/mssql/data/$DB_NAME.mdf'',
       MOVE ''' + @log  + ''' TO ''/var/opt/mssql/data/${DB_NAME}_log.ldf'',
       REPLACE, STATS = 10';
EXEC(@sql);
GO

-- The .bak resets users; rebind the app user to its login.
USE [$DB_NAME];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '$DB_USER')
    CREATE LOGIN [$DB_USER] WITH PASSWORD = '$DB_PASSWORD', CHECK_POLICY = OFF;
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$DB_USER')
    DROP USER [$DB_USER];
GO

CREATE USER [$DB_USER] FOR LOGIN [$DB_USER];
ALTER ROLE db_owner ADD MEMBER [$DB_USER];
GO

PRINT 'DB refresh complete.';
EOF

scp_send "$restore_sql" "/tmp/dm-restore.sql"
ssh_run "docker cp /tmp/dm-restore.sql mssql:/tmp/dm-restore.sql"
ssh_run "docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -i /tmp/dm-restore.sql"
ok "Database refreshed"

# 4. Cleanup
ssh_run "docker exec mssql rm -f /var/opt/mssql/backup/refresh.bak /tmp/dm-restore.sql"
ssh_run "rm -f $remote_bak /tmp/dm-restore.sql"

# 5. Restart services
for s in "${SVCS[@]}"; do
    say "Starting $s..."
    ssh_run "systemctl start $s 2>/dev/null || true"
done

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} DB refresh complete${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
