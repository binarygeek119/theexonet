#!/bin/bash
# Move a CI upload from githubdeploy home into /var/www/staging (sudo only).
#   sudo stage-theexonet-upload /home/githubdeploy/upload-foo.zip
set -euo pipefail

SRC="${1:?usage: stage-github-upload.sh <source-file>}"
STAGING="${THEEXONET_STAGING_DIR:-/var/www/staging}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run via sudo." >&2
  exit 1
fi
if [ ! -f "${SRC}" ]; then
  echo "ERROR: missing ${SRC}" >&2
  exit 1
fi

mkdir -p "${STAGING}"
DEST="${STAGING}/$(basename "${SRC}")"
mv -f "${SRC}" "${DEST}"
echo "Staged ${DEST}"
