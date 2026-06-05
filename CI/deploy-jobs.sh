#!/usr/bin/env bash
# deploy-jobs.sh — DirectoryManager background-job deploy (systemd timers)
#
# Companion to deploy.sh. Deploys console apps that run on a schedule:
#   - EmailMessageMaker        (daily   02:15 UTC)
#   - NewsletterSender         (hourly  *:00 UTC)
#   - SiteChecker              (weekly  Tue 12:15 UTC, uses Tor SOCKS)
#   - SponsoredListingOpening  (every 30 min, sends email)
#   - SponsoredListingReminder (every  3 hours, sends email)
#
# All jobs share the same DB_CONNECTION_STRING from deploy-config.sh,
# so they hit the same monerica_db as the web app.
#
# Email-using jobs (NewsletterSender, SponsoredListing*) read SendGrid
# credentials from the DB at runtime via ContentSnippetRepository — same
# pattern as the web app. Rotate the API key in the admin UI (or directly
# in the ContentSnippet table); no redeploy needed.
#
# === ROUTINE DEPLOY =========================================================
#   ./deploy-jobs.sh                              # deploy newsletter-sender (default)
#   ./deploy-jobs.sh newsletter-sender            # deploy a single job
#   ./deploy-jobs.sh newsletter-sender,site-checker
#   ./deploy-jobs.sh all                          # deploy every jobs/*.conf
#   ./deploy-jobs.sh all --skip-build             # skip restore/build/publish
#   ./deploy-jobs.sh all --skip-tests
#
# Pipeline: configs → restore → build → test → publish → sync → install-timer
#
# === OBSERVABILITY ==========================================================
#   ./deploy-jobs.sh --task status      # last-run / next-run / state for ALL jobs
#   ./deploy-jobs.sh <job> --task logs  # last 100 journal lines for one job
#   ./deploy-jobs.sh <job> --task run-now    # trigger immediately (test run)
#   ./deploy-jobs.sh <job> --task disable    # stop the timer (job stops firing)
#   ./deploy-jobs.sh <job> --task enable     # re-enable a disabled timer
#   ./deploy-jobs.sh <job> --task remove     # uninstall the unit + timer
#
# Each job runs as oneshot under www-data. journalctl -u dm-job-<name> for logs.
# `systemctl list-timers 'dm-job-*'` for at-a-glance schedule status.
#
# Local prereqs: dotnet, jq, ssh, scp, tar
# Job config: jobs/<job-name>.conf

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"
JOBS_DIR="$SCRIPT_DIR/jobs"
UNIT_PREFIX="dm-job-"   # systemd unit name prefix → dm-job-<job-name>.{service,timer}

# === Args ====================================================================
SKIP_BUILD=0
SKIP_TESTS=0
SINGLE_TASK=""
JOB_LIST="newsletter-sender"

if [[ $# -gt 0 && "${1:0:2}" != "--" ]]; then
    JOB_LIST="$1"
    shift
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-build) SKIP_BUILD=1; shift ;;
        --skip-tests) SKIP_TESTS=1; shift ;;
        --task)       SINGLE_TASK="${2:-}"; shift 2 ;;
        -h|--help)    sed -n '2,40p' "$0"; exit 0 ;;
        *) echo "Unknown flag: $1" >&2; exit 1 ;;
    esac
done

# === Tool checks =============================================================
for tool in dotnet jq ssh scp tar; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool missing: $tool" >&2; exit 1; }
done

