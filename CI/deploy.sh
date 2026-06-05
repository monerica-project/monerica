#!/usr/bin/env bash
# deploy.sh — DirectoryManager multi-service Linux deploy (SQL Server in Docker)
#
# === ROUTINE DEPLOY (run for every code change) =============================
#   ./deploy.sh                     # deploy 'web' (default)
#   ./deploy.sh web                 # deploy single service
#   ./deploy.sh web,api             # deploy multiple
#   ./deploy.sh all                 # deploy every services/*.conf
#   ./deploy.sh web --skip-build    # skip restore/build/publish
#   ./deploy.sh web --skip-tests    # skip unit tests
#   ./deploy.sh web --skip-migrate  # skip EF migrations
#
# Routine deploy runs: configs → restore → build → test → publish → sync
#                    → migrate → nginx → smoke. Tor and SSL are NOT touched.
#
# === ONE-TIME SETUP (run once per server, then forget) ======================
#   ./deploy.sh web --task bootstrap   # install dotnet, docker, base nginx
#   ./deploy.sh web --task mssql       # install + configure SQL Server
#   ./deploy.sh web --task ssl         # issue Let's Encrypt cert (auto-renews)
#   ./deploy.sh web --task tor         # install tor, set up onion service
#
# === ONE-OFF TASKS (run individually as needed) =============================
#   ./deploy.sh web --task <name>   where <name> is one of:
#     configs | restore | build | test | publish | sync | migrate |
#     nginx | smoke | ssl | tor | bootstrap | mssql |
#     maintenance-on | maintenance-off
#
# Local prereqs: dotnet, jq, ssh, scp, tar, curl
# Server: Ubuntu 22.04+ with docker (mssql container), .NET 10, host nginx
# Service config: services/<name>.conf

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="$SCRIPT_DIR/deploy-config.sh"
SERVICES_DIR="$SCRIPT_DIR/services"

# === Args ====================================================================
SKIP_BUILD=0
SKIP_TESTS=0
SKIP_MIGRATE=0
DO_SSL=0
DO_TOR=0
SINGLE_TASK=""
SERVICE_LIST="web"

if [[ $# -gt 0 && "${1:0:2}" != "--" ]]; then
    SERVICE_LIST="$1"
    shift
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-build)    SKIP_BUILD=1; shift ;;
        --skip-tests)    SKIP_TESTS=1; shift ;;
        --skip-migrate)  SKIP_MIGRATE=1; shift ;;
        --ssl)           DO_SSL=1; shift ;;
        --tor)           DO_TOR=1; shift ;;
        --task)          SINGLE_TASK="${2:-}"; shift 2 ;;
        -h|--help)       sed -n '2,28p' "$0"; exit 0 ;;
        *) echo "Unknown flag: $1" >&2; exit 1 ;;
    esac
done

