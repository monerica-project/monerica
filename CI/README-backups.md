# Database backups

monerica runs on **PostgreSQL**. Backups are automated on the VPS and pushed to
Bunny.net storage. (The old SQL Server backup was retired on 2026-07-01 when
monerica moved off MSSQL, and the upload target later moved from Azure Blob to
Bunny.net.)

## Daily automated backup (Postgres → Bunny.net)

A systemd timer on the VPS dumps **every** Postgres database and uploads the dumps
to a Bunny.net storage zone. It keeps the newest 7 daily and 8 weekly dumps per
database. Each dump is integrity-checked (`pg_restore --list`) before upload.

| Piece            | Where |
|------------------|-------|
| `pg-backup.timer` / `pg-backup.service` | `/etc/systemd/system/` (daily **03:45 UTC**) |
| `pg-backup.sh`   | `/usr/local/bin/` |
| `backup.env`     | `/etc/pg-backup/backup.env` (root only — storage zone, access key, retention) |

It is **not** installed from this repo — it was provisioned once on the server and
covers all databases on this shared Postgres instance, not only monerica's.

```bash
# check it
ssh <vps> 'systemctl list-timers pg-backup.timer --no-pager'
ssh <vps> 'journalctl -u pg-backup.service --since "24 hours ago" --no-pager'
# run now
ssh <vps> 'sudo systemctl start pg-backup.service'
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
