#!/bin/bash
# Copy Rava.Status dashboard static files into a wwwroot folder.
set -euo pipefail

SERVER_DIR="${1:?server directory (contains Rava.Status)}"
WWWROOT_DIR="${2:?wwwroot destination directory}"

STATUS_SRC="${SERVER_DIR}/Rava.Status/wwwroot"

if [ ! -d "$STATUS_SRC" ]; then
  echo "Missing status wwwroot: ${STATUS_SRC}" >&2
  exit 1
fi

mkdir -p "$WWWROOT_DIR"
rsync -a "${STATUS_SRC}/" "${WWWROOT_DIR}/"

for required in \
  "${WWWROOT_DIR}/index.html" \
  "${WWWROOT_DIR}/js/status.js" \
  "${WWWROOT_DIR}/ai.html" \
  "${WWWROOT_DIR}/favicon.svg"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: ${required} missing after status wwwroot sync." >&2
    exit 1
  fi
done

echo "Synced status wwwroot assets into ${WWWROOT_DIR}"
