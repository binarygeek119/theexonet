#!/bin/bash
# Full Ubuntu LTS host setup for theexonet.com:
#   - Apache2 reverse proxy (game + api/status/admin/moderator/docs subdomains)
#   - Docker + PostgreSQL database
#   - theexonet service user + gameftp FTPS deploy user
#   - ASP.NET 10 runtime, systemd units, helper scripts
#
# Run on the VM as root (Ubuntu 22.04 or 24.04 LTS):
#   sudo bash scripts/theexonet/install-host-server.sh
#
# Optional env:
#   THEEXONET_DOMAIN=theexonet.com
#   THEEXONET_REPO_URL=https://github.com/binarygeek119/theexonet.git
#   THEEXONET_REPO_PATH=/opt/theexonet/theexonet
#   POSTGRES_PASSWORD=...          # generated if unset
#   GAME_FTP_PASSWORD=...          # generated if unset; skip FTP with SKIP_FTP=1
#   SKIP_FTP=1                     # skip FTPS user (use SFTP/manual staging only)
#   SKIP_REPO_CLONE=1              # scripts already on disk
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DOMAIN="${THEEXONET_DOMAIN:-theexonet.com}"
REPO_URL="${THEEXONET_REPO_URL:-https://github.com/binarygeek119/theexonet.git}"
REPO_PATH="${THEEXONET_REPO_PATH:-/opt/theexonet/theexonet}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
SECRETS_DIR="/etc/theexonet"

log() { echo "[install-host-server] $*"; }

# shellcheck source=wait-for-apt-lock.sh
source "${SCRIPT_DIR}/wait-for-apt-lock.sh"

rand_secret() {
  openssl rand -base64 32 | tr -d '/+=' | head -c 32
}

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

# --- Base packages (Apache, Docker) ---
if ! command -v docker >/dev/null 2>&1 || ! systemctl is-active --quiet apache2 2>/dev/null; then
  log "Running startup-install.sh (Apache + Docker)…"
  bash "${SCRIPT_DIR}/startup-install.sh"
else
  log "Apache and Docker already present."
fi

wait_for_apt_lock
apt-get install -y unzip rsync curl

# --- Game users ---
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(rand_secret)}"
GAME_FTP_PASSWORD="${GAME_FTP_PASSWORD:-$(rand_secret)}"
export THEEXONET_SERVICE_USER="${SERVICE_USER}"
export THEEXONET_SERVICE_GROUP="${SERVICE_GROUP}"
export GAME_FTP_PASSWORD
bash "${SCRIPT_DIR}/setup-game-user.sh"

# --- Directory layout ---
log "Creating publish, data, and staging directories…"
mkdir -p \
  "${PUBLISH_DIR}/html/images/profile" \
  "${PUBLISH_DIR}/html/images/profile-backgrounds" \
  "${PUBLISH_DIR}/.aspnet" \
  "${PUBLISH_DIR}/status-wwwroot" \
  "${DATA_DIR}" \
  "${STAGING_DIR}" \
  "${SECRETS_DIR}" \
  "$(dirname "${REPO_PATH}")"

chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${PUBLISH_DIR}" "${DATA_DIR}" "${STAGING_DIR}"
chmod 2775 "${DATA_DIR}" "${STAGING_DIR}"
chmod 755 "${PUBLISH_DIR}"

# Placeholder game page until first deploy
if [ ! -f "${PUBLISH_DIR}/html/index.html" ]; then
  mkdir -p "${PUBLISH_DIR}/html"
  cat >"${PUBLISH_DIR}/html/index.html" <<EOF
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><title>theexonet</title></head>
<body><h1>theexonet</h1><p>Publish the game bundle to ${PUBLISH_DIR} (FTP staging or GitHub deploy).</p></body></html>
EOF
  chown "${SERVICE_USER}:${SERVICE_GROUP}" "${PUBLISH_DIR}/html/index.html"
fi

# --- ASP.NET 10 runtime ---
# Ubuntu 22.04/24.04: .NET 10 is in ppa:dotnet/backports (Microsoft's jammy feed stops at 9.x).
install_aspnetcore10_apt() {
  . /etc/os-release
  log "Installing ASP.NET Core 10 via Ubuntu dotnet backports (${VERSION_ID:-unknown})…"
  wait_for_apt_lock
  apt-get install -y software-properties-common
  add-apt-repository -y ppa:dotnet/backports
  apt-get update -y
  log "Installing aspnetcore-runtime-10.0 (may take a few minutes)…"
  apt-get install -y aspnetcore-runtime-10.0
}

install_aspnetcore10_via_script() {
  log "Using dotnet-install.sh (works on all Ubuntu LTS versions)…"
  wget -O /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet
  if [ ! -e /usr/bin/dotnet ]; then
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
  fi
}

has_aspnetcore10_runtime() {
  dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10.'
}

