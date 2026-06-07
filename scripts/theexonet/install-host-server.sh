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
#   SKIP_FTP=1                     # use SSH/GitHub deploy only
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
if ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10.'; then
  log "Installing ASP.NET Core 10 runtime…"
  CODENAME="$(. /etc/os-release && echo "${VERSION_CODENAME}")"
  wget -q "https://packages.microsoft.com/config/ubuntu/${CODENAME}/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
  dpkg -i /tmp/packages-microsoft-prod.deb
  apt-get update -y
  apt-get install -y aspnetcore-runtime-10.0
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
bash "${SCRIPTS_SRC}/install-bin-scripts.sh" "${SCRIPTS_SRC}"
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
  export GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
  export GAME_FTP_ROOT="${STAGING_DIR}"
  export GAME_FTP_PASSWORD
  bash "${SCRIPT_DIR}/install-ftp-server.sh"
  chown "${GAME_FTP_USER}:${SERVICE_GROUP}" "${STAGING_DIR}"
  chmod 2775 "${STAGING_DIR}"
fi

# --- Permissions watcher ---
install-theexonet-permissions-service 2>/dev/null || \
  bash "${SCRIPTS_SRC}/install-permissions-service.sh" 2>/dev/null || true

export THEEXONET_SERVICE_USER="${SERVICE_USER}"
fix-theexonet-permissions -q 2>/dev/null || \
  bash "${SCRIPTS_SRC}/fix-hosting-permissions.sh" -q

# --- Credentials summary ---
CREDS_FILE="${SECRETS_DIR}/install-credentials.txt"
cat >"${CREDS_FILE}" <<EOF
theexonet host install — $(date -u +%Y-%m-%dT%H:%M:%SZ)
Domain:            ${DOMAIN}
Service user:      ${SERVICE_USER}
FTP user:          ${GAME_FTP_USER:-gameftp} (upload → ${STAGING_DIR})
Postgres user:     theexonet
Postgres password: ${POSTGRES_PASSWORD}
JWT key:           ${JWT_KEY}
Repo path:         ${REPO_PATH}
Publish dir:       ${PUBLISH_DIR}
Data dir:          ${DATA_DIR}
EOF
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

HTTPS (after DNS works):
  apt install -y certbot python3-certbot-apache
  certbot --apache -d ${DOMAIN} -d www.${DOMAIN} \\
    -d api.${DOMAIN} -d status.${DOMAIN} -d admin.${DOMAIN} \\
    -d moderator.${DOMAIN} -d docs.${DOMAIN}

Deploy game (GitHub Actions rsync or manual):
  1. Upload publish zip to ${STAGING_DIR} via FTPS (user gameftp), then:
     sudo bash ${SCRIPT_DIR}/promote-staging.sh
  2. Or let GitHub Actions SSH deploy to ${PUBLISH_DIR} (see docs/deploy.md)

Credentials saved: ${CREDS_FILE}
Restart stack:     sudo restart-theexonet
Diagnostics:       sudo diagnose-theexonet-api

EOF
