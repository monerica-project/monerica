#!/usr/bin/env bash
# deploy.sh - DirectoryManager
# Run from the CI folder: ./deploy.sh
#
# Mirrors the original psake CI tool (BuildProject, RestorePackages, RunUnitTests,
# CreatePackage, MigrateDB, SyncWebFiles, smoke-test) but targets Linux + nginx + systemd
# instead of Windows IIS + msdeploy.
#
# Flags:
#   --skip-build      skip restore/build/publish
#   --skip-tests      skip unit tests
#   --skip-migrate    skip EF database migration
#   --ssl             install Let's Encrypt SSL after deploy
#   --task <name>     run a single named task and exit. Tasks:
#                       configs | restore | build | test | publish |
#                       sync | migrate | nginx | smoke | ssl
#
# Targets Ubuntu 24.04 LTS on the server (.NET 10 from Canonical archive).
# Local prerequisites: dotnet, jq, ssh, scp, tar, curl.

set -euo pipefail

# -- Parse flags ---------------------------------------------------------------
SKIP_BUILD=0
SKIP_TESTS=0
SKIP_MIGRATE=0
DO_SSL=0
SINGLE_TASK=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-build)   SKIP_BUILD=1; shift ;;
        --skip-tests)   SKIP_TESTS=1; shift ;;
        --skip-migrate) SKIP_MIGRATE=1; shift ;;
        --ssl)          DO_SSL=1; shift ;;
        --task)         SINGLE_TASK="${2:-}"; shift 2 ;;
        -h|--help)
            sed -n '2,18p' "$0"
            exit 0
            ;;
        *) echo "Unknown flag: $1" >&2; exit 1 ;;
    esac
done

# -- Load config ---------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"

if [[ ! -f "$CONFIG_PATH" ]]; then
    echo "deploy-config.sh not found next to deploy.sh" >&2
    exit 1
fi
# shellcheck disable=SC1090
source "$CONFIG_PATH"

# -- Tool checks ---------------------------------------------------------------
for tool in dotnet jq ssh scp tar curl; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Required tool missing: $tool" >&2
        exit 1
    fi
done

# -- Helpers (psake's FormatTaskName / Write-Host equivalents) ------------------
C_CYAN=$'\e[36m'; C_GREEN=$'\e[32m'; C_YELLOW=$'\e[33m'; C_RED=$'\e[31m'; C_GRAY=$'\e[90m'; C_RESET=$'\e[0m'

write_task() { echo; echo "${C_CYAN}----- Task: $1 -----${C_RESET}"; }
write_step() { echo; echo "${C_CYAN}==> $1${C_RESET}"; }
write_ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
write_warn() { echo "    ${C_YELLOW}WARN: $1${C_RESET}"; }
write_err()  { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

# Build SSH command prefix
SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    if ! command -v sshpass >/dev/null 2>&1; then
        echo "SSH_PASSWORD is set but sshpass is not installed." >&2
        echo "Install: sudo apt install -y sshpass   (or remove SSH_PASSWORD to use SSH key auth)" >&2
        exit 1
    fi
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi

SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o ServerAliveInterval=30)

