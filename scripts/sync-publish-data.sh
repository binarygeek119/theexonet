#!/bin/bash
# Copy required CSV spreadsheets from a repo checkout into the live publish folder.
# Run on the server as root after git pull:
#   sudo bash scripts/sync-publish-data.sh
#   sudo bash scripts/sync-publish-data.sh /path/to/rava-1/server/Rava.Api
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SRC_DIR="${1:-${SCRIPT_DIR}/../server/Rava.Api}"
DEST_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -d "${SRC_DIR}" ]; then
  echo "Missing source API dir: ${SRC_DIR}" >&2
  exit 1
fi

files=(
  credits.csv
  market-items.csv
  trade-items.csv
  hate-speech-terms.csv
  bad-language-terms.csv
  political-terms.csv
  sexual-terms.csv
)

missing_src=0
for file in "${files[@]}"; do
  if [ ! -f "${SRC_DIR}/${file}" ]; then
    echo "MISSING in repo: ${SRC_DIR}/${file}" >&2
    missing_src=1
  fi
done
if [ "$missing_src" -ne 0 ]; then
  exit 1
fi

mkdir -p "${DEST_DIR}"
for file in "${files[@]}"; do
  cp -f "${SRC_DIR}/${file}" "${DEST_DIR}/${file}"
  echo "Installed ${DEST_DIR}/${file}"
done

chown "${SERVICE_USER}:${SERVICE_USER}" "${DEST_DIR}"/*.csv 2>/dev/null || true
echo "Done. Restart API: sudo restart-rava"
