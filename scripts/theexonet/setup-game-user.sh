#!/bin/bash
# Create theexonet service group/user and optional FTP deploy user.
# Run as root. Sourced by install-host-server.sh or standalone:
#   sudo bash scripts/theexonet/setup-game-user.sh
set -euo pipefail

SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"
FTP_USER="${GAME_FTP_USER:-gameftp}"
FTP_PASSWORD="${GAME_FTP_PASSWORD:-}"

log() { echo "[setup-game-user] $*"; }

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if ! getent group "${SERVICE_GROUP}" >/dev/null; then
  groupadd --system "${SERVICE_GROUP}"
  log "Created group ${SERVICE_GROUP}"
fi

if ! id "${SERVICE_USER}" >/dev/null 2>&1; then
  useradd --system \
    --gid "${SERVICE_GROUP}" \
    --home-dir /var/www \
    --shell /usr/sbin/nologin \
    --comment "theexonet game services" \
    "${SERVICE_USER}"
  log "Created service user ${SERVICE_USER}"
fi

usermod -aG docker "${SERVICE_USER}" 2>/dev/null || true

if ! id "${FTP_USER}" >/dev/null 2>&1; then
  useradd \
    --gid "${SERVICE_GROUP}" \
    --home-dir /var/www/staging \
    --shell /usr/sbin/nologin \
    --comment "theexonet FTP deploy" \
    "${FTP_USER}"
  log "Created FTP user ${FTP_USER}"
fi

if [ -n "${FTP_PASSWORD}" ]; then
  echo "${FTP_USER}:${FTP_PASSWORD}" | chpasswd
  log "Set password for ${FTP_USER}"
fi

usermod -aG "${SERVICE_GROUP}" "${FTP_USER}" 2>/dev/null || true

# Apache reads static game files; keep www-data able to read publish/html.
usermod -aG "${SERVICE_GROUP}" www-data 2>/dev/null || true

log "Service user: ${SERVICE_USER} (group ${SERVICE_GROUP})"
log "FTP user:     ${FTP_USER} (group ${SERVICE_GROUP})"
