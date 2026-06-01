#!/bin/bash
# Install RAVA systemd units on production. Run on the server as root:
#   sudo install-rava-systemd
#   sudo bash scripts/install-systemd-units.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SYSTEMD_SRC="${SCRIPT_DIR}/systemd"
SYSTEMD_DST="/etc/systemd/system"
LIB_DIR="${RAVA_LIB_DIR:-/usr/local/lib/rava/scripts}"

units=(rava-api rava-status rava-admin rava-moderator rava-docs rava-permissions)

for unit in "${units[@]}"; do
  src="${SYSTEMD_SRC}/${unit}.service"
  dst="${SYSTEMD_DST}/${unit}.service"
  if [ ! -f "$src" ]; then
    echo "Missing unit file: $src" >&2
    exit 1
  fi
  cp "$src" "$dst"
  echo "Installed ${dst}"
done

if [ -f "${SYSTEMD_SRC}/rava-permissions.default" ] && [ ! -f /etc/default/rava-permissions ]; then
  cp -f "${SYSTEMD_SRC}/rava-permissions.default" /etc/default/rava-permissions
  chmod 644 /etc/default/rava-permissions
  echo "Installed /etc/default/rava-permissions"
fi

mkdir -p "${LIB_DIR}/systemd"
for script in rava-hosting-env.sh audit-hosting-permissions.sh fix-hosting-permissions.sh rava-permissions-watch.sh install-permissions-service.sh install-rava-permissions-service.sh; do
  if [ -f "${SCRIPT_DIR}/${script}" ]; then
    cp -f "${SCRIPT_DIR}/${script}" "${LIB_DIR}/${script}"
    chmod 755 "${LIB_DIR}/${script}"
  fi
done
if [ -d "${SYSTEMD_SRC}" ]; then
  cp -f "${SYSTEMD_SRC}/"*.service "${LIB_DIR}/systemd/" 2>/dev/null || true
  if [ -f "${SYSTEMD_SRC}/rava-permissions.default" ]; then
    cp -f "${SYSTEMD_SRC}/rava-permissions.default" "${LIB_DIR}/systemd/rava-permissions.default"
  fi
fi

BIN_DIR="/usr/local/bin"
for link in install-rava-permissions-service fix-rava-permissions audit-rava-permissions; do
  case "${link}" in
    install-rava-permissions-service)
      target="${LIB_DIR}/install-rava-permissions-service.sh" ;;
    fix-rava-permissions)
      target="${LIB_DIR}/fix-hosting-permissions.sh" ;;
    audit-rava-permissions)
      target="${LIB_DIR}/audit-hosting-permissions.sh" ;;
  esac
  if [ -f "${target}" ]; then
    ln -sf "${target}" "${BIN_DIR}/${link}"
  fi
done

systemctl daemon-reload

for unit in "${units[@]}"; do
  systemctl enable "$unit"
  systemctl restart "$unit" || systemctl start "$unit"
  if systemctl is-active --quiet "$unit"; then
    echo "${unit}: running"
  else
    echo "${unit}: FAILED (check journalctl -u ${unit})" >&2
  fi
done

echo "Done."
