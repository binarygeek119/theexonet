#!/bin/bash
# Copy admin/moderator portal static files from Rava.Api/html into a wwwroot folder.
# Used after dotnet publish and for direct deploy to production wwwroot.
set -euo pipefail

HTML_DIR="${1:?html source directory}"
WWWROOT_DIR="${2:?wwwroot destination directory}"

if [ ! -d "$HTML_DIR" ]; then
  echo "Missing html directory: ${HTML_DIR}" >&2
  exit 1
fi

mkdir -p "${WWWROOT_DIR}/css" "${WWWROOT_DIR}/js" "${WWWROOT_DIR}/images"

copy_file() {
  local rel="$1"
  local src="${HTML_DIR}/${rel}"
  local dest="${WWWROOT_DIR}/${rel}"
  if [ ! -f "$src" ]; then
    echo "Missing ${src}" >&2
    exit 1
  fi
  mkdir -p "$(dirname "$dest")"
  cp -f "$src" "$dest"
}

for rel in \
  admin.html \
  moderator.html \
  css/app.css \
  css/admin.css \
  css/moderator.css \
  js/admin.js \
  js/admin-messages-hub.js \
  js/admin-testing-mode.js \
  js/moderator.js \
  js/api.js \
  js/api-status.js \
  js/config.js \
  js/currency.js \
  js/flagged-messages.js \
  js/profile-social.js \
  js/staff-messages.js \
  js/staff-player-inbox.js \
  js/staff-player-messages.js \
  images/currency.png; do
  copy_file "$rel"
done

for required in \
  "${WWWROOT_DIR}/js/currency.js" \
  "${WWWROOT_DIR}/images/currency.png" \
  "${WWWROOT_DIR}/admin.html"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: ${required} missing after portal wwwroot sync." >&2
    exit 1
  fi
done

echo "Synced portal wwwroot assets into ${WWWROOT_DIR}"
