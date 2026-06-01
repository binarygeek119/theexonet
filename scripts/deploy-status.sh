#!/bin/bash
# Publish the status dashboard into the live publish folder and restart rava-status.
# Run on the server as root:
#   cd /opt/rava/rava && sudo deploy-rava-status
#   sudo deploy-rava-status /opt/rava/rava/server
#   sudo deploy-rava-status --static-only   # no SDK: sync wwwroot only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=dotnet-sdk.sh
source "${SCRIPT_DIR}/dotnet-sdk.sh"

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
STATUS_SERVICE="${RAVA_STATUS_SERVICE:-rava-status}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"
STATIC_ONLY=0
SERVER_ARG=""

for arg in "$@"; do
  case "$arg" in
    --static-only)
      STATIC_ONLY=1
      ;;
    -h|--help)
      echo "Usage: sudo deploy-rava-status [--static-only] [server-directory]" >&2
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
  echo "Run as root: sudo deploy-rava-status" >&2
  exit 1
fi

SERVER_DIR="$(bash "${SCRIPT_DIR}/resolve-server-dir.sh" "${SERVER_ARG}")"

if [ ! -f "${SERVER_DIR}/Rava.Status/Rava.Status.csproj" ]; then
  echo "Missing ${SERVER_DIR}/Rava.Status/Rava.Status.csproj" >&2
  exit 1
fi

sync_status_wwwroot() {
  local src="${SERVER_DIR}/Rava.Status/wwwroot"
  if [ ! -d "$src" ]; then
    echo "Missing ${src}" >&2
    exit 1
  fi
  rsync -a "${src}/" "${PUBLISH_DIR}/wwwroot/"
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${PUBLISH_DIR}/wwwroot" 2>/dev/null || true
}

restart_status() {
  echo "Restarting ${STATUS_SERVICE}..."
  systemctl restart "${STATUS_SERVICE}"

  if command -v curl >/dev/null 2>&1; then
    curl -sf http://127.0.0.1:6000/api/dashboard >/dev/null && echo "Status API: OK" || echo "Status API: unreachable"
    curl -sf http://127.0.0.1:6000/ >/dev/null && echo "Status dashboard: OK" || echo "Status dashboard: unreachable"
  fi
}

echo "Using server sources: ${SERVER_DIR}"

if [ "$STATIC_ONLY" -eq 1 ] || ! rava_has_dotnet_sdk; then
  if [ "$STATIC_ONLY" -eq 0 ]; then
    echo "WARNING: No .NET SDK — syncing status wwwroot only (Rava.Status.dll unchanged)." >&2
    rava_print_missing_sdk_help >&2
  else
    echo "Static-only mode: syncing status wwwroot (DLL unchanged)."
  fi
  sync_status_wwwroot
  for required in \
    "${PUBLISH_DIR}/wwwroot/index.html" \
    "${PUBLISH_DIR}/wwwroot/js/status.js"; do
    if [ ! -f "$required" ]; then
      echo "ERROR: missing ${required} after status deploy." >&2
      exit 1
    fi
  done
  restart_status
  echo "Done."
  exit 0
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

restart_status
echo "Done."
