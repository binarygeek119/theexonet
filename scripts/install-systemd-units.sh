#!/bin/bash
# Install theexonet systemd units on production. Run on the server as root:
#   sudo install-theexonet-systemd
#   sudo bash scripts/install-systemd-units.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SYSTEMD_SRC="${SCRIPT_DIR}/systemd"
SYSTEMD_DST="/etc/systemd/system"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"

units=(theexonet-api theexonet-status theexonet-admin theexonet-moderator theexonet-docs theexonet-permissions)

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

if [ -f "${SYSTEMD_SRC}/theexonet-permissions.default" ] && [ ! -f /etc/default/theexonet-permissions ]; then
  cp -f "${SYSTEMD_SRC}/theexonet-permissions.default" /etc/default/theexonet-permissions
  chmod 644 /etc/default/theexonet-permissions
  echo "Installed /etc/default/theexonet-permissions"
fi

mkdir -p "${LIB_DIR}/systemd"
for script in theexonet-hosting-env.sh audit-hosting-permissions.sh fix-hosting-permissions.sh theexonet-permissions-watch.sh install-permissions-service.sh install-theexonet-permissions-service.sh; do
  if [ -f "${SCRIPT_DIR}/${script}" ]; then
    cp -f "${SCRIPT_DIR}/${script}" "${LIB_DIR}/${script}"
    chmod 755 "${LIB_DIR}/${script}"
  fi
done
if [ -d "${SYSTEMD_SRC}" ]; then
  cp -f "${SYSTEMD_SRC}/"*.service "${LIB_DIR}/systemd/" 2>/dev/null || true
  if [ -f "${SYSTEMD_SRC}/theexonet-permissions.default" ]; then
    cp -f "${SYSTEMD_SRC}/theexonet-permissions.default" "${LIB_DIR}/systemd/theexonet-permissions.default"
  fi
fi

BIN_DIR="/usr/local/bin"
for link in install-theexonet-permissions-service fix-theexonet-permissions audit-theexonet-permissions; do
  case "${link}" in
    install-theexonet-permissions-service)
      target="${LIB_DIR}/install-theexonet-permissions-service.sh" ;;
    fix-theexonet-permissions)
      target="${LIB_DIR}/fix-hosting-permissions.sh" ;;
    audit-theexonet-permissions)
      target="${LIB_DIR}/audit-hosting-permissions.sh" ;;
  esac
  if [ -f "${target}" ]; then
    ln -sf "${target}" "${BIN_DIR}/${link}"
  fi
done

systemctl daemon-reload

PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
can_start_services=1
if [ ! -f "${PUBLISH_DIR}/Theexonet.Api.dll" ]; then
  can_start_services=0
  echo "Publish bundle not deployed yet (${PUBLISH_DIR}/Theexonet.Api.dll missing) — enabling units only."
fi

for unit in "${units[@]}"; do
  systemctl enable "$unit"
  if [ "${can_start_services}" -eq 1 ]; then
    systemctl restart "$unit" || systemctl start "$unit"
    if systemctl is-active --quiet "$unit"; then
      echo "${unit}: running"
    else
      echo "${unit}: FAILED (check journalctl -u ${unit})" >&2
    fi
  else
    systemctl stop "$unit" 2>/dev/null || true
    echo "${unit}: enabled (waiting for deploy)"
  fi
done

echo "Done."
