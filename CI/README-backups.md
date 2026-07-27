# Database backups

monerica runs on **PostgreSQL**. Backups are automated on the VPS and pushed to
Azure Blob Storage. (The old SQL Server → Azure backup was retired on 2026-07-01
when monerica moved off MSSQL; its scripts were removed — recover from git history
if ever needed for reference.)

## Daily automated backup (Postgres → Azure)

A systemd timer on the VPS dumps **every** Postgres database and uploads to Azure
Blob with GFS rotation. Each dump is integrity-checked (`pg_restore --list`) before
upload.

| Piece            | Where |
|------------------|-------|
| `pg-backup.timer` / `pg-backup.service` | `/etc/systemd/system/` (daily **03:45 UTC**) |
| `pg-backup.sh`   | `/usr/local/bin/` |
| `backup.env`     | `/etc/pg-backup/backup.env` (root:root, 600 — Azure SAS + account) |
| Azure target     | account `monericaeastus`, container `db-backups`, prefix `postgres` |

It is **not** installed from this repo — it was provisioned once via
`SwapRaven/CI/setup-pg-backup.sh` and covers all databases on this shared
Postgres instance (monerica, cryptopricenow, swapraven, …).

```bash
# check it
ssh monerica-vps 'systemctl list-timers pg-backup.timer --no-pager'
ssh monerica-vps 'journalctl -u pg-backup.service --since "24 hours ago" --no-pager'
# run now
ssh monerica-vps 'sudo systemctl start pg-backup.service'
```

## On-demand local backup

`db-backup.sh` runs `pg_dump` of the monerica DB on the VPS and downloads it:

```bash
./db-backup.sh                          # → ~/backups/monerica/<db>_<ts>.dump
./db-backup.sh ~/Documents/db-backups   # custom destination
```

Restore a `.dump` (custom format) with `pg_restore`:

```bash
pg_restore --clean --if-exists -d "<conn-string>" <file>.dump
```
