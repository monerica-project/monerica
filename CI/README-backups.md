# Database backups → Azure Blob Storage

Daily, automated, verified backups of **every user database** in the `mssql`
Docker container, pushed to a **private Azure Blob Storage container** with a
bounded grandfather-father-son (GFS) rotation. Storage never grows: each backup
overwrites a fixed slot keyed on weekday / week / month.

Files (drop the first four in `CI/`, the script + units get pushed to the VPS by
`deploy-backup.sh`):

| File                  | Where it lives                                  |
| --------------------- | ----------------------------------------------- |
| `deploy-backup.sh`    | `CI/` — run from your dev machine over SSH       |
| `mssql-backup.sh`     | VPS `/usr/local/bin/` (installed by deploy)      |
| `mssql-backup.service`| VPS `/etc/systemd/system/` (installed by deploy) |
| `mssql-backup.timer`  | VPS `/etc/systemd/system/` (installed by deploy) |
| `backup.env.example`  | reference only — real env is written to the VPS  |

---

## Retention model

Every daily run backs up each database once and writes that single `.bak` to its
current daily, weekly, and monthly slots. Storage per database is fixed at
`7 + WEEKLY_SLOTS + MONTHLY_SLOTS` blobs forever.

```
db-backups/                          (private container)
  <DB>/daily/<DB>.Mon.bak  ... Sun.bak        7 slots, overwrites weekly
  <DB>/weekly/<DB>.w00.bak ... w03.bak        WEEKLY_SLOTS slots (default 4)
  <DB>/monthly/<DB>.m00.bak ... m11.bak       MONTHLY_SLOTS slots (default 12)
```

- **daily slot** = current weekday → always the last 7 days.
- **weekly slot** = `(ISO_week − 1) mod WEEKLY_SLOTS` → always one ~1 week old.
- **monthly slot** = `(month − 1) mod MONTHLY_SLOTS` → always one ~1 month old.

When a period rolls over, the previous slot keeps its last backup until that
index comes around again. To match "one weekly + one monthly" literally, set
`WEEKLY_SLOTS=1` and `MONTHLY_SLOTS=1`.

The backup uses `COPY_ONLY` (does not disturb the backup chain or alter the DB),
`CHECKSUM`, and a `RESTORE VERIFYONLY` pass before upload. **No Azure lifecycle
rule is needed** — slot reuse is the rotation.

---

## One-time Azure setup

A `.bak` is a file, so it goes in a **Blob container**, not a Storage *Table*.
Use a **private** container (no public access). Run from your dev machine:

```bash
ACCT=yourstorageacct
KEY=$(az storage account keys list -n "$ACCT" --query '[0].value' -o tsv)

# private container (no anonymous access)
az storage container create \
  --account-name "$ACCT" --account-key "$KEY" \
  --name db-backups --public-access off

# container SAS: read, add, create, write, list (NO delete — slots are
# overwritten in place). Set an expiry you will rotate.
az storage container generate-sas \
  --account-name "$ACCT" --account-key "$KEY" \
  --name db-backups --permissions racwl \
  --expiry 2027-06-30T00:00:00Z --https-only -o tsv
```

The command prints a token like `sv=...&sp=racwl&se=...&sig=...`. In
`deploy-config.sh`, set `AZ_SAS` to that token **with a leading `?`**.

For revocable credentials, create a stored access policy on the container and
bind the SAS to it — then you can kill the SAS without rotating the account key.

---

## Add to `CI/deploy-config.sh` (gitignored)

```bash
# Azure Blob backup target
AZ_ACCOUNT='yourstorageacct'
AZ_CONTAINER='db-backups'
AZ_SAS='?sv=2024-11-04&sp=racwl&se=2027-06-30T00:00:00Z&sig=...'

# optional overrides (defaults shown)
# WEEKLY_SLOTS=4
# MONTHLY_SLOTS=12
# INCLUDE_SYSTEM_DBS=false
# COMPRESSION=false        # keep false on Express
# SQLCMD=/opt/mssql-tools18/bin/sqlcmd
```

`MSSQL_SA_PASSWORD`, `SSH_HOST`/`SSH_USER`, and `MSSQL_CONTAINER` are reused from
your existing config.

---

## Install & operate

```bash
cd CI
./deploy-backup.sh            # installs azcopy if missing, copies script + units,
                              # writes /etc/mssql-backup/backup.env (600), enables timer
./deploy-backup.sh run-now    # trigger a backup immediately and tail the log
./deploy-backup.sh status     # next run + last result
./deploy-backup.sh logs       # journal output
./deploy-backup.sh remove     # uninstall the timer
```

Direct on the VPS:

```bash
systemctl list-timers mssql-backup.timer --no-pager
journalctl -u mssql-backup.service --since "24 hours ago" --no-pager
```

The first run-now should show each DB going through `BACKUP … (COPY_ONLY)`,
`RESTORE VERIFYONLY`, and three uploads. Confirm the blobs landed:

```bash
az storage blob list --account-name "$ACCT" --account-key "$KEY" \
  -c db-backups --query "[].name" -o tsv | sort
```

---

## Restoring

```bash
ACCT=yourstorageacct
SAS='?sv=...&sig=...'
DB=YourDatabase

# 1. pull a slot down (here: Monday's daily)
azcopy copy \
  "https://$ACCT.blob.core.windows.net/db-backups/$DB/daily/$DB.Mon.bak$SAS" \
  ./$DB.bak

# 2. push it into the container and restore (over the existing DB)
docker cp ./$DB.bak mssql:/var/opt/mssql/backups/$DB.restore.bak
docker exec -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" mssql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q \
  "ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   RESTORE DATABASE [$DB] FROM DISK=N'/var/opt/mssql/backups/$DB.restore.bak'
     WITH REPLACE, RECOVERY;
   ALTER DATABASE [$DB] SET MULTI_USER;"
```

To restore under a **new** name, add `WITH MOVE` clauses pointing the data/log
files to new paths (get the logical names from
`RESTORE FILELISTONLY FROM DISK=N'…'`).

---

## Notes / caveats

- **Express edition**: no SQL Agent (so the schedule lives in systemd) and no
  backup compression — `COMPRESSION` must stay `false`. Express also caps each
  DB at 10 GB, so backups stay small.
- **Secrets**: the SA password is forwarded into the container by env-var name
  only (never in any argv), and `backup.env` is root-owned `600`. The SAS does
  appear in the azcopy URL argument during upload — fine on a single-admin box,
  but prefer a stored-access-policy SAS so it is revocable, and rotate it before
  the `se=` expiry.
- **`sqlcmd` path**: if the container uses a different path, set `SQLCMD` to
  match what `CI/db-rotate-credentials.sh` already uses.
- This timer is named `mssql-backup` (not `dm-job-*`) because it is infra, not a
  DirectoryManager .NET job — it won't show up under `systemctl list-timers 'dm-job-*'`.
