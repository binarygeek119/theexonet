#!/bin/bash
# Install theexonet-staging-watcher.service (auto-promote FTP/CI uploads).
#   sudo bash scripts/install-staging-watcher.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"
UNIT="theexonet-staging-watcher"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if ! command -v unzip >/dev/null 2>&1 || ! command -v rsync >/dev/null 2>&1; then
  apt-get update -y
  apt-get install -y unzip rsync
fi

mkdir -p "${LIB_DIR}/theexonet" "${LIB_DIR}/systemd"
cp -f "${SCRIPT_DIR}/theexonet/staging-watcher.sh" "${LIB_DIR}/theexonet/staging-watcher.sh"
cp -f "${SCRIPT_DIR}/theexonet/promote-staging.sh" "${LIB_DIR}/theexonet/promote-staging.sh"
chmod 755 "${LIB_DIR}/theexonet/staging-watcher.sh" "${LIB_DIR}/theexonet/promote-staging.sh"
cp -f "${SCRIPT_DIR}/systemd/theexonet-staging-watcher.service" "/etc/systemd/system/${UNIT}.service"

systemctl daemon-reload
systemctl enable "${UNIT}"
systemctl restart "${UNIT}"
echo "Installed ${UNIT}.service — $(systemctl is-active ${UNIT})"