if ! has_aspnetcore10_runtime; then
  log "Installing ASP.NET Core 10 runtime…"
  if ! install_aspnetcore10_apt; then
    log "apt install failed — trying dotnet-install.sh…"
    install_aspnetcore10_via_script
  fi
  if ! has_aspnetcore10_runtime; then
    echo "ERROR: ASP.NET Core 10 runtime not found after install. Run: dotnet --list-runtimes" >&2
    exit 1
  fi
  log "ASP.NET Core 10 runtime ready: $(dotnet --list-runtimes | grep 'Microsoft.AspNetCore.App 10.' | head -1)"
else
  log "ASP.NET Core 10 runtime already installed."
fi

# --- PostgreSQL (Docker) ---
mkdir -p "${SECRETS_DIR}"
chmod 700 "${SECRETS_DIR}"
if [ ! -f "${SECRETS_DIR}/postgres.env" ]; then
  cat >"${SECRETS_DIR}/postgres.env" <<EOF
POSTGRES_USER=theexonet
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
POSTGRES_DB=theexonet
EOF
  chmod 600 "${SECRETS_DIR}/postgres.env"
  log "Wrote ${SECRETS_DIR}/postgres.env"
fi

log "Starting PostgreSQL container…"
docker compose -f "${SCRIPT_DIR}/docker-compose.postgres.yml" up -d
docker compose -f "${SCRIPT_DIR}/docker-compose.postgres.yml" ps

# --- Clone repo and install helpers ---
if [ "${SKIP_REPO_CLONE:-0}" != "1" ]; then
  if [ -d "${REPO_PATH}/.git" ]; then
    log "Updating git checkout at ${REPO_PATH}…"
    git -C "${REPO_PATH}" pull --ff-only || true
  else
    log "Cloning ${REPO_URL} → ${REPO_PATH}…"
    git clone --depth 1 "${REPO_URL}" "${REPO_PATH}"
  fi
else
  REPO_PATH="${REPO_ROOT}"
  log "Using existing repo at ${REPO_PATH}"
fi

SCRIPTS_SRC="${REPO_PATH}/scripts"
if [ ! -d "${SCRIPTS_SRC}/systemd" ]; then
  echo "ERROR: ${SCRIPTS_SRC}/systemd not found. Set THEEXONET_REPO_PATH or clone the repo." >&2
  exit 1
fi

export THEEXONET_SERVICE_USER="${SERVICE_USER}"
export THEEXONET_SERVICE_GROUP="${SERVICE_GROUP}"
export THEEXONET_PUBLISH_DIR="${PUBLISH_DIR}"
export THEEXONET_DATA_DIR="${DATA_DIR}"
export THEEXONET_STAGING_DIR="${STAGING_DIR}"
bash "${SCRIPTS_SRC}/install-bin-scripts.sh" "${SCRIPTS_SRC}"

log "Seeding data CSV spreadsheets to ${DATA_DIR}…"
if command -v sync-theexonet-data >/dev/null 2>&1; then
  sync-theexonet-data || log "WARN: sync-theexonet-data failed — CSV templates may be missing from repo."
else
  bash "${SCRIPTS_SRC}/sync-publish-data.sh" || log "WARN: sync-publish-data failed."
fi

log "Installing systemd units…"
install-theexonet-systemd || bash "${SCRIPTS_SRC}/install-systemd-units.sh"

# --- appsettings for API + portals ---
JWT_KEY="${JWT_KEY:-$(rand_secret)}$(rand_secret)"
APPS_SETTINGS="${DATA_DIR}/appsettings.json"
if [ ! -f "${APPS_SETTINGS}" ]; then
  EXAMPLE="${REPO_PATH}/server/Theexonet.Api/appsettings.production.example.json"
  if [ ! -f "${EXAMPLE}" ]; then
    EXAMPLE="${REPO_PATH}/server/Theexonet.Api/appsettings.json.example"
  fi
  if [ -f "${EXAMPLE}" ]; then
    cp "${EXAMPLE}" "${APPS_SETTINGS}"
    # Production URLs for theexonet.com subdomains
    sed -i \
      -e "s|Host=localhost|Host=127.0.0.1|g" \
      -e "s|Username=postgres|Username=theexonet|g" \
      -e "s|Password=YOUR_POSTGRES_PASSWORD|Password=${POSTGRES_PASSWORD}|g" \
      -e "s|CHANGE_ME_MinimumLength32Characters!|${JWT_KEY}|g" \
      -e "s|https://theexonet\.binarygeek119\.duckdns\.org|https://${DOMAIN}|g" \
      -e "s|https://theexonetapi\.binarygeek119\.duckdns\.org|https://api.${DOMAIN}|g" \
      -e "s|https://theexonetstatus\.binarygeek119\.duckdns\.org|https://status.${DOMAIN}|g" \
      -e "s|https://theexonetadmin\.binarygeek119\.duckdns\.org|https://admin.${DOMAIN}|g" \
      -e "s|https://theexonetmoderator\.binarygeek119\.duckdns\.org|https://moderator.${DOMAIN}|g" \
      -e "s|https://theexonetdocs\.binarygeek119\.duckdns\.org|https://docs.${DOMAIN}|g" \
      "${APPS_SETTINGS}"
    chown "${SERVICE_USER}:${SERVICE_GROUP}" "${APPS_SETTINGS}"
    chmod 640 "${APPS_SETTINGS}"
    log "Created ${APPS_SETTINGS}"
  else
    log "WARN: No appsettings example found — create ${APPS_SETTINGS} manually."
  fi
