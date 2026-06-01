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

mkdir -p \
  "${DEST_DIR}/images/profile" \
  "${DEST_DIR}/images/profile-backgrounds" \
  "${DEST_DIR}/exonet/offworld-news/editions" \
  "${DEST_DIR}/exonet/offworld-news/images"

rsync -av \
  --filter 'P images/profile/' \
  --filter 'P images/profile/***' \
  --filter 'P images/profile-backgrounds/' \
  --filter 'P images/profile-backgrounds/***' \
  --filter 'P exonet/offworld-news/editions/' \
  --filter 'P exonet/offworld-news/editions/***' \
  --filter 'P exonet/offworld-news/images/' \
  --filter 'P exonet/offworld-news/images/***' \
  --exclude 'uploads/' \
  --exclude 'images/profile/' \
  --exclude 'images/profile-backgrounds/' \
  --exclude 'exonet/offworld-news/editions/' \
  --exclude 'exonet/offworld-news/images/' \
  "${SRC_DIR}/" "${DEST_DIR}/"

chown -R "${SERVICE_USER}:${SERVICE_USER}" \
  "${DEST_DIR}/images" \
  "${DEST_DIR}/exonet/offworld-news" 2>/dev/null || true

echo "Deployed html to ${DEST_DIR}"
echo "Verify: node --check ${DEST_DIR}/js/api.js 2>/dev/null || sed -n '350,395p' ${DEST_DIR}/js/api.js"
