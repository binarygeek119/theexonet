#!/bin/bash
# Publish the status dashboard into the live publish folder and restart rava-status.
# Run on the server as root:
#   sudo deploy-rava-status
# Or from a repo checkout:
#   sudo bash scripts/deploy-status.sh /path/to/rava-1/server
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SERVER_DIR="${1:-${SCRIPT_DIR}/../server}"
PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
STATUS_SERVICE="${RAVA_STATUS_SERVICE:-rava-status}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -f "${SERVER_DIR}/Rava.Status/Rava.Status.csproj" ]; then
  echo "Missing ${SERVER_DIR}/Rava.Status/Rava.Status.csproj" >&2
  echo "Pass the server directory: sudo bash $0 /path/to/rava-1/server" >&2
  exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Publishing status dashboard..."
dotnet publish "${SERVER_DIR}/Rava.Status/Rava.Status.csproj" \
  --configuration Release \
  --output "${work}/publish-status"

rsync -a \
  --exclude 'appsettings.json' \
  --exclude 'appsettings.Development.json' \
  --exclude 'status-runtime.json' \
  "${work}/publish-status/" "${PUBLISH_DIR}/"

for required in \
  "${PUBLISH_DIR}/Rava.Status.dll" \
  "${PUBLISH_DIR}/wwwroot/index.html" \
  "${PUBLISH_DIR}/wwwroot/js/status.js"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: missing ${required} after status deploy." >&2
    exit 1
  fi
done

echo "Restarting ${STATUS_SERVICE}..."
systemctl restart "${STATUS_SERVICE}"

if command -v curl >/dev/null 2>&1; then
  curl -sf http://127.0.0.1:6000/api/dashboard >/dev/null && echo "Status API: OK" || echo "Status API: unreachable"
  curl -sf http://127.0.0.1:6000/ >/dev/null && echo "Status dashboard: OK" || echo "Status dashboard: unreachable"
fi

echo "Done."