if [[ "$SERVICE_LIST" == "all" ]]; then
    SERVICES=()
    for f in "$SERVICES_DIR"/*.conf; do
        [[ -f "$f" ]] && SERVICES+=("$(basename "$f" .conf)")
    done
else
    IFS=',' read -ra SERVICES <<< "$SERVICE_LIST"
fi
[[ ${#SERVICES[@]} -gt 0 ]] || { echo "No services. Add services/<name>.conf or pass a service name." >&2; exit 1; }

# === Tool checks =============================================================
for tool in dotnet jq ssh scp tar curl; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool missing: $tool" >&2; exit 1; }
done

# === Source shared config ====================================================
[[ -f "$CONFIG_PATH" ]] || { echo "Missing $CONFIG_PATH" >&2; exit 1; }
# shellcheck disable=SC1090
source "$CONFIG_PATH"

# === Console helpers =========================================================
C_CYAN=$'\e[36m' C_GREEN=$'\e[32m' C_YELLOW=$'\e[33m' C_RED=$'\e[31m' C_GRAY=$'\e[90m' C_RESET=$'\e[0m'
CURRENT_SERVICE="<global>"
write_task() { echo; echo "${C_CYAN}----- [$CURRENT_SERVICE] Task: $1 -----${C_RESET}"; }
write_step() { echo "${C_CYAN}==> $1${C_RESET}"; }
write_ok()   { echo "    ${C_GREEN}OK: $1${C_RESET}"; }
write_warn() { echo "    ${C_YELLOW}WARN: $1${C_RESET}"; }
write_err()  { echo "    ${C_RED}ERR: $1${C_RESET}" >&2; }

# === SSH helpers =============================================================
SSH_PREFIX=()
if [[ -n "${SSH_PASSWORD:-}" ]]; then
    command -v sshpass >/dev/null 2>&1 || { echo "SSH_PASSWORD set but sshpass missing. Install: sudo apt install sshpass" >&2; exit 1; }
    SSH_PREFIX=(sshpass -p "$SSH_PASSWORD")
fi
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ConnectTimeout=10 -o ServerAliveInterval=30)

# If SSH_USER is blank, use SSH_HOST as-is so ~/.ssh/config aliases work fully.
SSH_TARGET="${SSH_USER:+$SSH_USER@}$SSH_HOST"

ssh_run()        { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'"; }
ssh_run_ignore() { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'" || true; }
ssh_query()      { "${SSH_PREFIX[@]}" ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "sudo bash -c '$1'" 2>/dev/null | tr -d '\r' || true; }
scp_send()       { "${SSH_PREFIX[@]}" scp "${SSH_OPTS[@]}" -r "$1" "$SSH_TARGET:$2"; }

run_remote_script() {
    local local_path="$1" remote_path="$2"
    scp_send "$local_path" "$remote_path"
    ssh_run "chmod +x $remote_path && sudo bash $remote_path"
}

# Inside the mssql container the tools live at /opt/mssql-tools18/bin/sqlcmd
# (-C trusts the self-signed server cert; -b makes errors propagate).
mssql_exec() {
    local sql="$1"
    ssh_run "docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -Q \"$sql\""
}

mssql_exec_file() {
    local local_sql="$1"
    local container_path="/tmp/$(basename "$local_sql" .local)"
    scp_send "$local_sql" "$container_path"
    ssh_run "docker cp $container_path mssql:$container_path"
    ssh_run "docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -b -i $container_path"
    ssh_run "docker exec mssql rm -f $container_path"
}

# === Nginx config writers ====================================================
nginx_server_names() {
    local names="$DOMAIN www.$DOMAIN"
    [[ -n "${STAGING_DOMAIN:-}" ]] && names="$names $STAGING_DOMAIN"
    echo "$names"
}

write_nginx_maint_ssl() {
cat <<EOF
server {
    listen 80;
    server_name $(nginx_server_names);
    location /.well-known/acme-challenge/ { root /var/www; }
    location / { return 301 https://\$host\$request_uri; }
}
server {
    listen 443 ssl;
    server_name $(nginx_server_names);
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
    server_name $(nginx_server_names);
    location /.well-known/acme-challenge/ { root /var/www; }
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
    server_name $(nginx_server_names);
    location /.well-known/acme-challenge/ { root /var/www; }
    location / { return 301 https://\$host\$request_uri; }
}
server {
    listen 443 ssl;
    server_name $(nginx_server_names);
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    add_header Strict-Transport-Security "max-age=31536000" always;

    client_max_body_size 50M;
    proxy_read_timeout 300s;

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
    server_name $(nginx_server_names);
    location /.well-known/acme-challenge/ { root /var/www; }
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

# === Maintenance page ========================================================
maintenance_html() {
cat <<HTML
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="refresh" content="15">
  <title>Updating - $DOMAIN</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      min-height: 100vh; display: flex; align-items: center; justify-content: center;
      background: #0f0f0f; color: #e0e0e0;
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
}

enable_maintenance_page() {
    write_step "Enabling maintenance page"

    local html_file="/tmp/maintenance-$APP_NAME.html.local"
    maintenance_html > "$html_file"
    scp_send "$html_file" "/tmp/maintenance-$APP_NAME.html"
    ssh_run "mkdir -p /var/www/maintenance && mv /tmp/maintenance-$APP_NAME.html /var/www/maintenance/$APP_NAME.html"

    local cert_now
    cert_now=$(ssh_query "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no")

    local m_file="/tmp/$APP_NAME.maint.conf.local"
    if [[ "$cert_now" == "yes" ]]; then
        cat > "$m_file" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / { return 301 https://\$host\$request_uri; }
}
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    location / {
        root /var/www/maintenance;
        try_files /$APP_NAME.html =503;
        add_header Retry-After 30 always;
    }
}
EOF
    else
        cat > "$m_file" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / {
        root /var/www/maintenance;
        try_files /$APP_NAME.html =503;
        add_header Retry-After 30 always;
    }
}
EOF
    fi

    scp_send "$m_file" "/tmp/$APP_NAME.conf"
    ssh_run "mv /tmp/$APP_NAME.conf /etc/nginx/sites-available/$APP_NAME.conf"
    ssh_run "ln -sf /etc/nginx/sites-available/$APP_NAME.conf /etc/nginx/sites-enabled/$APP_NAME.conf"
    ssh_run "nginx -t && systemctl reload nginx"
    write_ok "Maintenance page live for $DOMAIN"
}

# ============================================================================
# GLOBAL TASKS
# ============================================================================

task_bootstrap_server() {
    write_task "BootstrapServer"
    ssh_run "apt-get update -q"
    ssh_run "command -v dotnet >/dev/null 2>&1 || apt-get install -y aspnetcore-runtime-10.0"
    ssh_run "command -v docker >/dev/null 2>&1 || (apt-get install -y docker.io && systemctl enable docker && systemctl start docker)"

    # Make sure an nginx container exists
    if ! ssh_query "docker ps -a --format '{{.Names}}'" | grep -wq nginx; then
        write_step "Starting nginx container"
        ssh_run "docker run -d --name nginx --restart=always -p 80:80 -p 443:443 nginx:alpine"
    fi
    write_ok "Server dependencies ready"
}

task_setup_mssql() {
    write_task "SetupMSSQL"

    # 1. Container
    if ! ssh_query "docker ps -a --format '{{.Names}}'" | grep -wq mssql; then
        write_step "Starting mssql container ($MSSQL_PID edition)"
        ssh_run "docker volume create mssql-data 2>/dev/null || true"
        ssh_run "docker volume create mssql-backup 2>/dev/null || true"
        ssh_run "docker run -d --name mssql --restart=always \
            -e 'ACCEPT_EULA=Y' \
            -e 'MSSQL_SA_PASSWORD=$MSSQL_SA_PASSWORD' \
            -e 'MSSQL_PID=$MSSQL_PID' \
            -p 127.0.0.1:1433:1433 \
            -v mssql-data:/var/opt/mssql \
            -v mssql-backup:/var/opt/mssql/backup \
            $MSSQL_IMAGE"
    fi

    # 2. Wait for the server to be ready (first boot can take 30-60s)
    write_step "Waiting for SQL Server to accept connections"
    if ! ssh_run "for i in \$(seq 1 60); do
        docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$MSSQL_SA_PASSWORD' -C -Q 'SELECT 1' >/dev/null 2>&1 && exit 0
        sleep 2
    done
    exit 1"; then
        write_err "SQL Server didn't come up. Check: docker logs mssql"
        exit 1
    fi

    # 3. Database + app login (idempotent)
    local sql_file="/tmp/dm-mssql-setup.sql.local"
    cat > "$sql_file" <<EOF
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '$DB_NAME')
    CREATE DATABASE [$DB_NAME];
GO
EOF

    case "${DB_USER,,}" in
        sa|dbo)
            write_step "DB_USER='$DB_USER' is built-in; skipping login/user creation"
            ;;
        *)
            cat >> "$sql_file" <<EOF
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '$DB_USER')
    CREATE LOGIN [$DB_USER] WITH PASSWORD = '$DB_PASSWORD', CHECK_POLICY = OFF;
GO
USE [$DB_NAME];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$DB_USER')
    CREATE USER [$DB_USER] FOR LOGIN [$DB_USER];
ALTER ROLE db_owner ADD MEMBER [$DB_USER];
GO
EOF
            ;;
    esac

    mssql_exec_file "$sql_file"
    write_ok "SQL Server: database '$DB_NAME' and user '$DB_USER' ready"
}

# ============================================================================
# PER-SERVICE TASKS
# ============================================================================

task_set_configs() {
    write_task "SetConfigs"

    # Production overrides live in appsettings.Production.json — generated here,
    # bundled into the publish output, deployed to the server. Never committed.
    # appsettings.json stays clean in git.
    #
    # WHAT GOES IN HERE (and what doesn't):
    #
    # - ConnectionStrings: yes — different per environment (dev → prod).
    #
    # - Site (CustomDomain, RequestProtocol): yes — environment-specific
    #   (dev uses localhost:5xxxx, prod uses monerica.com).
    #
    # - SendGrid keys: YES. The web app now reads SendGrid creds from the
    #   "SendGrid" config section at startup (see Web/Extensions/ServiceExtensions.cs:
    #   config.GetSection("SendGrid").Get<SendGridConfig>()). Source of truth is
    #   deploy-config.sh (SENDGRID_API_KEY / SENDGRID_SENDER_EMAIL /
    #   SENDGRID_SENDER_NAME); never committed. Rotate by editing deploy-config.sh
    #   and re-running this task.
    #
    # - Neutrino API: NO. Removed — service is no longer used.
    local prod_settings="$WEB_PROJECT_DIR/appsettings.Production.json"
    jq -n \
        --arg conn    "$DB_CONNECTION_STRING" \
        --arg domain  "${CUSTOM_DOMAIN:-}" \
        --arg proto   "${REQUEST_PROTOCOL:-https://}" \
        --arg sgkey   "${SENDGRID_API_KEY:-}" \
        --arg sgemail "${SENDGRID_SENDER_EMAIL:-}" \
        --arg sgname  "${SENDGRID_SENDER_NAME:-}" \
        '{
            ConnectionStrings: { DefaultConnection: $conn },
            Site:              { CustomDomain: $domain, RequestProtocol: $proto },
            SendGrid:          { ApiKey: $sgkey, SenderEmail: $sgemail, SenderName: $sgname }
        }' > "$prod_settings"
    write_ok "Wrote $prod_settings"
}

task_restore_packages() {
    write_task "RestorePackages"
    [[ -d "$PUBLISH_OUT" ]] && rm -rf "$PUBLISH_OUT"

    # Nuke ALL bin/obj across the solution to avoid stale incremental-build artifacts.
    # Without this, dotnet's incremental cache can ship old binaries when files
    # appear "unchanged" by mtime even after edits.
    local solution_root
    solution_root="$(cd "$WEB_PROJECT_DIR/../.." && pwd)"
    find "$solution_root" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
    write_step "Cleaned bin/obj across solution"

    pushd "$WEB_PROJECT_DIR" >/dev/null
    dotnet restore
    popd >/dev/null
    write_ok "Packages restored"
}

task_build_project() {
    write_task "BuildProject"
    pushd "$WEB_PROJECT_DIR" >/dev/null
    # --no-incremental forces a full compile. Belt-and-suspenders alongside
    # the bin/obj wipe in restore — guarantees the dll bundled into publish
    # actually contains the latest source.
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
    echo "Test target: $TEST_SOLUTION_PATH"
    # No --no-build: test projects build fine without the linux-x64 RID
    # the web project uses, and rebuilding tests is cheap.
    dotnet test "$TEST_SOLUTION_PATH" \
        --configuration "$BUILD_CONFIGURATION" \
        --logger "console;verbosity=normal" \
        --nologo
    write_ok "Tests passed"
}

task_create_package() {
    write_task "CreatePackage"
    mkdir -p "$PUBLISH_OUT"
    pushd "$WEB_PROJECT_DIR" >/dev/null
    dotnet publish \
        --framework "$DOTNET_FRAMEWORK" \
        --output "$PUBLISH_OUT" \
        --configuration "$BUILD_CONFIGURATION" \
        --runtime "$DOTNET_RUNTIME" \
        --self-contained false \
        --no-build
    popd >/dev/null
    write_ok "Package built: $PUBLISH_OUT"
}

task_migrate_db() {
    write_task "MigrateDB"
    [[ "${USES_DB:-0}" -eq 1 ]] || { write_warn "Service has USES_DB=0; skipping"; return; }

    case "${MIGRATION_MODE:-server}" in
        local)
            pushd "$DATA_PROJECT_DIR" >/dev/null
            dotnet ef database update --verbose
            popd >/dev/null
            write_ok "Migrations applied (local connection)"
            ;;
        server)
            local migrate_script="/tmp/dm-migrate-$APP_NAME.sh.local"
         cat > "$migrate_script" <<EOF
#!/bin/bash
set -e
export ConnectionStrings__DefaultConnection='$DB_CONNECTION_STRING'
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://127.0.0.1:$APP_PORT
cd $DEPLOY_PATH
echo 'Running migrations...'
dotnet $WEB_DLL_NAME --migrate-only
echo 'Migrations complete.'
EOF
            run_remote_script "$migrate_script" "/tmp/dm-migrate-$APP_NAME.sh"
            write_ok "Migrations applied (server-side --migrate-only)"
            ;;
        skip)
            write_warn "MIGRATION_MODE=skip"
            ;;
        *)
            write_err "Unknown MIGRATION_MODE: ${MIGRATION_MODE}"
            exit 1
            ;;
    esac
}

task_sync_files() {
    write_task "SyncFiles"

    enable_maintenance_page
    ssh_run_ignore "systemctl stop $APP_NAME 2>/dev/null || true"

    local web_tar="/tmp/$APP_NAME.tar.gz.local"
    tar -czf "$web_tar" -C "$PUBLISH_OUT" .
    scp_send "$web_tar" "/tmp/$APP_NAME.tar.gz"
    ssh_run "mkdir -p $DEPLOY_PATH && tar -xzf /tmp/$APP_NAME.tar.gz -C $DEPLOY_PATH && rm /tmp/$APP_NAME.tar.gz"
    ssh_run "chown -R www-data:www-data $DEPLOY_PATH && chmod -R 755 $DEPLOY_PATH"
    ssh_run "mkdir -p /var/keys/$APP_NAME && chown www-data:www-data /var/keys/$APP_NAME && chmod 700 /var/keys/$APP_NAME"
    write_ok "Files synced to $DEPLOY_PATH"

    if [[ $SKIP_MIGRATE -eq 0 ]]; then
        task_migrate_db
    else
        write_warn "Skipping migrations (--skip-migrate)"
    fi

    # systemd unit — depends on docker (for SQL Server container)
    local svc_file="/tmp/$APP_NAME.service.local"
    cat > "$svc_file" <<EOF
[Unit]
Description=$APP_NAME ($DOMAIN)
After=network.target docker.service
Requires=docker.service

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
    scp_send "$svc_file" "/tmp/$APP_NAME.service"
    ssh_run "mv /tmp/$APP_NAME.service /etc/systemd/system/$APP_NAME.service && systemctl daemon-reload && systemctl enable $APP_NAME && systemctl restart $APP_NAME"
    write_ok "Service started: $APP_NAME"

    # Wait for the service to actually answer on its port before flipping nginx.
    write_step "Waiting for service to respond on port $APP_PORT"
    if ! ssh_run "for i in \$(seq 1 30); do curl -sf http://127.0.0.1:$APP_PORT -m 3 -o /dev/null && exit 0; sleep 2; done; exit 1"; then
        write_err "Service didn't respond after 60s. Maintenance page left in place."
        write_err "Logs: ssh $SSH_TARGET journalctl -u $APP_NAME -n 100 --no-pager"
        exit 1
    fi
    write_ok "Service is responding"
}

task_configure_nginx() {
    write_task "ConfigureNginx"
    local cert_exists
    cert_exists=$(ssh_query "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no")

    local nginx_file="/tmp/$APP_NAME.nginx.conf.local"
    if [[ "$cert_exists" == "yes" ]]; then
        cat > "$nginx_file" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / { return 301 https://\$host\$request_uri; }
}
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    add_header Strict-Transport-Security "max-age=31536000" always;

    client_max_body_size 50M;
    proxy_read_timeout 300s;

    location / {
        proxy_pass         http://127.0.0.1:$APP_PORT;
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
        write_ok "Nginx: HTTPS proxy"
    else
        cat > "$nginx_file" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN www.$DOMAIN;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / {
        proxy_pass         http://127.0.0.1:$APP_PORT;
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
        write_ok "Nginx: HTTP proxy (run with --ssl to enable HTTPS)"
    fi

    scp_send "$nginx_file" "/tmp/$APP_NAME.conf"
    ssh_run "mv /tmp/$APP_NAME.conf /etc/nginx/sites-available/$APP_NAME.conf"
    ssh_run "ln -sf /etc/nginx/sites-available/$APP_NAME.conf /etc/nginx/sites-enabled/$APP_NAME.conf"
    ssh_run "nginx -t && systemctl reload nginx"
    write_ok "Nginx reloaded"
}

install_renewal_hook() {
    local hook_script="/tmp/dm-renew-hook.sh.local"
    cat > "$hook_script" <<'HOOK'
#!/bin/bash
# Auto-installed by deploy.sh — do not edit.
set -e
for d in /etc/letsencrypt/live/*/; do
    domain=$(basename "$d")
    [ "$domain" = "README" ] && continue
    docker exec nginx mkdir -p "/etc/letsencrypt/live/$domain"
    docker cp "$d/fullchain.pem" "nginx:/etc/letsencrypt/live/$domain/fullchain.pem" || true
    docker cp "$d/privkey.pem"   "nginx:/etc/letsencrypt/live/$domain/privkey.pem"   || true
