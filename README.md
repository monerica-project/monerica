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

The Monerica website is built with C# and ASP.NET Core MVC on .NET 10. It runs on Windows, Linux, or macOS — production runs on Linux. The database is PostgreSQL, accessed through Entity Framework Core (Npgsql provider).

### Features

DirectoryManager is capable of:

- Adding, editing, deleting, and viewing links in the directory
- Auto-creating categories and sub-categories when adding new links
- Auditing the links
- Public submissions, verification requests and problem reports, reviewed by an admin
- Reviews and replies on listings
- Sponsored listing management, paid in Monero through BTCPay Server or NOWPayments
- Video sponsorship checkout
- Email reminders for expiring sponsored listings
- Newsletter campaigns
- Periodic site-uptime checking via Tor
- A Tor onion version of the site

---

## Application Structure

Everything lives in one solution, `src/DirectoryManager/DirectoryManager.sln`: one web application, a set of console apps that run on a schedule, and the libraries they share.

#### Web

- **DirectoryManager.Web** — the web application (controllers and views).

#### Scheduled console apps

- **DirectoryManager.EmailMessageMaker** — generates outbound email messages.
- **DirectoryManager.NewsletterSender** — sends queued newsletter emails.
- **DirectoryManager.ReviewNotifier** — emails a listing owner when a new review or reply on their listing goes live.
- **DirectoryManager.SiteChecker** — checks whether existing listings are online (uses Tor for onion links).
- **DirectoryManager.SponsoredListingOpening** — notifies subscribers when sponsor slots open.
- **DirectoryManager.SponsoredListingReminder** — emails sponsors about expiring listings.
- **DirectoryManager.ReviewModerator** — checks the order proof on submitted reviews. It has a job config but is not scheduled in production.
- **DirectoryManager.DetailChecker** — re-reads live listings and proposes detail corrections as pending revisions. Run by hand; it writes nothing unless `DetailCheck:DryRun` is turned off.

#### Libraries

- **DirectoryManager.Data** — EF Core `ApplicationDbContext`, models, repositories, migrations.
- **DirectoryManager.Services** — shared services used by the web app and the console apps.
- **DirectoryManager.Common** — shared constants, interfaces and models.
- **DirectoryManager.DisplayFormatting** — builds the display models and HTML fragments for listings.
- **DirectoryManager.FileStorage** — site file storage (images and other uploads) on Bunny.net Edge Storage.
- **DirectoryManager.Utilities** — helpers and input validation.
- **BtcPayServer.API** and **NowPayments.API** — clients for the two payment processors.

#### Tests

- **Tests/DirectoryManager.Data.Tests**, **Tests/DirectoryManager.Web.Tests**, **Tests/DirectoryManager.Utilities.Tests**

```bash
cd src/DirectoryManager
dotnet test DirectoryManager.sln
```

---

## Local Development

### Prerequisites

- .NET 10 SDK
- PostgreSQL (production runs version 14; a local Docker container is fine, e.g. `docker run -d --name monerica-pg -e POSTGRES_PASSWORD=pg -p 5432:5432 postgres:17`)
- The EF Core command-line tool, for migrations: `dotnet tool install --global dotnet-ef`

### Application Settings

Each runnable project reads `appsettings.json` from its own directory. Those files are git-ignored for most projects, so real values never get committed; an `appsettings.template.json` with empty values is committed next to them.

To set up a project for the first time:

1. Find the `appsettings.template.json` in the project you want to run (start with `DirectoryManager.Web`).
2. Copy it to `appsettings.json` in the same directory.
3. Set `ConnectionStrings:DefaultConnection` to a PostgreSQL connection string:

   ```
   Host=127.0.0.1;Port=5432;Database=monerica;Username=<role>;Password=...
   ```

Any setting can also be supplied as an environment variable, for example `ConnectionStrings__DefaultConnection`.

The other sections of the web template are only needed for the features that use them:

