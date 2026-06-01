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
  install-bin-scripts.sh; do
  if [ ! -f "${SRC_DIR}/${script}" ]; then
    echo "Missing ${SRC_DIR}/${script}" >&2
    exit 1
  fi
  cp -f "${SRC_DIR}/${script}" "${LIB_DIR}/${script}"
  chmod 755 "${LIB_DIR}/${script}"
done

cp -f "${SRC_DIR}/systemd/"*.service "${LIB_DIR}/systemd/"

declare -A bin_links=(
  [restart-rava.sh]=restart-rava
  [diagnose-api.sh]=diagnose-rava-api
  [diagnose-portals.sh]=diagnose-rava-portals
  [install-systemd-units.sh]=install-rava-systemd
  [install-portal-units.sh]=install-rava-portals
  [install-bin-scripts.sh]=install-rava-scripts
)

for src in "${!bin_links[@]}"; do
  ln -sf "${LIB_DIR}/${src}" "${BIN_DIR}/${bin_links[$src]}"
  echo "Installed ${BIN_DIR}/${bin_links[$src]}"
done

echo "Done. Example: sudo restart-rava"