done
docker exec nginx nginx -s reload || true
HOOK
    scp_send "$hook_script" "/usr/local/bin/dm-renew-hook.sh"
    ssh_run "chmod +x /usr/local/bin/dm-renew-hook.sh"
    ssh_run "(crontab -l 2>/dev/null | grep -v 'dm-renew-hook' ; echo '0 3 * * * certbot renew --quiet --pre-hook \"docker stop nginx\" --post-hook \"docker start nginx\" --deploy-hook /usr/local/bin/dm-renew-hook.sh') | crontab -"
}

task_install_ssl() {
    write_task "InstallSSL"

    ssh_run "command -v certbot >/dev/null 2>&1 || (apt-get update && apt-get install -y certbot)"

    # --cert-name $DOMAIN forces certs into /etc/letsencrypt/live/$DOMAIN/ regardless
    # of which subset of names actually validates. nginx config can hardcode that path.
    local primary_args="-d $DOMAIN -d www.$DOMAIN"
    local staging_args=""
    [[ -n "${STAGING_DOMAIN:-}" ]] && staging_args="-d $STAGING_DOMAIN"

    local ssl_script="/tmp/dm-ssl-$APP_NAME.sh.local"
    cat > "$ssl_script" <<EOF
#!/bin/bash
set -e
echo 'Stopping nginx briefly for cert issuance...'
docker stop nginx
sleep 2

issued=""
if certbot certonly --standalone --expand --cert-name $DOMAIN --non-interactive --agree-tos -m $SSL_EMAIL $primary_args $staging_args 2>/tmp/certbot.err; then
    issued="full"
elif [ -n "$staging_args" ] && certbot certonly --standalone --expand --cert-name $DOMAIN --non-interactive --agree-tos -m $SSL_EMAIL $staging_args 2>/tmp/certbot.err; then
    issued="staging-only"
    echo 'NOTE: Issued cert for staging domain only — primary domain DNS is not pointed here yet.'
    echo '      Re-run with --task ssl after flipping DNS to extend the cert to the primary.'
else
    echo 'CERTBOT FAILED:'
    cat /tmp/certbot.err
    docker start nginx
    exit 1
fi

docker start nginx
echo "Issued: \$issued"
EOF
    run_remote_script "$ssl_script" "/tmp/dm-ssl-$APP_NAME.sh"

    install_renewal_hook
    write_ok "SSL ready; auto-renewal cron active (3am daily)"

    task_configure_nginx
}

