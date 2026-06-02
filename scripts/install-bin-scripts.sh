#!/usr/bin/env bash
# Install RAVA helper scripts to /usr/local/bin (files live under /usr/local/lib/rava/scripts).
# Run on the server as root (always use bash — do not rely on ./ if the file has Windows line endings):
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
  deploy-portals.sh \
  deploy-status.sh \
  resolve-server-dir.sh \
  dotnet-sdk.sh \
  sync-portal-wwwroot.sh \
  sync-status-wwwroot.sh \
  sync-docs-wwwroot.sh \
  sync-publish-wwwroot.sh \
  migrate-publish-data-to-var-www.sh \
  sync-publish-data.sh \
  fix-hosting-permissions.sh \
  audit-hosting-permissions.sh \
  rava-hosting-env.sh \
  rava-permissions-watch.sh \
  install-permissions-service.sh \
  install-rava-permissions-service.sh \
  install-bin-scripts.sh; do
  if [ ! -f "${SRC_DIR}/${script}" ]; then
    echo "Missing ${SRC_DIR}/${script}" >&2
    exit 1
  fi
  cp -f "${SRC_DIR}/${script}" "${LIB_DIR}/${script}"
  chmod 755 "${LIB_DIR}/${script}"
done

cp -f "${SRC_DIR}/systemd/"*.service "${LIB_DIR}/systemd/"
if [ -f "${SRC_DIR}/systemd/rava-permissions.default" ]; then
  cp -f "${SRC_DIR}/systemd/rava-permissions.default" "${LIB_DIR}/systemd/rava-permissions.default"
fi

REPO_API_DIR="${SRC_DIR}/../server/Rava.Api"
if [ -d "${REPO_API_DIR}" ]; then
  mkdir -p "${TEMPLATE_DIR}"
  for csv in credits.csv market-items.csv trade-items.csv hate-speech-terms.csv bad-language-terms.csv political-terms.csv sexual-terms.csv offworld-news-reporters.csv; do
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
      --exclude 'exonet/offworld-news/reporters/' \
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
  [deploy-portals.sh]=deploy-rava-portals
  [deploy-status.sh]=deploy-rava-status
  [sync-publish-data.sh]=sync-rava-data
  [migrate-publish-data-to-var-www.sh]=migrate-rava-data
  [fix-hosting-permissions.sh]=fix-rava-permissions
  [audit-hosting-permissions.sh]=audit-rava-permissions
  [install-permissions-service.sh]=install-rava-permissions-service
  [install-rava-permissions-service.sh]=install-rava-permissions-service
)

for src in "${!bin_links[@]}"; do
  ln -sf "${LIB_DIR}/${src}" "${BIN_DIR}/${bin_links[$src]}"
  echo "Installed ${BIN_DIR}/${bin_links[$src]}"
done

echo "Done. Example: sudo restart-rava | sudo install-rava-permissions-service"