# === Source shared config ====================================================
[[ -f "$CONFIG_PATH" ]] || { echo "Missing $CONFIG_PATH" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

# === Console helpers (match deploy.sh) ======================================
C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_YELLOW=$'\e[33m' C_RED=$'\e[31m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
CURRENT_JOB="<global>"
write_task() { echo; echo "${C_CYAN}----- [$CURRENT_JOB] Task: $1 -----${C_RESET}"; }
write_step() { echo "${C_CYAN}==> $1${C_RESET}"; }
write_ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
write_warn() { echo "    ${C_YELLOW}WARN: $1${C_RESET}"; }
write_err()  { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

# === SSH helpers (match deploy.sh) ==========================================
SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { echo "SSH_PASSWORD set but sshpass missing." >&2; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o ServerAliveInterval=30)
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

ssh_run()        { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'"; }
ssh_run_ignore() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'" || true; }
ssh_query()      { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'" 2>/dev/null | tr -d '\r' || true; }
scp_send()       { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" -r "$1" "$SSH_TARGET:$2"; }

# === Resolve job list ========================================================
resolve_jobs() {
    if [[ "$JOB_LIST" == "all" ]]; then
        JOBS=()
        for f in "$JOBS_DIR"/*.conf; do
            [[ -f "$f" ]] && JOBS+=("$(basename "$f" .conf)")
        done
    else
        IFS=',' read -ra JOBS <<< "$JOB_LIST"
    fi
    [[ ${#JOBS[@]} -gt 0 ]] || { echo "No jobs. Add jobs/<name>.conf or pass a job name." >&2; exit 1; }
}

# ============================================================================
# GLOBAL TASKS (don't need a specific job context)
# ============================================================================

task_status_all() {
    write_task "StatusAll"

    # The previous version wrapped multi-line scripts in `sudo bash -c '...'`
    # via ssh_run. Quotes inside the script (awk '{...}', \$u, etc.) collide
    # with the outer single quotes and produce mangled remote commands.
    # Fix: pipe the script over ssh stdin to `bash -s` instead. Local vars
    # like $UNIT_PREFIX expand at heredoc time; remote vars escape with \$.
    "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -s" <<EOF
set +e

echo
echo "==> Timers (next/last run, state):"
systemctl list-timers '${UNIT_PREFIX}*' --all --no-pager 2>/dev/null || echo "(no timers found)"

echo
echo "==> Last-run summary per service:"
units=\$(systemctl list-units --all --type=service --no-legend '${UNIT_PREFIX}*.service' 2>/dev/null | awk '{print \$1}')
if [ -z "\$units" ]; then
    echo "(no ${UNIT_PREFIX}*.service units installed yet)"
else
    for u in \$units; do
        echo "--- \$u ---"
        systemctl show "\$u" \
            --property=ActiveState,SubState,Result,ExecMainStatus,ExecMainExitTimestamp \
            --no-pager
    done
fi

echo
echo "==> Tor daemon status (required by site-checker):"
if systemctl is-active tor@default >/dev/null 2>&1; then
    echo "active (tor@default)"
elif systemctl is-active tor >/dev/null 2>&1; then
    echo "active (tor)"
else
    echo "inactive — run './deploy.sh web --task tor' from the web repo if you plan to deploy site-checker"
fi
EOF
}

# ============================================================================
# PER-JOB TASKS
# ============================================================================

task_set_configs() {
    write_task "SetConfigs"

    # Some console projects (SiteChecker, etc.) only ship appsettings.template.json
    # in source — devs are expected to copy template → real after cloning. For
    # production deploys we need a real appsettings.json so ASP.NET's config
    # loader is happy, even though appsettings.Production.json is what carries
    # the actual values. Create a minimal stub if the project doesn't have one.
    local base_settings="$PROJECT_DIR/appsettings.json"
    if [[ ! -f "$base_settings" ]]; then
        # Prefer the project's own template if one exists — preserves the shape
        # the C# code expects (correct top-level keys with empty values).
        if [[ -f "$PROJECT_DIR/appsettings.template.json" ]]; then
            cp "$PROJECT_DIR/appsettings.template.json" "$base_settings"
            write_ok "Created $base_settings (copied from appsettings.template.json)"
        else
            echo '{}' > "$base_settings"
            write_ok "Created minimal $base_settings (no template found)"
        fi
    fi

    # appsettings.Production.json bundled into publish output. Same pattern as
    # the web deploy: never committed, always regenerated from deploy-config.sh.
    # ASP.NET overlays this on top of appsettings.json at runtime.
    #
    # WHAT GOES IN HERE (and what doesn't):
    #
    # - ConnectionStrings: yes — only value that legitimately differs between
    #   dev (empty/localhost) and production (monerica_db on the VPS).
    #
    # - SendGrid keys: YES, for email-using jobs (NEEDS_EMAIL=1). Each job now
    #   reads its SendGridConfig from the "SendGrid" config section at startup
    #   (config.GetSection("SendGrid").Get<SendGridConfig>()), same pattern as the
    #   web app. Source of truth is deploy-config.sh (SENDGRID_API_KEY /
    #   SENDGRID_SENDER_EMAIL / SENDGRID_SENDER_NAME); never committed. Non-email
    #   jobs (e.g. SiteChecker) skip the block entirely.
    #
    # - Job-specific config (EmailKeys, NotificationLinkTemplate*,
    #   RenewalLinkTemplate, ExpirationHours, EmailCampaignKey): NO. Each job's
    #   source appsettings.json already has these populated with production
    #   values that travel with the code. No deploy-time injection needed.
    #
    # - TorProxy + UserAgent (site-checker only): yes — these are truly
    #   environment-specific (tor port, identifying UA string).
    local prod_settings="$PROJECT_DIR/appsettings.Production.json"

    local jq_args=(-n --arg conn "$DB_CONNECTION_STRING")
    local jq_filter='{ ConnectionStrings: { DefaultConnection: $conn } }'

    if [[ "${NEEDS_EMAIL:-0}" -eq 1 ]]; then
        jq_args+=(--arg sgkey   "${SENDGRID_API_KEY:-}" \
                  --arg sgemail "${SENDGRID_SENDER_EMAIL:-}" \
                  --arg sgname  "${SENDGRID_SENDER_NAME:-}")
        # The C# code reads SendGrid:ApiKey / SenderEmail / SenderName via
        # config.GetSection("SendGrid").Get<SendGridConfig>(). EmailService throws
        # if ApiKey is empty, so a missing deploy-config.sh value fails fast.
        jq_filter+=' + {
            SendGrid: {
                ApiKey:      $sgkey,
                SenderEmail: $sgemail,
                SenderName:  $sgname
            }
        }'
    fi

    if [[ "${NEEDS_TOR:-0}" -eq 1 ]]; then
        jq_args+=(--arg torhost "${TOR_SOCKS_HOST:-127.0.0.1}" \
                  --arg torport "${TOR_SOCKS_PORT:-9050}" \
                  --arg ua "${USER_AGENT_HEADER:-Mozilla/5.0 (compatible; MonericaSiteChecker/1.0; +https://monerica.com)}")
        # The C# Program.cs reads these exact keys: TorProxy:Host, TorProxy:Port.
        # On Linux production, TryStartTorAsync first calls IsTorAvailable() which
        # probes 127.0.0.1:9050 — if the system tor@default daemon is already
        # listening (it is, because of `./deploy.sh web --task tor`), it returns
        # true immediately without trying to launch the bundled tor.exe. So no
        # extra Linux-specific flag is needed; the original code path already
        # does the right thing.
        #
        # UserAgent:Header is also required by SiteChecker (Program.cs throws
        # if missing). Inject a sensible default; override via deploy-config.sh
        # if you want a different UA string.
        jq_filter+=' + {
            TorProxy: {
                Host: $torhost,
                Port: ($torport|tonumber)
            },
            UserAgent: {
                Header: $ua
            }
        }'
    fi

    jq "${jq_args[@]}" "$jq_filter" > "$prod_settings"
    write_ok "Wrote $prod_settings"
}

task_restore_packages() {
    write_task "RestorePackages"
    [[ -d "$PUBLISH_OUT" ]] && rm -rf "$PUBLISH_OUT"

    # Wipe bin/obj across the whole solution (same reasoning as deploy.sh:
    # dotnet's incremental cache can ship stale binaries).
    local solution_root
    solution_root="$(cd "$PROJECT_DIR/../.." && pwd)"
    find "$solution_root" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
    write_step "Cleaned bin/obj across solution"

    pushd "$PROJECT_DIR" >/dev/null
    dotnet restore
    popd >/dev/null
    write_ok "Packages restored"
}

task_build_project() {
    write_task "BuildProject"
    pushd "$PROJECT_DIR" >/dev/null
    dotnet build --configuration "$BUILD_CONFIGURATION" --runtime "$DOTNET_RUNTIME" --no-restore --no-incremental
    popd >/dev/null
    write_ok "Build succeeded"
}

task_run_unit_tests() {
    write_task "RunUnitTests"
    if [[ -z "${TEST_SOLUTION_SOURCE_PATH:-}" || ! -f "$TEST_SOLUTION_PATH" ]]; then
        write_warn "No test solution configured — skipping"
        return
    fi
    dotnet test "$TEST_SOLUTION_PATH" \
        --configuration "$BUILD_CONFIGURATION" \
        --logger "console;verbosity=normal" \
        --nologo
    write_ok "Tests passed"
}

task_create_package() {
    write_task "CreatePackage"
    mkdir -p "$PUBLISH_OUT"
    pushd "$PROJECT_DIR" >/dev/null
    dotnet publish \
        --framework "$DOTNET_FRAMEWORK" \
        --output "$PUBLISH_OUT" \
        --configuration "$BUILD_CONFIGURATION" \
        --runtime "$DOTNET_RUNTIME" \
        --self-contained false \
        --no-build
    popd >/dev/null

    # Force-copy appsettings.Production.json into the publish output. The
    # console projects' .csproj files only declare <None Update="appsettings.json">
    # rules — they don't know about the Production overlay we generate at deploy
    # time. Without this explicit copy, MSBuild silently drops the Production
    # file during publish, the deploy ships only the empty-placeholder
    # appsettings.json, and runtime crashes on missing values (UserAgent, DB
    # connection string — depending on the job).
    local prod_src="$PROJECT_DIR/appsettings.Production.json"
    if [[ -f "$prod_src" ]]; then
        cp "$prod_src" "$PUBLISH_OUT/appsettings.Production.json"
        write_ok "Copied appsettings.Production.json into publish output"
    else
        write_err "appsettings.Production.json missing at $prod_src — task_set_configs should have written it"
        exit 1
    fi

    write_ok "Package built: $PUBLISH_OUT"
}

task_sync_files() {
    write_task "SyncFiles"

    # Stop the timer + service before swapping files. We want a clean handoff,
    # and a job that's mid-run when we replace its dlls would crash mid-flight.
    ssh_run_ignore "systemctl stop ${UNIT_PREFIX}${JOB_NAME}.timer 2>/dev/null || true"
    ssh_run_ignore "systemctl stop ${UNIT_PREFIX}${JOB_NAME}.service 2>/dev/null || true"

    local tar_file="/tmp/${UNIT_PREFIX}${JOB_NAME}.tar.gz.local"
    tar -czf "$tar_file" -C "$PUBLISH_OUT" .
    scp_send "$tar_file" "/tmp/${UNIT_PREFIX}${JOB_NAME}.tar.gz"
    ssh_run "mkdir -p $DEPLOY_PATH && tar -xzf /tmp/${UNIT_PREFIX}${JOB_NAME}.tar.gz -C $DEPLOY_PATH && rm /tmp/${UNIT_PREFIX}${JOB_NAME}.tar.gz"
    ssh_run "chown -R www-data:www-data $DEPLOY_PATH && chmod -R 755 $DEPLOY_PATH"
    write_ok "Files synced to $DEPLOY_PATH"
}

task_install_timer() {
    write_task "InstallTimer"

    : "${ON_CALENDAR:?ON_CALENDAR required in jobs/${JOB_NAME}.conf}"

    if [[ "${NEEDS_TOR:-0}" -eq 1 ]]; then
        # Fail fast — running site-checker without tor would silently degrade
        # to checking onion sites with the public IP, defeating the point.
        local tor_state
        tor_state=$(ssh_query "systemctl is-active tor@default 2>/dev/null || systemctl is-active tor 2>/dev/null || echo inactive")
        if [[ "$tor_state" != "active" ]]; then
            write_err "Job $JOB_NAME has NEEDS_TOR=1 but tor daemon is not active on the server."
            write_err "Run from the web repo: ./deploy.sh web --task tor"
            exit 1
        fi
        write_ok "Tor daemon is active"
    fi

    # --- service unit (Type=oneshot — runs to completion, then exits) -------
    local svc_file="/tmp/${UNIT_PREFIX}${JOB_NAME}.service.local"
    cat > "$svc_file" <<EOF
[Unit]
Description=$JOB_NAME (DirectoryManager scheduled job — $SCHEDULE_DESCRIPTION)
After=network-online.target
Wants=network-online.target
$( [[ "${NEEDS_TOR:-0}" -eq 1 ]] && echo "After=tor@default.service" )
$( [[ "${NEEDS_TOR:-0}" -eq 1 ]] && echo "Wants=tor@default.service" )

[Service]
Type=oneshot
WorkingDirectory=$DEPLOY_PATH
ExecStart=/usr/bin/dotnet $DEPLOY_PATH/$DLL_NAME
User=www-data
Group=www-data
Environment=DOTNET_ENVIRONMENT=Production
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=TZ=UTC

# Cap total execution time. For Type=oneshot, TimeoutStartSec is the runtime
# ceiling (RuntimeMaxSec is silently ignored on oneshot). If the job runs
# longer than this, systemd kills it with SIGTERM, then SIGKILL, and the
# unit's Result becomes "timeout" — visible in --task status.
TimeoutStartSec=${MAX_RUNTIME:-30min}

# Surface failures clearly: SuccessExitStatus stays default (0 only),
# so non-zero exit → systemd marks the unit failed → visible in status task.
StandardOutput=journal
StandardError=journal
SyslogIdentifier=${UNIT_PREFIX}${JOB_NAME}

[Install]
WantedBy=multi-user.target
EOF
    scp_send "$svc_file" "/tmp/${UNIT_PREFIX}${JOB_NAME}.service"
    ssh_run "mv /tmp/${UNIT_PREFIX}${JOB_NAME}.service /etc/systemd/system/${UNIT_PREFIX}${JOB_NAME}.service"

    # --- timer unit ---------------------------------------------------------
    # Persistent=false: deploys NEVER cause the job to fire as a side effect.
    # If a scheduled run is missed (server offline, timer just installed,
    # mid-deploy stop/start), it's simply skipped — the next regular slot
    # picks up wherever the work left off. This is the right tradeoff for
    # these jobs because each one is idempotent at its cadence: a missed
    # newsletter-sender hour is sent next hour, a missed site-checker week
    # is checked next Tuesday, etc.
    #
    # If a specific job ever genuinely needs catch-up behavior, set
    # TIMER_PERSISTENT=true in its jobs/<name>.conf to override.
    local timer_file="/tmp/${UNIT_PREFIX}${JOB_NAME}.timer.local"
    cat > "$timer_file" <<EOF
[Unit]
Description=Timer: $JOB_NAME ($SCHEDULE_DESCRIPTION)

[Timer]
OnCalendar=$ON_CALENDAR
Persistent=${TIMER_PERSISTENT:-false}
AccuracySec=1s
Unit=${UNIT_PREFIX}${JOB_NAME}.service

[Install]
WantedBy=timers.target
EOF
    scp_send "$timer_file" "/tmp/${UNIT_PREFIX}${JOB_NAME}.timer"
    ssh_run "mv /tmp/${UNIT_PREFIX}${JOB_NAME}.timer /etc/systemd/system/${UNIT_PREFIX}${JOB_NAME}.timer"

    ssh_run "systemctl daemon-reload"
    ssh_run "systemctl enable --now ${UNIT_PREFIX}${JOB_NAME}.timer"

    write_ok "Timer installed: ${UNIT_PREFIX}${JOB_NAME}.timer"

    # Show next scheduled run for this specific job — sanity check the cadence
    local next_run
    next_run=$(ssh_query "systemctl show ${UNIT_PREFIX}${JOB_NAME}.timer --property=NextElapseUSecRealtime --value")
    [[ -n "$next_run" ]] && echo "    ${C_GRAY}Next run: $next_run${C_RESET}"
}

task_run_now() {
    write_task "RunNow"
    write_step "Triggering ${UNIT_PREFIX}${JOB_NAME}.service immediately"
    # --no-block returns instantly; we tail journal for live output instead
    # of blocking on ssh until the (potentially long) job finishes.
    ssh_run "systemctl start --no-block ${UNIT_PREFIX}${JOB_NAME}.service"
    write_ok "Started. Tailing logs (Ctrl-C to detach — job keeps running):"
    "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo journalctl -u ${UNIT_PREFIX}${JOB_NAME}.service -f --since '10 seconds ago'" || true
}

task_logs() {
    write_task "Logs"
    "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo journalctl -u ${UNIT_PREFIX}${JOB_NAME}.service -n 100 --no-pager" || true
}

task_disable() {
    write_task "Disable"
    ssh_run "systemctl disable --now ${UNIT_PREFIX}${JOB_NAME}.timer"
    write_ok "Timer disabled — job will not fire until re-enabled"
}

task_enable() {
    write_task "Enable"
    ssh_run "systemctl enable --now ${UNIT_PREFIX}${JOB_NAME}.timer"
    write_ok "Timer re-enabled"
}

task_remove() {
    write_task "Remove"
    ssh_run_ignore "systemctl disable --now ${UNIT_PREFIX}${JOB_NAME}.timer"
    ssh_run_ignore "systemctl stop ${UNIT_PREFIX}${JOB_NAME}.service"
    ssh_run_ignore "rm -f /etc/systemd/system/${UNIT_PREFIX}${JOB_NAME}.service /etc/systemd/system/${UNIT_PREFIX}${JOB_NAME}.timer"
    ssh_run "systemctl daemon-reload"
    # Deliberately leave $DEPLOY_PATH on disk — re-deploys are faster, and
    # a stray dll directory hurts nothing. Use ssh manually if you want it gone.
    write_ok "Unit + timer removed (deploy dir left intact at $DEPLOY_PATH)"
}

# ============================================================================
# PER-JOB PIPELINE
# ============================================================================
deploy_job() {
    local job="$1"
    CURRENT_JOB="$job"
    JOB_NAME="$job"

    local conf="$JOBS_DIR/$job.conf"
    [[ -f "$conf" ]] || { write_err "Job config not found: $conf"; exit 1; }

    # NEEDS_EMAIL gates SendGrid config injection in task_set_configs (email
    # jobs read SendGrid from the "SendGrid" config section). We unset it here
    # first so a stale value from a previous job's conf doesn't leak into this
    # iteration; jobs/*.conf re-sets NEEDS_EMAIL=1 for the email-using jobs.
    unset NEEDS_EMAIL NEEDS_TOR ON_CALENDAR SCHEDULE_DESCRIPTION MAX_RUNTIME \
          TEST_SOLUTION_SOURCE_PATH TIMER_PERSISTENT
    # shellcheck disable=SC1090
    source "$conf"

    : "${PROJECT_SOURCE_PATH:?PROJECT_SOURCE_PATH required in $conf}"
    : "${DLL_NAME:?DLL_NAME required in $conf}"

    PROJECT_DIR="$SCRIPT_DIR/$PROJECT_SOURCE_PATH"
    TEST_SOLUTION_PATH="$SCRIPT_DIR/${TEST_SOLUTION_SOURCE_PATH:-}"
    PUBLISH_OUT="$SCRIPT_DIR/../publish-jobs/$job"
    DEPLOY_PATH="/var/www/dm-jobs/$job"

    if [[ -n "$SINGLE_TASK" ]]; then
        case "$SINGLE_TASK" in
            configs)        task_set_configs ;;
            restore)        task_restore_packages ;;
            build)          task_build_project ;;
            test)           task_run_unit_tests ;;
            publish)        task_create_package ;;
            sync)           task_sync_files ;;
            install-timer)  task_install_timer ;;
            run-now)        task_run_now ;;
            logs)           task_logs ;;
            disable)        task_disable ;;
            enable)         task_enable ;;
            remove)         task_remove ;;
            *) write_err "Unknown task: $SINGLE_TASK"; exit 1 ;;
        esac
        return
    fi

    task_set_configs
    if [[ $SKIP_BUILD -eq 0 ]]; then
        task_restore_packages
        task_build_project
        if [[ $SKIP_TESTS -eq 0 ]]; then
            task_run_unit_tests
        else
            write_warn "Skipping tests (--skip-tests)"
        fi
        task_create_package
    else
        write_warn "Skipping build (--skip-build)"
        [[ -d "$PUBLISH_OUT" ]] || { write_err "$PUBLISH_OUT missing — nothing to deploy"; exit 1; }
    fi
    task_sync_files
    task_install_timer
}

# ============================================================================
# MAIN
# ============================================================================

# Global tasks (no job context needed) handled before resolving job list.
if [[ "$SINGLE_TASK" == "status" ]]; then
    task_status_all
    exit 0
fi

resolve_jobs

for job in "${JOBS[@]}"; do
    deploy_job "$job"
done

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Job deployment complete: ${JOBS[*]}${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo
echo " ${C_GRAY}Status (all jobs):  ./deploy-jobs.sh --task status${C_RESET}"
echo " ${C_GRAY}Logs:               ./deploy-jobs.sh <job> --task logs${C_RESET}"
echo " ${C_GRAY}Run on demand:      ./deploy-jobs.sh <job> --task run-now${C_RESET}"
echo " ${C_GRAY}List timers (raw):  ssh $SSH_TARGET systemctl list-timers '${UNIT_PREFIX}*'${C_RESET}"
echo