#!/bin/bash
# Merge status dashboard + admin/moderator portal static files into a shared wwwroot.
# Used by CI deploy and local publish so rsync --delete does not strip status assets.
set -euo pipefail

SERVER_DIR="${1:?server directory (contains Rava.Status and Rava.Api)}"
WWWROOT_DIR="${2:?wwwroot destination directory}"

STATUS_SRC="${SERVER_DIR}/Rava.Status/wwwroot"
HTML_DIR="${SERVER_DIR}/Rava.Api/html"

if [ ! -d "$STATUS_SRC" ]; then
  echo "Missing status wwwroot: ${STATUS_SRC}" >&2
  exit 1
fi

mkdir -p "${WWWROOT_DIR}/css" "${WWWROOT_DIR}/js"

copy_status_file() {
  local rel="$1"
  local src="${STATUS_SRC}/${rel}"
  local dest="${WWWROOT_DIR}/${rel}"
  if [ ! -f "$src" ]; then
    echo "Missing ${src}" >&2
    exit 1
  fi
  mkdir -p "$(dirname "$dest")"
  cp -f "$src" "$dest"
}

for rel in \
  index.html \
  values.html \
  css/status.css \
  js/status.js \
  js/values.js; do
  copy_status_file "$rel"
done

bash "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/sync-portal-wwwroot.sh" \
  "$HTML_DIR" \
  "$WWWROOT_DIR"

for required in \
  "${WWWROOT_DIR}/index.html" \
  "${WWWROOT_DIR}/js/status.js" \
  "${WWWROOT_DIR}/admin.html" \
  "${WWWROOT_DIR}/js/admin-testing-mode.js"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: ${required} missing after publish wwwroot sync." >&2
    exit 1
  fi
done

echo "Synced status + portal wwwroot assets into ${WWWROOT_DIR}"
