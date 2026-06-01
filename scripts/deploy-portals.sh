#!/bin/bash
# Publish admin + moderator portals into the live publish folder and restart services.
# Run on the server as root:
#   cd /opt/rava/rava && sudo deploy-rava-portals
#   sudo deploy-rava-portals /opt/rava/rava/server
#   sudo deploy-rava-portals --static-only   # no SDK: sync wwwroot only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=dotnet-sdk.sh
source "${SCRIPT_DIR}/dotnet-sdk.sh"

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
ADMIN_SERVICE="${RAVA_ADMIN_SERVICE:-rava-admin}"
MODERATOR_SERVICE="${RAVA_MODERATOR_SERVICE:-rava-moderator}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"
STATIC_ONLY=0
SERVER_ARG=""

for arg in "$@"; do
  case "$arg" in
    --static-only)
      STATIC_ONLY=1
      ;;
    -h|--help)
      echo "Usage: sudo deploy-rava-portals [--static-only] [server-directory]" >&2
      exit 0
      ;;
    *)
      if [ -z "$SERVER_ARG" ]; then
        SERVER_ARG="$arg"
      fi
      ;;
  esac
done

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo deploy-rava-portals" >&2
  exit 1
fi

SERVER_DIR="$(bash "${SCRIPT_DIR}/resolve-server-dir.sh" "${SERVER_ARG}")"

if [ ! -f "${SERVER_DIR}/Rava.Admin/Rava.Admin.csproj" ]; then
  echo "Missing ${SERVER_DIR}/Rava.Admin/Rava.Admin.csproj" >&2
  exit 1
fi

sync_portal_wwwroot() {
  bash "${SCRIPT_DIR}/sync-portal-wwwroot.sh" \
    "${SERVER_DIR}/Rava.Api/html" \
    "${PUBLISH_DIR}/wwwroot"
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${PUBLISH_DIR}/wwwroot" 2>/dev/null || true
}

verify_portal_files() {
  for required in \
    "${PUBLISH_DIR}/wwwroot/admin.html" \
    "${PUBLISH_DIR}/wwwroot/moderator.html" \
    "${PUBLISH_DIR}/wwwroot/js/currency.js" \
    "${PUBLISH_DIR}/wwwroot/images/currency.svg"; do
    if [ ! -f "$required" ]; then
      echo "ERROR: missing ${required} after portal deploy." >&2
      exit 1
    fi
  done
}

restart_portals() {
  echo "Restarting ${ADMIN_SERVICE} and ${MODERATOR_SERVICE}..."
  systemctl restart "${ADMIN_SERVICE}" "${MODERATOR_SERVICE}"

  if command -v curl >/dev/null 2>&1; then
    curl -sf http://127.0.0.1:7000/admin.html >/dev/null && echo "Admin portal: OK" || echo "Admin portal: unreachable"
    curl -sf http://127.0.0.1:7050/moderator.html >/dev/null && echo "Moderator portal: OK" || echo "Moderator portal: unreachable"
    curl -sf http://127.0.0.1:7000/js/currency.js >/dev/null && echo "Admin currency.js: OK" || echo "Admin currency.js: missing"
    curl -sf http://127.0.0.1:7000/images/currency.svg >/dev/null && echo "Admin currency.svg: OK" || echo "Admin currency.svg: missing"
  fi
}

echo "Using server sources: ${SERVER_DIR}"

if [ "$STATIC_ONLY" -eq 1 ] || ! rava_has_dotnet_sdk; then
  if [ "$STATIC_ONLY" -eq 0 ]; then
    echo "WARNING: No .NET SDK — syncing portal wwwroot only (DLLs unchanged)." >&2
    rava_print_missing_sdk_help >&2
  else
    echo "Static-only mode: syncing portal wwwroot (DLLs unchanged)."
  fi
  sync_portal_wwwroot
  verify_portal_files
  restart_portals
  echo "Done."
  exit 0
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

sync_portal_wwwroot

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

restart_portals
echo "Done."
