#!/usr/bin/env bash
# verify-jobs.sh — sanity-check every deployed background job WITHOUT firing it.
#
# Catches the "deploy looked successful but runtime would crash" class of bug
# we hit with site-checker. For each of the five jobs, validates:
#
#   1. systemd .service and .timer units exist and are properly installed
#   2. Timer is enabled and active (will fire at its scheduled time)
#   3. Deploy directory exists on disk with the .dll present
#   4. appsettings.json AND appsettings.Production.json BOTH exist
#   5. Production overlay has the keys that job actually needs:
#        - ConnectionStrings.DefaultConnection (every job)
#        - SendGrid.ApiKey (newsletter-sender, sponsored-listing-*)
#        - TorProxy.Host (site-checker)
#        - UserAgent.Header (site-checker)
#   6. None of those values are empty strings (the bug we just hit)
#
# Run from the CI directory: ./verify-jobs.sh
# Exits 0 if all jobs pass, non-zero (with red FAIL lines) if anything's wrong.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"

[[ -f "$CONFIG_PATH" ]] || { echo "Missing $CONFIG_PATH" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

UNIT_PREFIX="dm-job-"

C_GREEN=$'\e[32m' C_RED=$'\e[31m' C_YELLOW=$'\e[33m' C_CYAN=$'\e[36m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
PASS=$'\u2713'    # ✓
FAIL=$'\u2717'    # ✗
WARN=$'\u26A0'    # ⚠

# === SSH plumbing (mirrors deploy-jobs.sh) ==================================
SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { echo "SSH_PASSWORD set but sshpass missing." >&2; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o BatchMode=no)
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

ssh_q() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'" 2>/dev/null | tr -d '\r'; }

# === Per-job expectations ====================================================
# Each job lists which top-level config keys MUST exist with non-empty values.
# Anything not listed is allowed to be missing or empty.
declare -A REQUIRED_KEYS=(
    [email-message-maker]="ConnectionStrings.DefaultConnection"
    [newsletter-sender]="ConnectionStrings.DefaultConnection SendGrid.ApiKey SendGrid.SenderEmail"
    [sponsored-listing-opening]="ConnectionStrings.DefaultConnection SendGrid.ApiKey SendGrid.SenderEmail"
    [sponsored-listing-reminder]="ConnectionStrings.DefaultConnection SendGrid.ApiKey SendGrid.SenderEmail"
    [site-checker]="ConnectionStrings.DefaultConnection TorProxy.Host TorProxy.Port UserAgent.Header"
)

declare -A EXPECTED_DLL=(
    [email-message-maker]="DirectoryManager.EmailMessageMaker.dll"
    [newsletter-sender]="DirectoryManager.NewsletterSender.dll"
    [sponsored-listing-opening]="DirectoryManager.SponsoredListingOpening.dll"
    [sponsored-listing-reminder]="DirectoryManager.SponsoredListingReminder.dll"
    [site-checker]="DirectoryManager.SiteChecker.dll"
)

ALL_JOBS=(email-message-maker newsletter-sender sponsored-listing-opening sponsored-listing-reminder site-checker)

# === Check counters ==========================================================
TOTAL_CHECKS=0
TOTAL_FAILS=0
TOTAL_WARNS=0

# Per-job state
JOB_FAILS=0

check() {
    local label="$1" status="$2" detail="${3:-}"
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    case "$status" in
        pass) printf "    ${C_GREEN}%s${C_RESET} %s\n" "$PASS" "$label" ;;
        fail) printf "    ${C_RED}%s${C_RESET} %s${C_GRAY}%s${C_RESET}\n" "$FAIL" "$label" "${detail:+ — $detail}"
              TOTAL_FAILS=$((TOTAL_FAILS + 1))
              JOB_FAILS=$((JOB_FAILS + 1)) ;;
        warn) printf "    ${C_YELLOW}%s${C_RESET} %s${C_GRAY}%s${C_RESET}\n" "$WARN" "$label" "${detail:+ — $detail}"
              TOTAL_WARNS=$((TOTAL_WARNS + 1)) ;;
    esac
}

# === Read a JSON value from the server using sudo cat + jq locally ===========
# Safer than running jq remotely (we don't know if it's installed there).
# Returns the value, or empty string if path doesn't exist.
remote_jq() {
    local remote_file="$1" jq_path="$2"
    local content
    content=$(ssh_q "cat $remote_file" 2>/dev/null) || return 1
    [[ -n "$content" ]] || return 1
    echo "$content" | jq -r "$jq_path // \"\"" 2>/dev/null
}

