#!/bin/bash
# Install RAVA helper scripts to /usr/local/bin (files live under /usr/local/lib/rava/scripts).
# Run on the server as root:
#   sudo bash scripts/install-bin-scripts.sh
# Re-run after git pull to refresh lib files and symlinks:
#   sudo install-rava-scripts
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SRC_DIR="${1:-$SCRIPT_DIR}"
LIB_DIR="${RAVA_LIB_DIR:-/usr/local/lib/rava/scripts}"
TEMPLATE_DIR="${RAVA_TEMPLATE_DATA_DIR:-/usr/local/lib/rava/data}"
HTML_TEMPLATE_DIR="${RAVA_HTML_TEMPLATE_DIR:-/usr/local/lib/rava/html}"
BIN_DIR="/usr/local/bin"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -d "${SRC_DIR}/systemd" ]; then
  echo "Missing ${SRC_DIR}/systemd — pass the repo scripts directory." >&2
  exit 1
fi

mkdir -p "${LIB_DIR}/systemd"

for script in \
  restart-rava.sh \
  diagnose-api.sh \
  diagnose-portals.sh \
  install-systemd-units.sh \
  install-portal-units.sh \
  deploy-html.sh \
  migrate-publish-data-to-var-www.sh \
  sync-publish-data.sh \
  fix-hosting-permissions.sh \
  install-bin-scripts.sh; do
  if [ ! -f "${SRC_DIR}/${script}" ]; then
    echo "Missing ${SRC_DIR}/${script}" >&2
    exit 1
  fi
  cp -f "${SRC_DIR}/${script}" "${LIB_DIR}/${script}"
  chmod 755 "${LIB_DIR}/${script}"
done

cp -f "${SRC_DIR}/systemd/"*.service "${LIB_DIR}/systemd/"

REPO_API_DIR="${SRC_DIR}/../server/Rava.Api"
if [ -d "${REPO_API_DIR}" ]; then
  mkdir -p "${TEMPLATE_DIR}"
  for csv in credits.csv market-items.csv trade-items.csv hate-speech-terms.csv bad-language-terms.csv political-terms.csv sexual-terms.csv; do
    if [ -f "${REPO_API_DIR}/${csv}" ]; then
      cp -f "${REPO_API_DIR}/${csv}" "${TEMPLATE_DIR}/${csv}"
    fi
  done
  echo "Installed CSV template files to ${TEMPLATE_DIR}"

  if [ -d "${REPO_API_DIR}/html" ]; then
    mkdir -p "${HTML_TEMPLATE_DIR}"
    rsync -a --delete \
      --exclude 'uploads/' \
      --exclude 'images/profile/' \
      --exclude 'images/profile-backgrounds/' \
      --exclude 'exonet/offworld-news/editions/' \
      --exclude 'exonet/offworld-news/images/' \
      "${REPO_API_DIR}/html/" "${HTML_TEMPLATE_DIR}/"
    echo "Installed html template to ${HTML_TEMPLATE_DIR}"
  fi
fi

declare -A bin_links=(
  [restart-rava.sh]=restart-rava
  [diagnose-api.sh]=diagnose-rava-api
  [diagnose-portals.sh]=diagnose-rava-portals
  [install-systemd-units.sh]=install-rava-systemd
  [install-portal-units.sh]=install-rava-portals
  [install-bin-scripts.sh]=install-rava-scripts
  [deploy-html.sh]=deploy-rava-html
  [sync-publish-data.sh]=sync-rava-data
  [migrate-publish-data-to-var-www.sh]=migrate-rava-data
  [fix-hosting-permissions.sh]=fix-rava-permissions
)

for src in "${!bin_links[@]}"; do
  ln -sf "${LIB_DIR}/${src}" "${BIN_DIR}/${bin_links[$src]}"
  echo "Installed ${BIN_DIR}/${bin_links[$src]}"
done

echo "Done. Example: sudo restart-rava"
