# Monerica

A Directory For A Monero Circular Economy

[Submit your link to Monerica here!](https://app.monerica.com/submit)

---

### Donations

You can donate to help with hosting and admin costs by sending Monero to: `8BciMHULmH1Gu6Jrwv1vEJB11f3sNNSGRVU2PKMuaMJ48wndUuNDbaZ94ijH7H3fKQbhkUA5k98ZHUB3cmuoJjwV7WcT3dC`

---

### About

Monerica is a directory of websites and services that accept Monero as payment or relate to Monero in some way. It is a community project that is open to contributions from anyone. The goal is to create a directory of websites and services facilitating a circular economy for Monero.

### Contact

Email: `admin@monerica.com`

### Contributing

The best way to contribute to Monerica is to help manage the data in the directory. This can be done by [submitting new links](https://app.monerica.com/submit) or [editing existing links](https://app.monerica.com/submission/findexisting).

If you would like to contribute to the codebase, please fork the repository and submit a pull request.

The Monerica website is built using C# and ASP.NET Core MVC. It runs on Windows, Linux, or macOS — production runs on Linux. The database is SQL Server and the code uses Entity Framework Core.

### Features

DirectoryManager is capable of:

- Adding, editing, deleting, and viewing links in the directory
- Auto-creating categories and sub-categories when adding new links
- Auditing the links
- Sponsored listing management with BTCPay Server and NOWPayments XMR processors
- Email reminders for expiring sponsored listings
- Newsletter campaigns
- Periodic site-uptime checking via Tor

---

## Application Structure

The solution is one web application plus a set of background console apps that share a data layer.

#### Web

- **DirectoryManager.Web** — main web application (controllers and views).

#### Background Processes

- **DirectoryManager.Console** — local one-off tasks and imports.
- **DirectoryManager.EmailMessageMaker** — generates outbound email messages.
- **DirectoryManager.NewsletterSender** — sends queued newsletter emails.
- **DirectoryManager.SiteChecker** — checks whether existing listings are online.
- **DirectoryManager.SponsoredListingOpening** — notifies subscribers when slots open.
- **DirectoryManager.SponsoredListingReminder** — emails sponsors about expiring listings.

#### Data

- **DirectoryManager.Data** — EF Core `ApplicationDbContext`, repositories, migrations.

---

## Local Development

### Application Settings

Configuration lives in `appsettings.json` per project. For local dev, override values in `appsettings.Development.json`.

To set up a project for the first time:

1. Find the `appsettings.template.json` in the project you want to run.
2. Copy it to `appsettings.json` in the same directory.
3. Fill in real values (connection string, etc.).

You will need a SQL Server instance reachable from your dev machine. The simplest option is to run SQL Server in Docker locally with the same `mssql` image used in production (see `CI/deploy.sh`).

### Database Migrations

The schema is managed by Entity Framework Core. From the `DirectoryManager.Web` directory:

Create a new migration:

```bash
dotnet ef migrations add MigrationName \
    --context ApplicationDbContext \
    --project DirectoryManager.Data/DirectoryManager.Data.csproj
```

Apply pending migrations:

```bash
dotnet ef database update \
    --context ApplicationDbContext \
    --project DirectoryManager.Data/DirectoryManager.Data.csproj
```

Production migrations run automatically as part of `./deploy.sh web` (the `task_migrate_db` step inside the deploy pipeline). Pass `--skip-migrate` to skip them.

---

## Production Operations (Linux VPS)

Production is one Ubuntu VPS running:

- The web app under **systemd** (`monerica.com` unit), reverse-proxied by host nginx
- **SQL Server** in a Docker container named `mssql` (port `127.0.0.1:1433`)
- Five background jobs as systemd timers, each prefixed `dm-job-*`
- Optional Tor hidden service + SOCKS proxy (used by `site-checker`)

All operational scripts live in `CI/` and are run from a **dev machine** (Linux/macOS) over SSH. They source `CI/deploy-config.sh` for credentials and the SSH target.

### CI/ folder

| File | Purpose |
| --- | --- |
| `deploy-config.sh` | Secrets + SSH target. **Gitignored.** |
| `deploy.sh` | Web app deploy, server bootstrap, MSSQL setup, nginx, SSL, Tor, smoke tests |
| `deploy-jobs.sh` | Background-job deploy (timers + services), status, logs, enable/disable/remove |
| `db-rotate-credentials.sh` | Create or rotate a SQL Server login on the VPS |
| `db-verify-deployed-configs.sh` | Audit which DB login each deployed service is using |
| `jobs/<name>.conf` | Per-job schedule + project source path |
| `services/<name>.conf` | Per-web-service deploy config. **Gitignored.** |

### deploy-config.sh

This file is never committed. It defines:

```bash
SSH_HOST=your-vps                # or IP / ~/.ssh/config alias
SSH_USER=                        # blank if alias handles user

DB_CONNECTION_STRING='Server=127.0.0.1,1433;Database=<db_name>;User Id=<app_login>;Password=...;TrustServerCertificate=true;Encrypt=false;'
MSSQL_SA_PASSWORD='...'          # used only by setup/rotation, not by apps
MSSQL_PID='Express'              # SQL Server edition
MSSQL_IMAGE='mcr.microsoft.com/mssql/server:2022-latest'

# Tor (only if site-checker is in use)
TOR_SOCKS_HOST=127.0.0.1
TOR_SOCKS_PORT=9050
USER_AGENT_HEADER='Mozilla/5.0 (compatible; MonericaSiteChecker/1.0; +https://monerica.com)'
```

The `db-rotate-credentials.sh` script prints the exact lines to paste in here whenever you change DB credentials.

---

### Web app deploy (`deploy.sh`)

**Routine deploy** — run for every code change:

```bash
cd CI
./deploy.sh                     # deploy 'web' (default)
./deploy.sh web                 # explicit
./deploy.sh web --skip-build    # already-built artifacts
./deploy.sh web --skip-tests
./deploy.sh web --skip-migrate  # skip EF migrations
```

The pipeline runs: configs → restore → build → test → publish → sync → migrate → nginx → smoke. Tor and SSL are not touched by routine deploys.

**One-time server setup** — run once per VPS:

```bash
./deploy.sh web --task bootstrap   # install dotnet, docker, base nginx
./deploy.sh web --task mssql       # install + configure SQL Server in Docker
./deploy.sh web --task ssl         # issue Let's Encrypt cert (auto-renews)
./deploy.sh web --task tor         # install tor + onion service
```

**Individual tasks** — run any task on its own with `--task <name>`:

`configs` · `restore` · `build` · `test` · `publish` · `sync` · `migrate` · `nginx` · `smoke` · `ssl` · `tor` · `bootstrap` · `mssql` · `maintenance-on` · `maintenance-off`

**Maintenance page** — flip the site to a static "down for maintenance" page during risky operations:

```bash
./deploy.sh web --task maintenance-on
# do the risky thing
./deploy.sh web --task maintenance-off
```

---

### Background-job deploy (`deploy-jobs.sh`)

Each background job runs as a systemd one-shot triggered by a timer. Unit names are `dm-job-<job>.{service,timer}`. Jobs share the same `DB_CONNECTION_STRING` as the web app.

The 5 jobs and their schedules:

| Job | Schedule | Purpose |
| --- | --- | --- |
| `email-message-maker` | Daily at 02:15 UTC | Generates outbound email messages |
| `newsletter-sender` | Hourly at :00 UTC | Sends queued newsletter emails |
| `site-checker` | Weekly, Tuesdays at 12:15 UTC | Checks listing uptime via Tor |
| `sponsored-listing-opening` | Every 30 minutes | Notifies subscribers when slots open |
| `sponsored-listing-reminder` | Every 3 hours (00:00, 03:00, … 21:00 UTC) | Reminds sponsors of expiring listings |

**Routine deploy:**

```bash
./deploy-jobs.sh                              # deploy newsletter-sender (default)
./deploy-jobs.sh sponsored-listing-reminder   # deploy a single job
./deploy-jobs.sh newsletter-sender,site-checker
./deploy-jobs.sh all                          # deploy every jobs/*.conf
./deploy-jobs.sh all --skip-build
./deploy-jobs.sh all --skip-tests
```

Pipeline: configs → restore → build → test → publish → sync → install-timer.

**Observability and lifecycle:**

```bash
./deploy-jobs.sh --task status               # last/next run + state for ALL jobs
./deploy-jobs.sh <job> --task logs           # last 100 journal lines for one job
./deploy-jobs.sh <job> --task run-now        # trigger immediately (test run)
./deploy-jobs.sh <job> --task disable        # stop the timer (job stops firing)
./deploy-jobs.sh <job> --task enable         # re-enable a disabled timer
./deploy-jobs.sh <job> --task remove         # uninstall the unit + timer
```

**Direct systemd inspection** (run on the VPS or via SSH):

```bash
# All Monerica timers at a glance
systemctl list-timers 'dm-job-*' --all --no-pager

# Status of one job
systemctl status dm-job-sponsored-listing-reminder.timer
systemctl status dm-job-sponsored-listing-reminder.service

# Recent log output (Console.WriteLines from the .NET app land here)
sudo journalctl -u dm-job-sponsored-listing-reminder.service --since "24 hours ago" --no-pager

# Trigger a run manually without waiting for the next tick
sudo systemctl start dm-job-sponsored-listing-reminder.service
```

The web app journal lives at `journalctl -u monerica.com` (or whatever `APP_NAME` is set to in `services/web.conf`).

---

### Tuning job behavior

Most job-level settings live in each job's `appsettings.json` in the source tree (e.g. `src/DirectoryManager/DirectoryManager.SponsoredListingReminder/appsettings.template.json`) — these travel with the code and are baked into the publish output at deploy time.

For example, to give sponsors 72 hours' notice instead of 48:

1. Edit `src/DirectoryManager/DirectoryManager.SponsoredListingReminder/appsettings.template.json` → `"ExpirationHours": 72`.
2. Commit, push.
3. `./deploy-jobs.sh sponsored-listing-reminder`
4. Watch the next run's journal for `Found N listings expiring within 72 hours.`

For schedule changes, edit `CI/jobs/<job>.conf` (`ON_CALENDAR=` and `SCHEDULE_DESCRIPTION=`) and redeploy. The `ON_CALENDAR` value uses systemd timer syntax — see `man systemd.time`.

---

### Database credential management

Production runs under a least-privilege SQL Server login with `db_owner` on the application database only — never as `sa`. The application login name and database name are defined in `DB_CONNECTION_STRING` in `deploy-config.sh`; the examples below use `<app_login>` as a placeholder for whatever you set there.

Both initial creation and password rotation use the same script:

```bash
./db-rotate-credentials.sh --login <app_login>                    # generate password
./db-rotate-credentials.sh --login <app_login> --password '...'   # provide one
./db-rotate-credentials.sh --login sa                             # rotate sa
```

Behavior:

- If the login does not exist: creates it, creates a database user on the application database, grants `db_owner`.
- If the login exists: rotates the password only (no permission changes).
- Tests the new credentials with `SELECT 1` before printing the connection string.

**End-to-end first-time setup of the non-sa app login:**

1. Run `./db-rotate-credentials.sh --login <app_login>` (pick any valid SQL identifier; it must match what you put in `DB_CONNECTION_STRING`).
2. Save the printed password in your password manager (it is shown once).
3. Paste the printed `DB_USER` / `DB_PASSWORD` / `DB_CONNECTION_STRING` block into `CI/deploy-config.sh`.
4. Redeploy everything that holds the connection string:
   ```bash
   ./deploy.sh web
   for j in newsletter-sender sponsored-listing-opening sponsored-listing-reminder \
            email-message-maker site-checker; do
       ./deploy-jobs.sh "$j"
   done
   ```
5. Verify with `./db-verify-deployed-configs.sh` (see below).
6. Once verified clean, rotate `sa` with `./db-rotate-credentials.sh --login sa` and update `MSSQL_SA_PASSWORD` in `deploy-config.sh`. **No redeploy needed for an `sa` rotation** — applications do not use `sa`.

**Future rotations** of the app login:

```bash
./db-rotate-credentials.sh --login <app_login>
# paste new values into deploy-config.sh
./deploy.sh web
for j in newsletter-sender sponsored-listing-opening sponsored-listing-reminder \
         email-message-maker site-checker; do
    ./deploy-jobs.sh "$j"
done
./db-verify-deployed-configs.sh
```

A 90-180 day rotation cadence is reasonable.

---

### Verification

`db-verify-deployed-configs.sh` audits the production VPS in three ways. It is read-only at the file/SQL level; the third section triggers each background job to confirm it can authenticate (each job is a oneshot, so a triggered run is the same as any scheduled run).

```bash
./db-verify-deployed-configs.sh
```

The three sections:

1. **File audit** — finds every `appsettings*.json` under `/var/www/` on the VPS and prints the `User Id=` from each. Every `appsettings.Production.json` should show the application login defined in `DB_CONNECTION_STRING`. Base `appsettings.json` files showing `(none)` are normal — they ship as empty placeholders, with the Production overlay carrying the real value at runtime.
2. **Live audit** — queries `sys.dm_exec_sessions` for every login currently connected to the application database. After a credential migration, only the new login should appear.
3. **Functional audit** — starts each background job in turn, waits for it to finish, and prints its systemd `Result` plus the last 6 journal lines. A clean run shows `Processing complete.` and an exit status of 0.

If the live audit shows lingering connections from an old login after a redeploy, restart the web service (`sudo systemctl restart <web-app-name>`) to force fresh connections — pooled connections opened before the redeploy can outlive it.

---

### Quick reference: log locations and unit names

| Service | Unit | Logs |
| --- | --- | --- |
| Web app | `monerica.com` (or `services/web.conf` `APP_NAME`) | `journalctl -u monerica.com` |
| Email message maker | `dm-job-email-message-maker` | `journalctl -u dm-job-email-message-maker` |
| Newsletter sender | `dm-job-newsletter-sender` | `journalctl -u dm-job-newsletter-sender` |
| Site checker | `dm-job-site-checker` | `journalctl -u dm-job-site-checker` |
| Sponsored listing opening | `dm-job-sponsored-listing-opening` | `journalctl -u dm-job-sponsored-listing-opening` |
| Sponsored listing reminder | `dm-job-sponsored-listing-reminder` | `journalctl -u dm-job-sponsored-listing-reminder` |
| SQL Server (Docker) | container `mssql` | `docker logs mssql` |

Deployed file layout on the VPS:

- `/var/www/dm-web/` — web app publish output + `appsettings.json` + `appsettings.Production.json`
- `/var/www/dm-jobs/<job>/` — one directory per job, each with its own `appsettings.json` and `appsettings.Production.json`
- `/etc/systemd/system/dm-job-<job>.{service,timer}` — installed by `deploy-jobs.sh`
- `/etc/nginx/sites-available/<app>.conf` and `sites-enabled/` — managed by `deploy.sh --task nginx`
