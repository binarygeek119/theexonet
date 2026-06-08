#!/bin/bash
# Fix ownership and permissions for theexonet hosting directories on Linux.
# Run on the server as root:
#   sudo bash scripts/fix-hosting-permissions.sh
#   sudo fix-theexonet-permissions
#   sudo fix-theexonet-permissions -q
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=theexonet-hosting-env.sh
source "${SCRIPT_DIR}/theexonet-hosting-env.sh"

QUIET=0
if [ "${1:-}" = "-q" ] || [ "${1:-}" = "--quiet" ]; then
  QUIET=1
fi

say() {
  if [ "$QUIET" -eq 0 ]; then
    echo "$@"
  fi
}

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
  say "OK  ${path} (${mode}${owner:+, ${owner}})"
}

fix_publish_tree() {
  say "--- Publish (${PUBLISH_DIR}) ---"
  ensure_dir "${WWW_ROOT}" 755
  ensure_dir "${PUBLISH_DIR}" 755
  ensure_dir "${PUBLISH_DIR}/.aspnet" 775 "${SERVICE_USER}:${SERVICE_GROUP}"
  for www_assets in "${PUBLISH_DIR}/wwwroot" "${PUBLISH_DIR}/status-wwwroot"; do
    if [ ! -d "$www_assets" ]; then
      continue
    fi
    chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "$www_assets"
    find "$www_assets" -type d -exec chmod 755 {} +
    find "$www_assets" -type f -exec chmod 644 {} +
    say "OK  ${www_assets} (644/755, ${SERVICE_USER})"
  done

  # CI deploy (githubdeploy) rsyncs html from staging; group write keeps Apache (www-data) readable.
  if [ -d "${PUBLISH_DIR}/html" ]; then
    chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${PUBLISH_DIR}/html"
    find "${PUBLISH_DIR}/html" -type d -exec chmod 2775 {} +
    find "${PUBLISH_DIR}/html" -type f -exec chmod 664 {} +
    say "OK  ${PUBLISH_DIR}/html (664/2775, ${SERVICE_USER})"
  else
    ensure_dir "${PUBLISH_DIR}/html" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  fi

  # Deploy rsync often leaves DLLs owned by the SSH user; www-data must read them to run portals/API.
  if [ -d "${PUBLISH_DIR}" ]; then
    chmod a+rx "${PUBLISH_DIR}" 2>/dev/null || true
    find "${PUBLISH_DIR}" -maxdepth 1 -type f \( -name '*.dll' -o -name '*.json' -o -name '*.deps.json' \) \
      -exec chmod a+r {} + 2>/dev/null || true
    say "OK  publish assemblies world-readable"
  fi
}

fix_staging_tree() {
  local staging="${THEEXONET_STAGING_DIR:-/var/www/staging}"
  say "--- Staging (${staging}) ---"
  ensure_dir "${staging}" 2775 "root:${SERVICE_GROUP}"
  ensure_dir "${staging}/.incoming" 2775 "root:${SERVICE_GROUP}"
  if id githubdeploy >/dev/null 2>&1; then
    usermod -aG "${SERVICE_GROUP}" githubdeploy 2>/dev/null || true
    say "OK  githubdeploy in group ${SERVICE_GROUP} (CI can mv uploads into staging/.incoming)"
  fi
}

fix_data_tree() {
  say "--- Data (${DATA_DIR}) ---"
  for path in "${DATA_WRITABLE_DIRS[@]}"; do
    ensure_dir "${path}" 2775 "${SERVICE_USER}:${SERVICE_GROUP}"
  done

  if [ -d "${DATA_DIR}" ]; then
    chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${DATA_DIR}"
    find "${DATA_DIR}" -type d -exec chmod 2775 {} +
    find "${DATA_DIR}" -type f -exec chmod 664 {} +
    say "OK  applied ${SERVICE_USER} ownership and 2775/664 under ${DATA_DIR}"
  fi
}

say "Fixing theexonet hosting permissions (user=${SERVICE_USER})..."
fix_publish_tree
fix_staging_tree
fix_data_tree
say "Done."