| Section | Needed for |
| --- | --- |
| `SendGrid` | Sending email |
| `PaymentProcessors` | Sponsorship and donation checkouts (NOWPayments and BTCPay Server keys, store IDs, webhook secrets) |
| `BunnyStorage` | Uploading and listing site files (`StorageZoneName`, `AccessKey`, `StorageHostname`). Not in the template; add it by hand |
| `ConnectionStrings:AzureStorage` | Nothing. Left over from the old Azure Blob storage; leave it empty |

Never commit real keys, passwords or webhook secrets. This repository is public.

### Database

The schema is managed by Entity Framework Core migrations in `DirectoryManager.Data/Migrations`. Create an empty database and a role that owns it, then apply the migrations. Both commands below are run from `src/DirectoryManager/DirectoryManager.Web` and use the connection string from that project's `appsettings.json`.

Apply pending migrations:

```bash
dotnet ef database update \
    --context ApplicationDbContext \
    --project ../DirectoryManager.Data/DirectoryManager.Data.csproj
```

Create a new migration after changing a model:

```bash
dotnet ef migrations add MigrationName \
    --context ApplicationDbContext \
    --project ../DirectoryManager.Data/DirectoryManager.Data.csproj
```

The web app does not migrate the database when it starts normally. It only does so when started with `--migrate-only` (`dotnet run -- --migrate-only`), which applies the migrations and exits. The production deploy uses that flag.

To work against a copy of production data instead of an empty database, `CI/db-sync-local.sh` restores a dump into a local Postgres container (see `CI/README-backups.md`).

### Run

```bash
cd src/DirectoryManager/DirectoryManager.Web
dotnet run
```

The site listens on `https://localhost:7145` and `http://localhost:5007`. There is no public sign-up page: admin accounts are ASP.NET Core Identity users, and you sign in at `/account/login`.

---

## Production Operations (Linux VPS)

Production is one Ubuntu 22.04 VPS running:

- The web app under **systemd** (unit `dm-web`), reverse-proxied by nginx installed on the host
- **PostgreSQL 14** installed on the host, listening on localhost only
- Six background jobs as systemd timers, each prefixed `dm-job-`
- A Tor hidden service for the onion site, and Tor's SOCKS proxy (used by `site-checker`)

All operational scripts live in `CI/` and are run from a **dev machine** (Linux/macOS) over SSH. They source `CI/deploy-config.sh` for credentials and the SSH target. Local tools needed: `dotnet`, `jq`, `ssh`, `scp`, `tar`, `curl`.

### CI/ folder

| File | Purpose |
| --- | --- |
| `deploy-config.sh` | Secrets + SSH target. **Gitignored.** |
| `services/web.conf` | Web app deploy config (domain, port, paths). **Gitignored.** |
| `deploy.sh` | Web app deploy: build, test, publish, sync, migrate, nginx, smoke test; also SSL and Tor tasks |
| `deploy-jobs.sh` | Background-job deploy (timers + services), status, logs, enable/disable/remove |
| `jobs/<name>.conf` | Per-job schedule + project source path |
| `verify-jobs.sh` | Checks each deployed job's units, files and settings without running it |
| `db-backup.sh` | Dumps the production database and downloads the dump |
| `db-refresh.sh` | **Replaces** the production database from a dump file |
| `db-sync-local.sh` | Restores a production dump into a local Postgres container |
| `db-rotate-credentials.sh` | Creates a PostgreSQL role or rotates its password on the VPS |
| `db-verify-deployed-configs.sh` | Audits which database role each deployed service uses |
| `README-backups.md` | How the automated database backups work |
| `pq-archive/` | A retention job for a different site's database on the same server; not part of this application |

`CI.bat` and `CI-Tools/` at the repository root are the old Windows build scripts. They are not used.

### deploy-config.sh

This file is never committed. It defines:

