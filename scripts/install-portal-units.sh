#!/bin/bash
# Install admin + moderator systemd units when rava-api and rava-status already exist.
# Run on the server as root:
#   sudo install-rava-portals
#   sudo bash scripts/install-portal-units.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
SYSTEMD_SRC="${SCRIPT_DIR}/systemd"
SYSTEMD_DST="/etc/systemd/system"

units=(rava-admin rava-moderator)

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

systemctl daemon-reload

for unit in "${units[@]}"; do
  systemctl enable "$unit"
  systemctl restart "$unit" || systemctl start "$unit"
  if systemctl is-active --quiet "$unit"; then
    echo "${unit}: running"
  else
    echo "${unit}: FAILED (check journalctl -u ${unit})" >&2
    exit 1
  fi
done

echo "Done."