task_migrate_tor() {
    write_task "TorMigrate"

    local keys_dir="${TOR_KEYS_DIR:-${TOR_KEYS_BASE_DIR:-}/$CURRENT_SERVICE}"

    if [[ ! -f "$keys_dir/hostname" ]] \
       || [[ ! -f "$keys_dir/hs_ed25519_public_key" ]] \
       || [[ ! -f "$keys_dir/hs_ed25519_secret_key" ]]; then
        write_err "Tor keys not found at $keys_dir/"
        write_err "Expected: hostname, hs_ed25519_public_key, hs_ed25519_secret_key"
        return 1
    fi

    # Ship keys to flat /tmp paths (world-writable, no sudo needed for scp)
    scp_send "$keys_dir/hostname"              "/tmp/tor-$APP_NAME-hostname"
    scp_send "$keys_dir/hs_ed25519_public_key" "/tmp/tor-$APP_NAME-pub"
    scp_send "$keys_dir/hs_ed25519_secret_key" "/tmp/tor-$APP_NAME-sec"

    # Build the install script LOCALLY, then ship it and run it
    local script_local="/tmp/tor-install-$APP_NAME.sh.local"
    cat > "$script_local" <<EOF
#!/usr/bin/env bash
set -euo pipefail

APP_NAME="$APP_NAME"
APP_PORT="$APP_PORT"

# Install tor if missing
if ! command -v tor >/dev/null 2>&1; then
    DEBIAN_FRONTEND=noninteractive apt-get update -y
    DEBIAN_FRONTEND=noninteractive apt-get install -y tor
fi

# Place keys with correct ownership and mode
install -d -m 700 -o debian-tor -g debian-tor "/var/lib/tor/\$APP_NAME"
install -m 600 -o debian-tor -g debian-tor "/tmp/tor-\$APP_NAME-hostname" "/var/lib/tor/\$APP_NAME/hostname"
install -m 600 -o debian-tor -g debian-tor "/tmp/tor-\$APP_NAME-pub"      "/var/lib/tor/\$APP_NAME/hs_ed25519_public_key"
install -m 600 -o debian-tor -g debian-tor "/tmp/tor-\$APP_NAME-sec"      "/var/lib/tor/\$APP_NAME/hs_ed25519_secret_key"
rm -f "/tmp/tor-\$APP_NAME-hostname" "/tmp/tor-\$APP_NAME-pub" "/tmp/tor-\$APP_NAME-sec"

# Append HiddenService block to torrc if not already present
MARKER="# BEGIN \$APP_NAME hidden service"
if ! grep -qF "\$MARKER" /etc/tor/torrc; then
    cat >> /etc/tor/torrc <<TORRC

\$MARKER
HiddenServiceDir /var/lib/tor/\$APP_NAME/
HiddenServicePort 80 127.0.0.1:\$APP_PORT
# END \$APP_NAME hidden service
TORRC
fi

# On Debian/Ubuntu, tor.service is a dummy wrapper; the real instance is tor@default
systemctl enable tor@default >/dev/null 2>&1 || true
systemctl restart tor@default

sleep 1
cat "/var/lib/tor/\$APP_NAME/hostname"
EOF

    scp_send "$script_local" "/tmp/tor-install-$APP_NAME.sh"
    ssh_run "chmod +x /tmp/tor-install-$APP_NAME.sh"
    local onion
    onion=$(ssh_query "/tmp/tor-install-$APP_NAME.sh")
    ssh_run "rm -f /tmp/tor-install-$APP_NAME.sh"

    write_ok "Onion address: $onion"
}