```bash
SSH_HOST=your-vps                # or IP / ~/.ssh/config alias
SSH_USER=                        # blank if the alias handles the user

BUILD_CONFIGURATION=Release
DOTNET_FRAMEWORK=net10.0
DOTNET_RUNTIME=linux-x64

# PostgreSQL
DB_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=<db_name>;Username=<app_role>;Password=...'

# The db-*.sh scripts need the parts of that string as PG_HOST, PG_PORT, PG_DB,
# PG_USER and PG_PASSWORD. Set them here too, or parse them out of the string, e.g.:
PG_DB="$(sed -nE "s/.*Database=([^;]+).*/\1/Ip" <<<"$DB_CONNECTION_STRING")"

# Email
SENDGRID_API_KEY='...'
SENDGRID_SENDER_EMAIL=
SENDGRID_SENDER_NAME=

# Site file storage (Bunny.net)
BUNNY_STORAGE_ZONE=
BUNNY_STORAGE_KEY='...'
BUNNY_STORAGE_HOST=storage.bunnycdn.com

# Payment processors. The deploy stops if a required one is missing:
# NOWPAYMENTS_API_KEY, NOWPAYMENTS_IPN_SECRET_KEY,
# BTCPAY_BASE_URL, BTCPAY_API_KEY, BTCPAY_STORE_ID, BTCPAY_WEBHOOK_SECRET
NOWPAYMENTS_...=
BTCPAY_...=

SSL_EMAIL=                       # only for --task ssl

# Tor SOCKS proxy for site-checker (these are the defaults)
TOR_SOCKS_HOST=127.0.0.1
TOR_SOCKS_PORT=9050
USER_AGENT_HEADER='...'
```

On every deploy, `deploy.sh` and `deploy-jobs.sh` generate an `appsettings.Production.json` from these values and ship it with the build. That file is git-ignored and is the only place production secrets are written. To change a secret, edit `deploy-config.sh` and redeploy.

### services/web.conf

Also never committed. It tells `deploy.sh` what to deploy and where:

```bash
APP_NAME=dm-web                  # systemd unit and nginx config name
DOMAIN=app.example.com
APP_PORT=5055
DEPLOY_PATH=/var/www/dm-web
WEB_PROJECT_SOURCE_PATH=../src/DirectoryManager/DirectoryManager.Web
WEB_DLL_NAME=DirectoryManager.Web.dll
DATA_PROJECT_SOURCE_PATH=../src/DirectoryManager/DirectoryManager.Data
TEST_SOLUTION_SOURCE_PATH=../src/DirectoryManager/DirectoryManager.sln
USES_DB=1
MIGRATION_MODE=server            # server | local | skip
CUSTOM_DOMAIN=
REQUEST_PROTOCOL=https://
TOR_KEYS_DIR=                    # local folder holding the onion service keys
```

---

### First-time server setup

Preparing a new server is mostly manual. Before the first deploy the VPS needs:

1. The ASP.NET Core 10 runtime (`aspnetcore-runtime-10.0`).
2. nginx installed on the host.
3. PostgreSQL installed on the host, with an empty database for the application.
4. An application role that owns that database. `./db-rotate-credentials.sh --login <role>` creates it and prints the connection string to put in `deploy-config.sh` (see below).
5. `deploy-config.sh` and `services/web.conf` filled in.

Then run `./deploy.sh web`. The first deploy creates the schema by applying every migration, installs the systemd unit and writes the nginx site config. After that, `./deploy.sh web --task tor` installs Tor and the onion service from the keys in `TOR_KEYS_DIR`.

The script still carries three tasks from the earlier setup, when SQL Server and nginx ran in Docker: `bootstrap`, `mssql` and `ssl`. They do not match the setup described here (`mssql` starts a SQL Server container the application can no longer use, and `ssl` stops and starts an nginx Docker container). Do not use them on a server set up as above; issue the TLS certificate with certbot directly.

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

The pipeline runs: configs → restore → build → test → publish → sync → migrate → nginx → smoke. During the sync the site shows a maintenance page, and the live site is restored at the end (also if the deploy fails part-way). Migrations run on the server with the app's `--migrate-only` flag. Tor and SSL are not touched by routine deploys.

**Individual tasks** — run any task on its own with `--task <name>`:

`configs` · `restore` · `build` · `test` · `publish` · `sync` · `migrate` · `nginx` · `smoke` · `tor` · `maintenance-on` · `maintenance-off`

**Maintenance page** — flip the site to a static "down for maintenance" page during risky operations:

```bash
./deploy.sh web --task maintenance-on
# do the risky thing
./deploy.sh web --task maintenance-off
```

---

### Background-job deploy (`deploy-jobs.sh`)

