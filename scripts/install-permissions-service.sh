#!/bin/bash
# Install and enable the theexonet permissions watcher (runs as root).
#   sudo install-theexonet-permissions-service
#   sudo bash scripts/install-permissions-service.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"
SYSTEMD_DST="/etc/systemd/system"
DEFAULT_DST="/etc/default/theexonet-permissions"
UNIT="theexonet-permissions"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

resolve_unit_file() {
  local candidate
  for candidate in \
    "${SCRIPT_DIR}/systemd/${UNIT}.service" \
    "${LIB_DIR}/systemd/${UNIT}.service"; do
    if [ -f "${candidate}" ]; then
      printf '%s' "${candidate}"
      return 0
    fi
  done
  return 1
}

resolve_default_file() {
  local candidate
  for candidate in \
    "${SCRIPT_DIR}/systemd/theexonet-permissions.default" \
    "${LIB_DIR}/systemd/theexonet-permissions.default"; do
    if [ -f "${candidate}" ]; then
      printf '%s' "${candidate}"
      return 0
    fi
  done
  return 1
}

mkdir -p "${LIB_DIR}/systemd"

for script in theexonet-hosting-env.sh audit-hosting-permissions.sh fix-hosting-permissions.sh theexonet-permissions-watch.sh install-permissions-service.sh install-theexonet-permissions-service.sh; do
  if [ -f "${SCRIPT_DIR}/${script}" ]; then
    cp -f "${SCRIPT_DIR}/${script}" "${LIB_DIR}/${script}"
    chmod 755 "${LIB_DIR}/${script}"
  fi
done

unit_src="$(resolve_unit_file)" || {
  echo "Missing unit file ${UNIT}.service under ${SCRIPT_DIR}/systemd or ${LIB_DIR}/systemd." >&2
  echo "Run from a git checkout or: sudo install-theexonet-scripts" >&2
  exit 1
}

cp -f "${unit_src}" "${SYSTEMD_DST}/${UNIT}.service"
cp -f "${unit_src}" "${LIB_DIR}/systemd/${UNIT}.service"

if [ ! -f "${DEFAULT_DST}" ]; then
  default_src="$(resolve_default_file)" || true
  if [ -n "${default_src:-}" ]; then
    cp -f "${default_src}" "${DEFAULT_DST}"
    chmod 644 "${DEFAULT_DST}"
    cp -f "${default_src}" "${LIB_DIR}/systemd/theexonet-permissions.default"
    echo "Installed ${DEFAULT_DST}"
  fi
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

echo "Done. Manual fix: sudo fix-theexonet-permissions | Audit: sudo audit-theexonet-permissions"