task_smoke_test() {
    write_task "SmokeTest"
    local base="https://$DOMAIN"
    # Hit homepage + sitemap to warm caches before declaring deploy complete.
    # Otherwise downstream consumers (gitsitesyncer) can hit a cold app.
    local urls=("/" "/sitemap.xml")
    local code
    for path in "${urls[@]}"; do
        for i in $(seq 1 6); do
            code=$(curl -skL -o /dev/null -w '%{http_code}' --max-time 30 "$base$path" || echo "000")
            if [[ "$code" =~ ^(200|301|302)$ ]]; then
                write_ok "Warmed $base$path → HTTP $code"
                break
            fi
            sleep 5
        done
        [[ "$code" =~ ^(200|301|302)$ ]] || write_warn "$base$path returned $code"
    done
}

# ============================================================================
# PER-SERVICE PIPELINE
# ============================================================================
deploy_service() {
    local svc="$1"
    CURRENT_SERVICE="$svc"

    local conf="$SERVICES_DIR/$svc.conf"
    [[ -f "$conf" ]] || { write_err "Service config not found: $conf"; exit 1; }

    unset USES_DB USES_TOR STAGING_DOMAIN MIGRATION_MODE \
          DATA_PROJECT_SOURCE_PATH TEST_SOLUTION_SOURCE_PATH \
          TOR_KEYS_DIR
    # shellcheck disable=SC1090
    source "$conf"

    WEB_PROJECT_DIR="$SCRIPT_DIR/$WEB_PROJECT_SOURCE_PATH"
    DATA_PROJECT_DIR="$SCRIPT_DIR/${DATA_PROJECT_SOURCE_PATH:-}"
    TEST_SOLUTION_PATH="$SCRIPT_DIR/${TEST_SOLUTION_SOURCE_PATH:-}"
    PUBLISH_OUT="$SCRIPT_DIR/../publish/$svc"

    if [[ -n "$SINGLE_TASK" ]]; then
        case "$SINGLE_TASK" in
            configs)         task_set_configs ;;
            restore)         task_restore_packages ;;
            build)           task_build_project ;;
            test)            task_run_unit_tests ;;
            publish)         task_create_package ;;
            sync)            task_sync_files ;;
            migrate)         task_migrate_db ;;
            nginx)           task_configure_nginx ;;
            smoke)           task_smoke_test ;;
            ssl)             task_install_ssl ;;
            tor)             task_migrate_tor ;;
            bootstrap)       task_bootstrap_server ;;
            mssql)           task_setup_mssql ;;
            maintenance-on)  enable_maintenance_page ;;
            maintenance-off) task_configure_nginx ;;
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
    task_configure_nginx
    if [[ $DO_TOR -eq 1 ]]; then
        task_migrate_tor
    fi
    task_smoke_test
}

# ============================================================================
# MAIN
# ============================================================================
if [[ -z "$SINGLE_TASK" ]]; then
    : # skipping bootstrap and mssql — server already configured
fi

for svc in "${SERVICES[@]}"; do
    deploy_service "$svc"
done

echo
echo "${C_GREEN}============================================${C_RESET}"
echo "${C_GREEN} Deployment complete: ${SERVICES[*]}${C_RESET}"
echo "${C_GREEN}============================================${C_RESET}"
echo
echo " ${C_GRAY}Status:    ssh $SSH_TARGET systemctl status <service>${C_RESET}"
echo " ${C_GRAY}Logs:      ssh $SSH_TARGET journalctl -u <service> -f${C_RESET}"
echo " ${C_GRAY}DB:        ssh $SSH_TARGET docker exec -it mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<sa-pass>' -C${C_RESET}"
echo
echo " ${C_GRAY}One-time:  ./deploy.sh <svc> --task ssl    # SSL cert (auto-renews after)${C_RESET}"
echo " ${C_GRAY}One-time:  ./deploy.sh <svc> --task tor    # Tor onion service${C_RESET}"
echo