Each background job runs as a systemd one-shot triggered by a timer. Unit names are `dm-job-<job>.{service,timer}`. Jobs share the same `DB_CONNECTION_STRING` as the web app, and the jobs that send email get the `SENDGRID_*` values.

The jobs and their schedules:

| Job | Schedule | Purpose |
| --- | --- | --- |
| `email-message-maker` | Daily at 02:15 UTC | Generates outbound email messages |
| `newsletter-sender` | Hourly at :00 UTC | Sends queued newsletter emails |
| `review-notifier` | Every 5 minutes | Emails listing owners about new reviews and replies |
| `site-checker` | Weekly, Tuesdays at 12:15 UTC | Checks listing uptime via Tor |
| `sponsored-listing-opening` | Every 30 minutes | Notifies subscribers when slots open |
| `sponsored-listing-reminder` | Every 3 hours (00:00, 03:00, … 21:00 UTC) | Reminds sponsors of expiring listings |
| `review-moderator` | Every 15 minutes | Checks order proofs on reviews. Config exists; not deployed in production |

**Routine deploy:**

```bash
./deploy-jobs.sh                              # deploy newsletter-sender (default)
./deploy-jobs.sh sponsored-listing-reminder   # deploy a single job
./deploy-jobs.sh newsletter-sender,site-checker
./deploy-jobs.sh all                          # deploy every jobs/*.conf (this includes review-moderator)
./deploy-jobs.sh all --skip-build
./deploy-jobs.sh all --skip-tests
```

Pipeline: configs → restore → build → test → publish → sync → install-timer. Deploying a job never runs it; it fires at its next scheduled time.

**Observability and lifecycle:**

```bash
./deploy-jobs.sh --task status               # last/next run + state for ALL jobs
./deploy-jobs.sh <job> --task logs           # last 100 journal lines for one job
./deploy-jobs.sh <job> --task run-now        # trigger immediately (test run)
./deploy-jobs.sh <job> --task disable        # stop the timer (job stops firing)
./deploy-jobs.sh <job> --task enable         # re-enable a disabled timer
./deploy-jobs.sh <job> --task remove         # uninstall the unit + timer
./verify-jobs.sh                             # check units, files and settings without running anything
```

`verify-jobs.sh` covers five of the jobs; it does not check `review-notifier`.

**Direct systemd inspection** (run on the VPS or via SSH):

```bash
# All timers at a glance
systemctl list-timers 'dm-job-*' --all --no-pager

# Status of one job
systemctl status dm-job-sponsored-listing-reminder.timer
systemctl status dm-job-sponsored-listing-reminder.service

# Recent log output (Console.WriteLines from the .NET app land here)
sudo journalctl -u dm-job-sponsored-listing-reminder.service --since "24 hours ago" --no-pager

# Trigger a run manually without waiting for the next tick
sudo systemctl start dm-job-sponsored-listing-reminder.service
```

The web app journal lives at `journalctl -u dm-web` (or whatever `APP_NAME` is set to in `services/web.conf`).

---

### Tuning job behavior

Most job-level settings live in each job's `appsettings.template.json` in the source tree (e.g. `src/DirectoryManager/DirectoryManager.SponsoredListingReminder/appsettings.template.json`). `deploy-jobs.sh` copies the template to `appsettings.json` when that file does not exist yet, and the result is baked into the publish output. If you already have a local `appsettings.json` for the job, change it there too, because that file is the one that ships.

For example, to give sponsors 72 hours' notice instead of 48:

1. Set `"ExpirationHours": 72` in the SponsoredListingReminder settings.
2. `./deploy-jobs.sh sponsored-listing-reminder`
3. Watch the next run's journal for `Found N listings expiring within 72 hours.`

For schedule changes, edit `CI/jobs/<job>.conf` (`ON_CALENDAR=` and `SCHEDULE_DESCRIPTION=`) and redeploy. The `ON_CALENDAR` value uses systemd timer syntax — see `man systemd.time`.

---

### Database backups

The server dumps every PostgreSQL database once a day and uploads the dumps to off-site storage. That job is installed on the server, not from this repository. Details, plus how to take and restore a manual dump with `db-backup.sh`, `db-refresh.sh` and `db-sync-local.sh`, are in [`CI/README-backups.md`](CI/README-backups.md).