ssh_run()        { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "$1"; }
ssh_run_ignore() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "$1" || true; }
ssh_query()      { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "$1" 2>/dev/null | tr -d '\r' || true; }
scp_send()       { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" -r "$1" "$SSH_USER@$SSH_HOST:$2"; }

run_remote_script() {
    local local_path="$1"
    local remote_path="$2"
    scp_send "$local_path" "$remote_path"
    ssh_run "chmod +x $remote_path && bash $remote_path"
}

# -- Resolved paths (psake used $CIRoot relative — bash uses $SCRIPT_DIR) -------
WEB_PROJECT_DIR="$SCRIPT_DIR/$WEB_PROJECT_SOURCE_PATH"
DATA_PROJECT_DIR="$SCRIPT_DIR/$DATA_PROJECT_SOURCE_PATH"
TEST_SOLUTION_PATH="$SCRIPT_DIR/$TEST_SOLUTION_SOURCE_PATH"
PUBLISH_OUT="$SCRIPT_DIR/../publish/web"
WEB_APPSETTINGS="$WEB_PROJECT_DIR/appsettings.json"
DATA_APPSETTINGS="$DATA_PROJECT_DIR/appsettings.json"

# ===============================================================================
# TASKS — each maps to a psake task in the original script
# ===============================================================================

task_set_configs() {
    write_task "SetConfigs"

    if [[ -f "$WEB_APPSETTINGS" ]]; then
        local tmp="$WEB_APPSETTINGS.tmp"
        jq \
            --arg conn      "$DB_CONNECTION_STRING" \
            --arg neuId     "$NEUTRINO_API_USER_ID" \
            --arg neuKey    "$NEUTRINO_API_API_KEY" \
            --arg domain    "$CUSTOM_DOMAIN" \
            --arg proto     "$REQUEST_PROTOCOL" \
            '
            .ConnectionStrings.DefaultConnection = $conn
            | (if .NeutrinoApi then .NeutrinoApi.UserId = $neuId | .NeutrinoApi.ApiKey = $neuKey else . end)
            | (if .Site then .Site.CustomDomain = $domain | .Site.RequestProtocol = $proto else . end)
            ' "$WEB_APPSETTINGS" > "$tmp"
        mv "$tmp" "$WEB_APPSETTINGS"
        write_ok "Web appsettings updated: $WEB_APPSETTINGS"
    else
        write_warn "Web appsettings not found at $WEB_APPSETTINGS"
    fi

    if [[ -f "$DATA_APPSETTINGS" ]]; then
        local tmp="$DATA_APPSETTINGS.tmp"
        jq --arg conn "$DB_CONNECTION_STRING" \
           '.ConnectionStrings.DefaultConnection = $conn' \
           "$DATA_APPSETTINGS" > "$tmp"
        mv "$tmp" "$DATA_APPSETTINGS"
        write_ok "Data appsettings updated: $DATA_APPSETTINGS"
    else
        write_warn "Data appsettings not found at $DATA_APPSETTINGS"
    fi
}

task_restore_packages() {
    write_task "RestorePackages"

    if [[ -d "$PUBLISH_OUT" ]]; then
        echo "Deleting files at: '$PUBLISH_OUT'..."
        rm -rf "$PUBLISH_OUT"
    fi

    pushd "$WEB_PROJECT_DIR" >/dev/null
    dotnet msbuild /t:Restore /p:Configuration="$BUILD_CONFIGURATION"
    popd >/dev/null

    write_ok "Packages restored"
}

task_build_project() {
    write_task "BuildProject"

    pushd "$WEB_PROJECT_DIR" >/dev/null
    dotnet restore
    popd >/dev/null

    write_ok "Project restored"
}

task_run_unit_tests() {
    write_task "RunUnitTests"

    if [[ -f "$TEST_SOLUTION_PATH" ]]; then
        echo "Test target: $TEST_SOLUTION_PATH"
        dotnet test "$TEST_SOLUTION_PATH" --configuration "$BUILD_CONFIGURATION"
        write_ok "Tests passed"
    else
        write_warn "Test solution not found at $TEST_SOLUTION_PATH — skipping tests"
    fi
}

task_create_package() {
    write_task "CreatePackage"

    mkdir -p "$PUBLISH_OUT"

    pushd "$WEB_PROJECT_DIR" >/dev/null

    echo "Packaging..."
    # Linux runtime, framework-dependent. Original psake used self-contained win-x64;
    # for Linux + installed runtime, framework-dependent is the right tradeoff
    # (smaller package, faster deploy, matches MoneroMarketCap deploy pattern).
    dotnet publish \
        --framework "$DOTNET_FRAMEWORK" \
        --output "$PUBLISH_OUT" \
        --configuration "$BUILD_CONFIGURATION" \
        --runtime "$DOTNET_RUNTIME" \
        --self-contained false

    popd >/dev/null

    write_ok "Package built: $PUBLISH_OUT"
}

task_migrate_db() {
    write_task "MigrateDB"

    # Two modes via MIGRATION_MODE in deploy-config.sh:
    #   server (default) — run --migrate-only on the server before service start.
    #                      Same pattern as your moneromarketcap deploy. Requires the
    #                      web app to support an --migrate-only entrypoint.
    #   local            — run `dotnet ef database update` against the prod DB from
    #                      the deploy machine. Original psake behaviour. Only works
    #                      if the prod DB is reachable from your local IP.
    #   skip             — don't run migrations.

    case "${MIGRATION_MODE:-server}" in
        local)
            pushd "$DATA_PROJECT_DIR" >/dev/null
            dotnet ef database update --verbose
            popd >/dev/null
            write_ok "Migrations applied (local connection)"
            ;;
        server)
            local migrate_script="/tmp/dm-migrate.sh.local"
            cat > "$migrate_script" <<EOF
