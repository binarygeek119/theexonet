#!/bin/bash
# Copy Theexonet.Docs portal static files into a shared wwwroot folder.
set -euo pipefail

SERVER_DIR="${1:?server directory (contains Theexonet.Docs)}"
WWWROOT_DIR="${2:?wwwroot destination directory}"

DOCS_SRC="${SERVER_DIR}/Theexonet.Docs/wwwroot"

if [ ! -d "$DOCS_SRC" ]; then
  echo "Missing docs wwwroot: ${DOCS_SRC}" >&2
  exit 1
fi

mkdir -p "$WWWROOT_DIR"
rsync -a "${DOCS_SRC}/" "${WWWROOT_DIR}/"

if [ ! -f "${WWWROOT_DIR}/css/docs.css" ]; then
  echo "ERROR: ${WWWROOT_DIR}/css/docs.css missing after docs wwwroot sync." >&2
  exit 1
fi

echo "Synced docs wwwroot assets into ${WWWROOT_DIR}"
