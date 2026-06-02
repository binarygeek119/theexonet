#!/bin/bash
# Merge status dashboard + admin/moderator portal static files into a shared wwwroot.
# Used by CI deploy and local publish so rsync --delete does not strip status assets.
set -euo pipefail

SERVER_DIR="${1:?server directory (contains Rava.Status and Rava.Api)}"
WWWROOT_DIR="${2:?wwwroot destination directory}"

HTML_DIR="${SERVER_DIR}/Rava.Api/html"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STATUS_DEDICATED="$(dirname "$WWWROOT_DIR")/status-wwwroot"

bash "${SCRIPT_DIR}/sync-status-wwwroot.sh" "$SERVER_DIR" "$WWWROOT_DIR"
mkdir -p "$STATUS_DEDICATED"
bash "${SCRIPT_DIR}/sync-status-wwwroot.sh" "$SERVER_DIR" "$STATUS_DEDICATED"

bash "${SCRIPT_DIR}/sync-portal-wwwroot.sh" \
  "$HTML_DIR" \
  "$WWWROOT_DIR"

bash "${SCRIPT_DIR}/sync-docs-wwwroot.sh" \
  "$SERVER_DIR" \
  "$WWWROOT_DIR"

for required in \
  "${WWWROOT_DIR}/index.html" \
  "${WWWROOT_DIR}/js/status.js" \
  "${WWWROOT_DIR}/admin.html" \
  "${WWWROOT_DIR}/js/admin-testing-mode.js" \
  "${WWWROOT_DIR}/css/docs.css"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: ${required} missing after publish wwwroot sync." >&2
    exit 1
  fi
done

echo "Synced status + portal + docs wwwroot assets into ${WWWROOT_DIR}"
