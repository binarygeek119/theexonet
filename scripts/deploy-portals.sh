#!/bin/bash
# Publish admin + moderator portals into the live publish folder and restart services.
# Run on the server as root:
#   sudo deploy-rava-portals
# Or from a repo checkout:
#   sudo bash scripts/deploy-portals.sh /path/to/rava-1/server
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SERVER_DIR="${1:-${SCRIPT_DIR}/../server}"
PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
ADMIN_SERVICE="${RAVA_ADMIN_SERVICE:-rava-admin}"
MODERATOR_SERVICE="${RAVA_MODERATOR_SERVICE:-rava-moderator}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -f "${SERVER_DIR}/Rava.Admin/Rava.Admin.csproj" ]; then
  echo "Missing ${SERVER_DIR}/Rava.Admin/Rava.Admin.csproj" >&2
  echo "Pass the server directory: sudo bash $0 /path/to/rava-1/server" >&2
  exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Publishing admin portal..."
dotnet publish "${SERVER_DIR}/Rava.Admin/Rava.Admin.csproj" \
  --configuration Release \
  --output "${work}/publish-admin"

echo "Publishing moderator portal..."
dotnet publish "${SERVER_DIR}/Rava.Moderator/Rava.Moderator.csproj" \
  --configuration Release \
  --output "${work}/publish-moderator"

rsync -a \
  --exclude 'appsettings.json' \
  --exclude 'appsettings.Development.json' \
  "${work}/publish-admin/" "${PUBLISH_DIR}/"

rsync -a \
  --exclude 'appsettings.json' \
  --exclude 'appsettings.Development.json' \
  "${work}/publish-moderator/" "${PUBLISH_DIR}/"

bash "${SCRIPT_DIR}/sync-portal-wwwroot.sh" \
  "${SERVER_DIR}/Rava.Api/html" \
  "${PUBLISH_DIR}/wwwroot"

for required in \
  "${PUBLISH_DIR}/Rava.Admin.dll" \
  "${PUBLISH_DIR}/Rava.Moderator.dll" \
  "${PUBLISH_DIR}/wwwroot/admin.html" \
  "${PUBLISH_DIR}/wwwroot/moderator.html" \
  "${PUBLISH_DIR}/wwwroot/js/currency.js" \
  "${PUBLISH_DIR}/wwwroot/images/currency.svg"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: missing ${required} after portal deploy." >&2
    exit 1
  fi
done

echo "Restarting ${ADMIN_SERVICE} and ${MODERATOR_SERVICE}..."
systemctl restart "${ADMIN_SERVICE}" "${MODERATOR_SERVICE}"

if command -v curl >/dev/null 2>&1; then
  curl -sf http://127.0.0.1:7000/admin.html >/dev/null && echo "Admin portal: OK" || echo "Admin portal: unreachable"
  curl -sf http://127.0.0.1:7050/moderator.html >/dev/null && echo "Moderator portal: OK" || echo "Moderator portal: unreachable"
  curl -sf http://127.0.0.1:7000/js/currency.js >/dev/null && echo "Admin currency.js: OK" || echo "Admin currency.js: missing"
  curl -sf http://127.0.0.1:7000/images/currency.svg >/dev/null && echo "Admin currency.svg: OK" || echo "Admin currency.svg: missing"
fi

echo "Done."
