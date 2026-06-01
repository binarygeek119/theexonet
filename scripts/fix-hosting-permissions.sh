#!/bin/bash
# Fix ownership and permissions for RAVA hosting directories on Linux.
# Run on the server as root:
#   sudo bash scripts/fix-hosting-permissions.sh
#   sudo fix-rava-permissions
set -euo pipefail

WWW_ROOT="${RAVA_WWW_ROOT:-/var/www}"
PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"
SERVICE_GROUP="${RAVA_SERVICE_GROUP:-$SERVICE_USER}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if ! id "${SERVICE_USER}" >/dev/null 2>&1; then
  echo "Service user not found: ${SERVICE_USER}" >&2
  exit 1
fi

ensure_dir() {
  local path="$1"
  local mode="$2"
  local owner="${3:-}"

  mkdir -p "${path}"
  if [ -n "${owner}" ]; then
    chown "${owner}" "${path}"
  fi
  chmod "${mode}" "${path}"
  echo "OK  ${path} (${mode}${owner:+, ${owner}})"
}

fix_publish_tree() {
  echo "--- Publish (${PUBLISH_DIR}) ---"
  ensure_dir "${WWW_ROOT}" 755
  ensure_dir "${PUBLISH_DIR}" 755
  ensure_dir "${PUBLISH_DIR}/.aspnet" 775 "${SERVICE_USER}:${SERVICE_GROUP}"
  if [ -d "${PUBLISH_DIR}/wwwroot" ]; then
    chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${PUBLISH_DIR}/wwwroot"
    find "${PUBLISH_DIR}/wwwroot" -type d -exec chmod 755 {} +
    find "${PUBLISH_DIR}/wwwroot" -type f -exec chmod 644 {} +
    echo "OK  ${PUBLISH_DIR}/wwwroot (644/755, ${SERVICE_USER})"
  fi
}

fix_data_tree() {
  echo "--- Data (${DATA_DIR}) ---"
  ensure_dir "${DATA_DIR}" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/images" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/images/profile" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/images/profile-backgrounds" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/exonet" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/exonet/offworld-news" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/exonet/offworld-news/editions" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  ensure_dir "${DATA_DIR}/exonet/offworld-news/images" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"

  if [ -d "${DATA_DIR}" ]; then
    chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${DATA_DIR}"
    find "${DATA_DIR}" -type d -exec chmod 2775 {} +
    find "${DATA_DIR}" -type f -exec chmod 664 {} +
    echo "OK  applied ${SERVICE_USER} ownership and 2775/664 under ${DATA_DIR}"
  fi
}

echo "Fixing RAVA hosting permissions (user=${SERVICE_USER})..."
fix_publish_tree
fix_data_tree
echo "Done."