#!/bin/bash
set -e
export ConnectionStrings__DefaultConnection='$DB_CONNECTION_STRING'
export ASPNETCORE_ENVIRONMENT=Production
cd $DEPLOY_PATH
echo 'Running migrations...'
dotnet $WEB_DLL_NAME --migrate-only
echo 'Migrations complete.'
EOF
            run_remote_script "$migrate_script" "/tmp/dm-migrate.sh"
            write_ok "Migrations applied (server-side, --migrate-only)"
            ;;
        skip)
            write_warn "MIGRATION_MODE=skip; not applying migrations"
            ;;
        *)
            write_err "Unknown MIGRATION_MODE: ${MIGRATION_MODE} (expected: server|local|skip)"
            exit 1
            ;;
    esac
}

# -- Maintenance page ----------------------------------------------------------
read -r -d '' MAINTENANCE_HTML <<'HTML' || true
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="refresh" content="15">
  <title>Updating - DirectoryManager</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #0f0f0f;
      color: #e0e0e0;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }
    .card { text-align: center; padding: 3rem 2.5rem; max-width: 440px; }
    .icon { font-size: 2.8rem; margin-bottom: 1.25rem; display: inline-block; animation: spin 3s linear infinite; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    h1 { font-size: 1.5rem; font-weight: 600; margin-bottom: 0.75rem; color: #ff6600; }
    p  { font-size: 0.95rem; line-height: 1.6; color: #aaa; }
    .note { margin-top: 1.75rem; font-size: 0.8rem; color: #555; }
  </style>
</head>
<body>
  <div class="card">
    <div class="icon">&#9881;</div>
    <h1>Updating in progress</h1>
    <p>The site is being updated and will be back shortly.</p>
    <p class="note">This page refreshes automatically every 15 seconds.</p>
  </div>
</body>
</html>
HTML

write_nginx_maint_ssl() {
cat <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    location / {
        root /var/www;
        try_files /maintenance-$APP_NAME.html =503;
        add_header Retry-After 30;
    }
}
EOF
}

write_nginx_maint_plain() {
cat <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    location / {
        root /var/www;
        try_files /maintenance-$APP_NAME.html =503;
        add_header Retry-After 30;
    }
}
EOF
}

write_nginx_proxy_ssl() {
cat <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass         http://172.17.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF
}

write_nginx_proxy_plain() {
cat <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;

    location / {
        proxy_pass         http://172.17.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF
}

enable_maintenance_page() {
    write_step "Enabling maintenance page"

    local html_file="/tmp/maintenance-$APP_NAME.html.local"
    printf '%s\n' "$MAINTENANCE_HTML" > "$html_file"
    scp_send "$html_file" "/tmp/maintenance-$APP_NAME.html"
    ssh_run "docker exec nginx mkdir -p /var/www"
    ssh_run "docker cp /tmp/maintenance-$APP_NAME.html nginx:/var/www/maintenance-$APP_NAME.html"

    local cert_now
    cert_now=$(ssh_query "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no")

    local m_file="/tmp/$APP_NAME.maint.conf"
    if [[ "$cert_now" == "yes" ]]; then
        write_nginx_maint_ssl > "$m_file"
    else
        write_nginx_maint_plain > "$m_file"
    fi

    scp_send "$m_file" "/tmp/$APP_NAME.conf"
    ssh_run "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
    ssh_run "docker exec nginx nginx -s reload"
    write_ok "Maintenance page live"
}

# -- Server bootstrap (idempotent) ---------------------------------------------
task_bootstrap_server() {
    write_task "BootstrapServer"

    ssh_run "apt-get update -q"
    ssh_run "command -v dotnet >/dev/null 2>&1 || apt-get install -y aspnetcore-runtime-10.0"
    ssh_run "command -v psql >/dev/null 2>&1 || (apt-get install -y postgresql postgresql-contrib && systemctl enable postgresql && systemctl start postgresql)"
    ssh_run_ignore "systemctl start postgresql 2>/dev/null || true"
    ssh_run "mkdir -p $DEPLOY_PATH"
    ssh_run_ignore "ufw allow $APP_PORT/tcp 2>/dev/null || true"

    write_ok "Server dependencies ready"
}

task_setup_postgres() {
    write_task "SetupPostgres"

    local pg_script="/tmp/dm-pg-setup.sh.local"
    cat > "$pg_script" <<EOF
#!/bin/bash
set -e
sudo -u postgres psql -c "CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';" 2>/dev/null || true
sudo -u postgres psql -c "CREATE DATABASE $DB_NAME OWNER $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -d $DB_NAME -c "GRANT ALL ON SCHEMA public TO $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -d $DB_NAME -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $DB_USER;" 2>/dev/null || true
echo 'DB setup complete'
EOF
    run_remote_script "$pg_script" "/tmp/dm-pg-setup.sh"

    write_ok "Database ready"
}

# -- Linux equivalent of SyncWebFiles (msdeploy → tar+scp+systemd) -------------
task_sync_web_files() {
    write_task "SyncWebFiles"

    enable_maintenance_page
    ssh_run_ignore "systemctl stop $APP_NAME 2>/dev/null || true"

    local web_tar="/tmp/$APP_NAME.tar.gz.local"
    tar -czf "$web_tar" -C "$PUBLISH_OUT" .

    scp_send "$web_tar" "/tmp/$APP_NAME.tar.gz"
    ssh_run "mkdir -p $DEPLOY_PATH && tar -xzf /tmp/$APP_NAME.tar.gz -C $DEPLOY_PATH && rm /tmp/$APP_NAME.tar.gz"
    ssh_run "chown -R www-data:www-data $DEPLOY_PATH && chmod -R 755 $DEPLOY_PATH"

    write_ok "Files synced to $DEPLOY_PATH"

    # Migrations BEFORE starting the new service.
    if [[ $SKIP_MIGRATE -eq 0 ]]; then
        task_migrate_db
    else
        write_warn "Skipping migrations (--skip-migrate)"
    fi

    # Write systemd unit and start service
    local svc_file="/tmp/$APP_NAME.service.local"
    cat > "$svc_file" <<EOF
[Unit]
Description=DirectoryManager Web ($DOMAIN)
After=network.target postgresql.service

[Service]
WorkingDirectory=$DEPLOY_PATH
ExecStart=/usr/bin/dotnet $DEPLOY_PATH/$WEB_DLL_NAME
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF
    scp_send "$svc_file" "/etc/systemd/system/$APP_NAME.service"
    ssh_run "systemctl daemon-reload && systemctl enable $APP_NAME && systemctl restart $APP_NAME"

    write_ok "Service started: $APP_NAME"
}

task_smoke_test() {
    write_task "DeployWebApp (smoke test)"

    local url="${REQUEST_PROTOCOL}${WEB_APP_HOST}"
    echo "Deployment completed, requesting page '$url'..."

    local max=10
    local i
    for ((i=1; i<=max; i++)); do
        local code
        code=$(curl -sL -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || echo "000")
        if [[ "$code" == "200" ]]; then
            write_ok "HTTP 200 — COMPLETE!"
            return
        fi
        echo "    Attempt $i/$max - HTTP $code, retrying in 5s..."
        sleep 5
    done

    write_err "Site did not return HTTP 200 after $max attempts"
    write_err "Check logs: ssh $SSH_USER@$SSH_HOST journalctl -u $APP_NAME -n 80 --no-pager"
    exit 1
}

# -- Nginx config (replaces maintenance page after successful deploy) ----------
task_configure_nginx() {
    write_task "ConfigureNginx"

    local cert_exists
    cert_exists=$(ssh_query "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no")

    local nginx_file="/tmp/$APP_NAME.nginx.conf.local"
    if [[ "$cert_exists" == "yes" ]]; then
        ssh_run "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
        ssh_run "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
        ssh_run "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
        ssh_run "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"
        write_nginx_proxy_ssl > "$nginx_file"
        write_ok "Nginx will be configured for HTTPS"
    else
        write_nginx_proxy_plain > "$nginx_file"
        write_ok "Nginx will be configured for HTTP (run with --ssl to enable HTTPS)"
    fi

    scp_send "$nginx_file" "/tmp/$APP_NAME.conf"
    ssh_run "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
    ssh_run "docker exec nginx nginx -s reload"
    write_ok "Nginx reloaded"
}

task_install_ssl() {
    write_task "InstallSSL"

    ssh_run "apt-get install -y certbot"

    local ssl_script="/tmp/dm-ssl-setup.sh.local"
    cat > "$ssl_script" <<EOF
#!/bin/bash
set -e
echo 'Stopping docker nginx...'
docker stop nginx
sleep 2
echo 'Getting cert...'
certbot certonly --standalone -d $DOMAIN --non-interactive --agree-tos -m admin@$DOMAIN
echo 'Starting docker nginx...'
docker start nginx
sleep 2
echo 'Done'
EOF
    run_remote_script "$ssl_script" "/tmp/dm-ssl-setup.sh"
    write_ok "Certificate obtained"

    task_configure_nginx
}

# ===============================================================================
# Single-task mode (psake equivalent: ./CI.bat TaskName)
# ===============================================================================
if [[ -n "$SINGLE_TASK" ]]; then
    case "$SINGLE_TASK" in
        configs)  task_set_configs ;;
        restore)  task_restore_packages ;;
        build)    task_build_project ;;
        test)     task_run_unit_tests ;;
        publish)  task_create_package ;;
        sync)     task_sync_web_files ;;
        migrate)  task_migrate_db ;;
        nginx)    task_configure_nginx ;;
        smoke)    task_smoke_test ;;
        ssl)      task_install_ssl ;;
        *) echo "Unknown task: $SINGLE_TASK" >&2; exit 1 ;;
    esac
    exit 0