---

### Database credential management

The application connects as its own PostgreSQL role, which has full rights on the application database only — never as the `postgres` superuser. The role name and database name come from `DB_CONNECTION_STRING` in `deploy-config.sh`; the examples below use `<app_role>` as a placeholder.

Both initial creation and password rotation use the same script. It runs `psql` on the VPS as the `postgres` superuser through `sudo`:

```bash
./db-rotate-credentials.sh --login <app_role>                    # generate a password
./db-rotate-credentials.sh --login <app_role> --password '...'   # provide one
```

Behavior:

- If the role does not exist: creates it and grants it full privileges on the application database and its `public` schema.
- If the role exists: changes the password only (no permission changes).
- Tests the new credentials with `SELECT 1` before printing the connection string.

After a run:

1. Save the printed password in your password manager (it is shown once).
2. Put the new password in `CI/deploy-config.sh`.
3. Redeploy everything that holds the connection string:
   ```bash
   ./deploy.sh web
   for j in newsletter-sender sponsored-listing-opening sponsored-listing-reminder \
            email-message-maker site-checker review-notifier; do
       ./deploy-jobs.sh "$j"
   done
   ```
4. Verify with `./db-verify-deployed-configs.sh` (see below).

A 90-180 day rotation cadence is reasonable.

---

### Verification

`db-verify-deployed-configs.sh` audits the production VPS in three ways. It changes no files and no data; the third section triggers each background job to confirm it can authenticate (each job is a oneshot, so a triggered run is the same as any scheduled run, and it may send that job's emails).

```bash
./db-verify-deployed-configs.sh
```

The three sections:

1. **File audit** — finds every `appsettings*.json` under `/var/www/` on the VPS and prints the `Username=` from each. Every `appsettings.Production.json` should show the application role. Base `appsettings.json` files showing `(none)` are normal — they ship as empty placeholders, with the Production overlay carrying the real value at runtime.
2. **Live audit** — queries `pg_stat_activity` for every role currently connected to the application database. After a credential change, only the application role should appear.
3. **Functional audit** — starts five of the background jobs in turn (not `review-notifier`), waits for it to finish, and prints its systemd `Result` plus the last journal lines. A clean run shows `Processing complete.` and an exit status of 0.

If the live audit shows lingering connections from an old role after a redeploy, restart the web service (`sudo systemctl restart dm-web`) to force fresh connections — pooled connections opened before the redeploy can outlive it.

---

### Quick reference: log locations and unit names

| Service | Unit | Logs |
| --- | --- | --- |
| Web app | `dm-web` (or `services/web.conf` `APP_NAME`) | `journalctl -u dm-web` |
| Email message maker | `dm-job-email-message-maker` | `journalctl -u dm-job-email-message-maker` |
| Newsletter sender | `dm-job-newsletter-sender` | `journalctl -u dm-job-newsletter-sender` |
| Review notifier | `dm-job-review-notifier` | `journalctl -u dm-job-review-notifier` |
| Site checker | `dm-job-site-checker` | `journalctl -u dm-job-site-checker` |
| Sponsored listing opening | `dm-job-sponsored-listing-opening` | `journalctl -u dm-job-sponsored-listing-opening` |
| Sponsored listing reminder | `dm-job-sponsored-listing-reminder` | `journalctl -u dm-job-sponsored-listing-reminder` |
| PostgreSQL | `postgresql@14-main` | `journalctl -u postgresql@14-main` |

Deployed file layout on the VPS:

- `/var/www/dm-web/` — web app publish output + `appsettings.json` + `appsettings.Production.json`
- `/var/www/dm-jobs/<job>/` — one directory per job, each with its own `appsettings.json` and `appsettings.Production.json`
- `/etc/systemd/system/dm-job-<job>.{service,timer}` — installed by `deploy-jobs.sh`
- `/etc/nginx/sites-available/<app>.conf` and `sites-enabled/` — managed by `deploy.sh --task nginx`

## License

MIT — see the [`LICENSE`](LICENSE) file.
