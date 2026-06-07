#!/bin/bash
# Copy required CSV spreadsheets into the live data directory (/var/www/data by default).
# Overwrites existing CSV files with the newest version from the source directory.
# Source resolution (when no argument): publish dir, then git checkout Theexonet.Api, then templates.
# Run on the server as root after git pull or deploy:
#   sudo bash scripts/sync-publish-data.sh
#   sudo bash scripts/sync-publish-data.sh /var/www/publish
#   sudo sync-theexonet-data
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
TEMPLATE_DIR="${THEEXONET_TEMPLATE_DATA_DIR:-/usr/local/lib/theexonet/data}"
DEST_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

resolve_src_dir() {
  local requested="${1:-}"

  if [ -n "${requested}" ] && [ -d "${requested}" ] && [ -f "${requested}/credits.csv" ]; then
    printf '%s' "${requested}"
    return
  fi

  if [ -f "${PUBLISH_DIR}/credits.csv" ]; then
    printf '%s' "${PUBLISH_DIR}"
    return
  fi

  local repo_api="${SCRIPT_DIR}/../server/Theexonet.Api"
  if [ -f "${repo_api}/credits.csv" ]; then
    printf '%s' "$repo_api"
    return
  fi

  if [ -f "${TEMPLATE_DIR}/credits.csv" ]; then
    printf '%s' "$TEMPLATE_DIR"
    return
  fi

  echo "Missing CSV source. Pass server/Theexonet.Api, git pull + install-theexonet-scripts, or populate ${TEMPLATE_DIR}." >&2
  exit 1
}

SRC_DIR="$(resolve_src_dir "${1:-}")"

files=(
  credits.csv
  market-items.csv
  trade-items.csv
  hate-speech-terms.csv
  bad-language-terms.csv
  political-terms.csv
  sexual-terms.csv
  offworld-news-reporters.csv
)

missing_src=0
for file in "${files[@]}"; do
  if [ ! -f "${SRC_DIR}/${file}" ]; then
    echo "MISSING in source ${SRC_DIR}: ${file}" >&2
    missing_src=1
  fi
done
if [ "$missing_src" -ne 0 ]; then
  exit 1
fi

mkdir -p "${DEST_DIR}"
for file in "${files[@]}"; do
  cp -f "${SRC_DIR}/${file}" "${DEST_DIR}/${file}"
  echo "Updated ${DEST_DIR}/${file} from ${SRC_DIR}/${file}"
done

if [ -f "${PUBLISH_DIR}/appsettings.json" ] && [ ! -f "${DEST_DIR}/appsettings.json" ]; then
  cp -f "${PUBLISH_DIR}/appsettings.json" "${DEST_DIR}/appsettings.json"
  echo "Migrated ${PUBLISH_DIR}/appsettings.json -> ${DEST_DIR}/appsettings.json"
fi

chown "${SERVICE_USER}:${SERVICE_USER}" "${DEST_DIR}" "${DEST_DIR}"/*.csv 2>/dev/null || true
if [ -f "${DEST_DIR}/appsettings.json" ]; then
  chown "${SERVICE_USER}:${SERVICE_USER}" "${DEST_DIR}/appsettings.json" 2>/dev/null || true
fi
echo "Done."
