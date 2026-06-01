#!/bin/bash
# Sync game html/ from a repo checkout to the live publish folder.
# Run on the server as root after git pull:
#   sudo bash scripts/deploy-html.sh
#   sudo bash scripts/deploy-html.sh /path/to/rava-1/server/Rava.Api/html
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SRC_DIR="${1:-${SCRIPT_DIR}/../server/Rava.Api/html}"
DEST_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}/html"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -d "${SRC_DIR}" ]; then
  echo "Missing source html dir: ${SRC_DIR}" >&2
  exit 1
fi

rsync -av \
  --exclude 'uploads/' \
  --exclude 'images/profile/' \
  --exclude 'images/profile-backgrounds/' \
  --exclude 'exonet/offworld-news/editions/' \
  --exclude 'exonet/offworld-news/images/' \
  "${SRC_DIR}/" "${DEST_DIR}/"

echo "Deployed html to ${DEST_DIR}"
echo "Verify: node --check ${DEST_DIR}/js/api.js 2>/dev/null || sed -n '350,395p' ${DEST_DIR}/js/api.js"