# === Per-job checks ==========================================================
verify_job() {
    local job="$1"
    local unit="${UNIT_PREFIX}${job}"
    local deploy_path="/var/www/dm-jobs/$job"
    local dll="${EXPECTED_DLL[$job]}"
    local required_keys="${REQUIRED_KEYS[$job]}"

    JOB_FAILS=0
    echo
    echo "${C_CYAN}━━━ $job ━━━${C_RESET}"

    # ---- 1. systemd unit files exist
    if [[ "$(ssh_q "test -f /etc/systemd/system/${unit}.service && echo y || echo n")" == "y" ]]; then
        check "systemd .service unit installed" pass
    else
        check "systemd .service unit installed" fail "/etc/systemd/system/${unit}.service missing"
        return
    fi

    if [[ "$(ssh_q "test -f /etc/systemd/system/${unit}.timer && echo y || echo n")" == "y" ]]; then
        check "systemd .timer unit installed" pass
    else
        check "systemd .timer unit installed" fail "/etc/systemd/system/${unit}.timer missing"
    fi

    # ---- 2. timer enabled + active
    local timer_enabled
    timer_enabled=$(ssh_q "systemctl is-enabled ${unit}.timer 2>/dev/null || true")
    [[ "$timer_enabled" == "enabled" ]] \
        && check "timer is enabled" pass \
        || check "timer is enabled" fail "is-enabled returned: ${timer_enabled:-(empty)}"

    local timer_active
    timer_active=$(ssh_q "systemctl is-active ${unit}.timer 2>/dev/null || true")
    [[ "$timer_active" == "active" ]] \
        && check "timer is active" pass \
        || check "timer is active" fail "is-active returned: ${timer_active:-(empty)}"

    # ---- 3. deploy dir + .dll
    if [[ "$(ssh_q "test -d $deploy_path && echo y || echo n")" == "y" ]]; then
        check "deploy directory exists" pass
    else
        check "deploy directory exists" fail "$deploy_path missing"
        return
    fi

    if [[ "$(ssh_q "test -f $deploy_path/$dll && echo y || echo n")" == "y" ]]; then
        check "main dll present" pass
    else
        check "main dll present" fail "$deploy_path/$dll missing"
    fi

    # ---- 4. config files present
    if [[ "$(ssh_q "test -f $deploy_path/appsettings.json && echo y || echo n")" == "y" ]]; then
        check "appsettings.json present" pass
    else
        check "appsettings.json present" fail "$deploy_path/appsettings.json missing"
    fi

    if [[ "$(ssh_q "test -f $deploy_path/appsettings.Production.json && echo y || echo n")" == "y" ]]; then
        check "appsettings.Production.json present" pass
    else
        check "appsettings.Production.json present" fail "this is the bug we just hit — redeploy with the fixed deploy-jobs.sh"
        return  # No point checking individual keys if the file is missing
    fi

    # ---- 5+6. each required key exists AND is non-empty in Production overlay
    for key_spec in $required_keys; do
        # Convert "ConnectionStrings.DefaultConnection" → ".ConnectionStrings.DefaultConnection"
        local jq_path=".${key_spec}"
        local value
        value=$(remote_jq "$deploy_path/appsettings.Production.json" "$jq_path")

        if [[ -z "$value" ]]; then
            check "$key_spec is set in Production overlay" fail "value is null or empty"
        else
            # Mask sensitive values in output (show first 8 chars + length)
            local display
            case "$key_spec" in
                *Password*|*ApiKey*|*Connection*)
                    display="${value:0:8}... (${#value} chars)" ;;
                *)
                    display="${value:0:50}$([ ${#value} -gt 50 ] && echo '...')" ;;
            esac
            check "$key_spec = ${C_RESET}${display}" pass
        fi
    done

    # ---- 7. timer schedule & last result
    local last_result
    last_result=$(ssh_q "systemctl show ${unit}.service --property=Result --value 2>/dev/null")
    case "$last_result" in
        success|"") check "last invocation result OK" pass ;;
        *)          check "last invocation result OK" warn "Result=$last_result (run: ./deploy-jobs.sh $job --task logs)" ;;
    esac

    if [[ $JOB_FAILS -eq 0 ]]; then
        echo "    ${C_GREEN}── $job: HEALTHY ──${C_RESET}"
    else
        echo "    ${C_RED}── $job: $JOB_FAILS issue(s) ──${C_RESET}"
    fi
}

# === Pre-flight ==============================================================
command -v jq >/dev/null 2>&1 || { echo "jq required locally" >&2; exit 1; }

echo "${C_CYAN}Verifying background jobs on $SSH_TARGET${C_RESET}"
echo "${C_GRAY}(read-only checks — no jobs will be triggered)${C_RESET}"

# Tor reachability — needed by site-checker
echo
echo "${C_CYAN}━━━ pre-flight ━━━${C_RESET}"
local_tor_state=$(ssh_q "systemctl is-active tor@default 2>/dev/null || systemctl is-active tor 2>/dev/null || echo inactive")
case "$local_tor_state" in
    active) check "tor daemon is active (required by site-checker)" pass ;;
    *)      check "tor daemon is active (required by site-checker)" fail "is-active returned: $local_tor_state" ;;
esac

# === Run all jobs ============================================================
for job in "${ALL_JOBS[@]}"; do
    verify_job "$job"
done

# === Summary =================================================================
echo
echo "${C_CYAN}━━━ summary ━━━${C_RESET}"
echo "  Total checks:   $TOTAL_CHECKS"
echo "  ${C_GREEN}Passed:${C_RESET}         $((TOTAL_CHECKS - TOTAL_FAILS - TOTAL_WARNS))"
[[ $TOTAL_WARNS -gt 0 ]] && echo "  ${C_YELLOW}Warnings:${C_RESET}       $TOTAL_WARNS"
[[ $TOTAL_FAILS -gt 0 ]] && echo "  ${C_RED}Failures:${C_RESET}       $TOTAL_FAILS"

echo
if [[ $TOTAL_FAILS -eq 0 ]]; then
    echo "${C_GREEN}All jobs are correctly deployed and ready for their scheduled fires.${C_RESET}"
    exit 0
else
    echo "${C_RED}Some jobs have issues. Fix them before the next scheduled fire.${C_RESET}"
    exit 1
fi
