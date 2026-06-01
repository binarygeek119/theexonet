#!/bin/bash
# Sync game html/ from a repo checkout or installed template to the live publish folder.
# Run on the server as root:
#   sudo deploy-rava-html
#   sudo deploy-rava-html /path/to/rava-1/server/Rava.Api/html
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
HTML_TEMPLATE_DIR="${RAVA_HTML_TEMPLATE_DIR:-/usr/local/lib/rava/html}"
DEST_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}/html"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

resolve_src_dir() {
  if [ -n "${1:-}" ] && [ -d "$1" ]; then
    printf '%s' "$1"
    return
  fi

  local repo_html="${SCRIPT_DIR}/../server/Rava.Api/html"
  if [ -f "${repo_html}/index.html" ]; then
    printf '%s' "$repo_html"
    return
  fi

  if [ -f "${HTML_TEMPLATE_DIR}/index.html" ]; then
    printf '%s' "$HTML_TEMPLATE_DIR"
    return
  fi

  echo "Missing html source. Options:" >&2
  echo "  1) Pass a path: sudo deploy-rava-html /path/to/server/Rava.Api/html" >&2
  echo "  2) From a git checkout: sudo install-rava-scripts   # copies html to ${HTML_TEMPLATE_DIR}" >&2
  echo "  3) Let GitHub Actions deploy html on push to main" >&2
  exit 1
}

SRC_DIR="$(resolve_src_dir "${1:-}")"

rsync -av \
  --exclude 'uploads/' \
  --exclude 'images/profile/' \
  --exclude 'images/profile-backgrounds/' \
  --exclude 'exonet/offworld-news/editions/' \
  --exclude 'exonet/offworld-news/images/' \
  --exclude 'exonet/offworld-news/reporters/' \
  "${SRC_DIR}/" "${DEST_DIR}/"

echo "Deployed html from ${SRC_DIR} to ${DEST_DIR}"
echo "Verify: node --check ${DEST_DIR}/js/api.js 2>/dev/null || sed -n '350,395p' ${DEST_DIR}/js/api.js"