fi


task_tor_migrate() {
    write_task "TorMigrate"

    if [[ ! -f "$TOR_KEYS_DIR/hs_ed25519_secret_key" ]]; then
        write_err "Tor keys not found at $TOR_KEYS_DIR/"
        write_err "Expected: hostname, hs_ed25519_public_key, hs_ed25519_secret_key"
        exit 1
    fi

    ssh_run "apt-get install -y tor && systemctl enable tor"
    ssh_run "mkdir -p /var/lib/tor/$APP_NAME && chown -R debian-tor:debian-tor /var/lib/tor/$APP_NAME && chmod 700 /var/lib/tor/$APP_NAME"

    scp_send "$TOR_KEYS_DIR/hostname"               "/tmp/tor-hostname"
    scp_send "$TOR_KEYS_DIR/hs_ed25519_public_key"  "/tmp/tor-pub"
    scp_send "$TOR_KEYS_DIR/hs_ed25519_secret_key"  "/tmp/tor-sec"

    ssh_run "mv /tmp/tor-hostname /var/lib/tor/$APP_NAME/hostname && \
             mv /tmp/tor-pub /var/lib/tor/$APP_NAME/hs_ed25519_public_key && \
             mv /tmp/tor-sec /var/lib/tor/$APP_NAME/hs_ed25519_secret_key && \
             chown debian-tor:debian-tor /var/lib/tor/$APP_NAME/* && \
             chmod 600 /var/lib/tor/$APP_NAME/hs_ed25519_secret_key && \
             chmod 644 /var/lib/tor/$APP_NAME/hs_ed25519_public_key /var/lib/tor/$APP_NAME/hostname"

    ssh_run "if ! grep -q 'HiddenServiceDir /var/lib/tor/$APP_NAME' /etc/tor/torrc; then
                echo '' >> /etc/tor/torrc
                echo '# $APP_NAME hidden service' >> /etc/tor/torrc
                echo 'HiddenServiceDir /var/lib/tor/$APP_NAME/' >> /etc/tor/torrc
                echo 'HiddenServicePort 80 127.0.0.1:$APP_PORT' >> /etc/tor/torrc
            fi"

    ssh_run "systemctl restart tor"
    sleep 3

    local onion
    onion=$(ssh_query "cat /var/lib/tor/$APP_NAME/hostname")
    write_ok "Tor hidden service active: $onion"
}

