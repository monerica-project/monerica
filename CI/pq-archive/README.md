# pq-archive — PriceQuotes disk-retention job

Keeps the MoneroPriceNow price DB (`cryptopricenow`) from growing without bound.

Raw per-exchange quotes live in `cryptopricenow.PriceQuotes`, which is **monthly
RANGE-partitioned** on `TimestampUtc`. The charts' aggregate/history views read the
rolled-up `PriceBuckets` table (kept forever), so raw quotes older than the
retention window are cold. This job, each month, for every `PriceQuotes_YYYY_MM`
partition whose month is **entirely older than the retention window**:

1. `pg_dump -Fc` that one partition (compressed, restorable),
2. verifies it (`pg_restore --list`),
3. uploads it to Bunny.net storage, then
4. **only after a verified upload**, `DROP`s the partition (instant disk reclaim).

Nothing is ever dropped before its archive is safely in Bunny.

## Retention window

Default **90 days**, set in `pq-archive.service` as `Environment=PQ_RETENTION_DAYS=90`.

- To change the scheduled window: edit that number, re-install the unit (below), and
  `sudo systemctl daemon-reload`.
- One-off with a different window: `sudo PQ_RETENTION_DAYS=180 /usr/local/bin/pq-archive.sh`
- Preview without changing anything: `sudo PQ_DRY_RUN=1 /usr/local/bin/pq-archive.sh`

> Note: per-exchange charts read raw `PriceQuotes`. If a per-exchange chart window
> exceeds the retention window, dropped months show empty there (data is still in
> Bunny). Raise `PQ_RETENTION_DAYS` if that matters.

## Secrets

The script contains **no secrets**. Bunny credentials and the PG superuser come at
runtime from `/etc/pg-backup/backup.env` (root-only, not in source control — shared
with the `pg-backup` job).

## Install on the server (monerica-vps)

```sh
sudo install -m 0755 pq-archive.sh      /usr/local/bin/pq-archive.sh
sudo install -m 0644 pq-archive.service /etc/systemd/system/pq-archive.service
sudo install -m 0644 pq-archive.timer   /etc/systemd/system/pq-archive.timer
sudo systemctl daemon-reload
sudo systemctl enable --now pq-archive.timer
```

Observe: `systemctl list-timers pq-archive.timer`, `journalctl -u pq-archive.service`.

## Restore an archived month

Archives land at `<zone>/postgres/pricequotes-archive/cryptopricenow/PriceQuotes_YYYY_MM.dump`.
Download it from Bunny and restore into the DB:

```sh
pg_restore -d cryptopricenow --no-owner PriceQuotes_YYYY_MM.dump
```

This recreates the month as a standalone table; re-attach it as a partition if you
want it back under `PriceQuotes`.
