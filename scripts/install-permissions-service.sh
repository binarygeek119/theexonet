#!/bin/bash
# Install and enable the RAVA permissions watcher (runs as root).
#   sudo install-rava-permissions-service
#   sudo bash scripts/install-permissions-service.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
LIB_DIR="${RAVA_LIB_DIR:-/usr/local/lib/rava/scripts}"
SYSTEMD_DST="/etc/systemd/system"
DEFAULT_DST="/etc/default/rava-permissions"
UNIT="rava-permissions"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

for script in rava-hosting-env.sh audit-hosting-permissions.sh fix-hosting-permissions.sh rava-permissions-watch.sh; do
  if [ ! -f "${SCRIPT_DIR}/${script}" ]; then
    echo "Missing ${SCRIPT_DIR}/${script}" >&2
    exit 1
  fi
  cp -f "${SCRIPT_DIR}/${script}" "${LIB_DIR}/${script}"
  chmod 755 "${LIB_DIR}/${script}"
done

if [ ! -f "${SCRIPT_DIR}/systemd/${UNIT}.service" ]; then
  echo "Missing unit file: ${SCRIPT_DIR}/systemd/${UNIT}.service" >&2
  exit 1
fi

cp -f "${SCRIPT_DIR}/systemd/${UNIT}.service" "${SYSTEMD_DST}/${UNIT}.service"

if [ ! -f "${DEFAULT_DST}" ] && [ -f "${SCRIPT_DIR}/systemd/rava-permissions.default" ]; then
  cp -f "${SCRIPT_DIR}/systemd/rava-permissions.default" "${DEFAULT_DST}"
  chmod 644 "${DEFAULT_DST}"
  echo "Installed ${DEFAULT_DST}"
fi

systemctl daemon-reload
systemctl enable "${UNIT}"
systemctl restart "${UNIT}" || systemctl start "${UNIT}"

if systemctl is-active --quiet "${UNIT}"; then
  echo "${UNIT}: running"
else
  echo "${UNIT}: FAILED (check journalctl -u ${UNIT})" >&2
  exit 1
fi

echo "Done. Manual fix: sudo fix-rava-permissions | Audit: sudo audit-rava-permissions"