# ===============================================================================
# Full deploy pipeline (psake equivalent: DeployWebApp's depends chain)
#   SetConfigs → RestorePackages → BuildProject → RunUnitTests → CreatePackage
#   → BootstrapServer → SetupPostgres → SyncWebFiles (which runs MigrateDB inline)
#   → ConfigureNginx → SmokeTest
# ===============================================================================

task_set_configs

if [[ $SKIP_BUILD -eq 0 ]]; then
    task_restore_packages
    task_build_project

    if [[ $SKIP_TESTS -eq 0 ]]; then
        task_run_unit_tests
    else
        write_warn "Skipping unit tests (--skip-tests)"
    fi

    task_create_package
else
    write_warn "Skipping build/restore/test/publish (--skip-build)"
    if [[ ! -d "$PUBLISH_OUT" ]]; then
        write_err "--skip-build set but $PUBLISH_OUT doesn't exist; nothing to deploy"
        exit 1
    fi
fi

task_bootstrap_server
task_setup_postgres
task_sync_web_files
task_configure_nginx

if [[ $DO_SSL -eq 1 ]]; then
    task_install_ssl
fi

task_smoke_test

# -- Done ----------------------------------------------------------------------
echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Deployment complete!${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo " Site:   ${REQUEST_PROTOCOL}${WEB_APP_HOST}"
echo
echo " ${C_GRAY}Useful commands:${C_RESET}"
echo "   ${C_GRAY}Status: ssh $SSH_USER@$SSH_HOST systemctl status $APP_NAME${C_RESET}"
echo "   ${C_GRAY}Logs:   ssh $SSH_USER@$SSH_HOST journalctl -u $APP_NAME -f${C_RESET}"
echo