fi

# Symlink data appsettings into publish if missing
if [ ! -f "${PUBLISH_DIR}/appsettings.json" ]; then
  ln -sf "${APPS_SETTINGS}" "${PUBLISH_DIR}/appsettings.json"
fi

# --- Apache vhosts ---
log "Configuring Apache for ${DOMAIN}…"
a2enmod proxy proxy_http headers rewrite ssl 2>/dev/null || true
a2dissite 000-default.conf 2>/dev/null || true

sed "s/@@DOMAIN@@/${DOMAIN}/g" "${SCRIPT_DIR}/apache-theexonet.conf" \
  >/etc/apache2/sites-available/theexonet.conf
a2ensite theexonet.conf
systemctl reload apache2

# --- FTP (FTPS) for manual / CI uploads to staging ---
if [ "${SKIP_FTP:-0}" != "1" ]; then
  wait_for_apt_lock
  export GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
  export GAME_FTP_ROOT="/var/www"
  export GAME_FTP_STAGING="${STAGING_DIR}"
  export GAME_FTP_PASSWORD
  bash "${SCRIPT_DIR}/install-ftp-server.sh"
else
  log "SKIP_FTP=1 — skipping vsftpd (upload zips to ${STAGING_DIR} via SFTP if needed)."
fi

# Auto-promote CI/FTP uploads from staging (needs unzip + rsync, installed above).
if [ -f "${SCRIPTS_SRC}/install-staging-watcher.sh" ]; then
  bash "${SCRIPTS_SRC}/install-staging-watcher.sh"
fi

# --- Permissions watcher ---
install-theexonet-permissions-service 2>/dev/null || \
  bash "${SCRIPTS_SRC}/install-permissions-service.sh" 2>/dev/null || true

export THEEXONET_SERVICE_USER="${SERVICE_USER}"
fix-theexonet-permissions -q 2>/dev/null || \
  bash "${SCRIPTS_SRC}/fix-hosting-permissions.sh" -q

# --- Credentials summary ---
CREDS_FILE="${SECRETS_DIR}/install-credentials.txt"
{
  echo "theexonet host install — $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "Domain:            ${DOMAIN}"
  echo "Service user:      ${SERVICE_USER}"
  if [ "${SKIP_FTP:-0}" != "1" ]; then
    echo "FTP user:          ${GAME_FTP_USER:-gameftp} (FTPS upload → ${STAGING_DIR}/)"
    echo "FTP password:      ${GAME_FTP_PASSWORD}  (GitHub secret DEPLOY_FTP_PASSWORD)"
  fi
  echo "Postgres user:     theexonet"
  echo "Postgres password: ${POSTGRES_PASSWORD}"
  echo "JWT key:           ${JWT_KEY}"
  echo "Repo path:         ${REPO_PATH}"
  echo "Publish dir:       ${PUBLISH_DIR}"
  echo "Data dir:          ${DATA_DIR}"
  echo "Staging watcher:   theexonet-staging-watcher.service"
} >"${CREDS_FILE}"
chmod 600 "${CREDS_FILE}"

cat <<EOF

=== theexonet host server ready ===

DNS (point all A records at this server's public IP):
  ${DOMAIN}
  www.${DOMAIN}
  api.${DOMAIN}
  status.${DOMAIN}
  admin.${DOMAIN}
  moderator.${DOMAIN}
  docs.${DOMAIN}

URLs after DNS:
  Game:       https://${DOMAIN}/
  API:        https://api.${DOMAIN}/
  Status:     https://status.${DOMAIN}/
  Admin:      https://admin.${DOMAIN}/
  Moderator:  https://moderator.${DOMAIN}/
  Docs:       https://docs.${DOMAIN}/

HTTPS (after DNS A records resolve to this server):
  sudo CERTBOT_EMAIL=you@example.com bash ${SCRIPT_DIR}/install-ssl-certs.sh
  # or: sudo install-theexonet-ssl

Deploy game (first publish bundle required before services run):
  1. GitHub Actions FTPS (recommended): set ENABLE_PRODUCTION_DEPLOY=true,
     DEPLOY_HOST=${DOMAIN}, secret DEPLOY_FTP_PASSWORD=<FTP password above>.
     See docs/github-deploy-setup.md
  2. Manual: upload theexonet-website-*.zip to ${STAGING_DIR}/ via FTPS (gameftp)
     or SFTP; staging-watcher auto-promotes, or: sudo promote-theexonet-staging
  3. Open GCP firewall TCP 21 and 40000-40050 for GitHub Actions + your IP

Until deploy completes, API/portals stay stopped (no Theexonet.Api.dll yet).

Credentials saved: ${CREDS_FILE}
Promote staging:   sudo promote-theexonet-staging
Restart stack:     sudo restart-theexonet
Diagnostics:       sudo diagnose-theexonet-api

EOF
