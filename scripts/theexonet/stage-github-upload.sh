#!/bin/bash
# Move a CI upload from githubdeploy home into staging (sudo only).
#
#   sudo stage-theexonet-upload /home/githubdeploy/upload-foo.tar.gz
#   sudo stage-theexonet-upload /home/githubdeploy/upload-foo.tar.gz theexonet-website-deploy-sha.tar.gz
#
# Second argument is the final archive basename. When provided, the file is placed under
# staging/.incoming/ so the staging watcher does not auto-promote mid-CI.
set -euo pipefail

SRC="${1:?usage: stage-github-upload.sh <source-file> [dest-basename]}"
DEST_BASENAME="${2:-}"
STAGING="${THEEXONET_STAGING_DIR:-/var/www/staging}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run via sudo." >&2
  exit 1
fi

resolve_src() {
  local candidate="$1"
  if [ -f "${candidate}" ]; then
    printf '%s' "${candidate}"
    return 0
  fi

  local deploy_user="${DEPLOY_SSH_USER:-githubdeploy}"
  local home_candidate="/home/${deploy_user}/${candidate}"
  if [ -f "${home_candidate}" ]; then
    printf '%s' "${home_candidate}"
    return 0
  fi

  return 1
}

if ! SRC="$(resolve_src "${SRC}")"; then
  echo "ERROR: missing ${1}" >&2
  exit 1
fi

src_base="$(basename "${SRC}")"
if [ -z "${DEST_BASENAME}" ]; then
  DEST_BASENAME="${src_base}"
  if [[ "${DEST_BASENAME}" == upload-* ]]; then
    DEST_BASENAME="${DEST_BASENAME#upload-}"
  fi
  DEST="${STAGING}/${DEST_BASENAME}"
else
  mkdir -p "${STAGING}/.incoming"
  DEST="${STAGING}/.incoming/${DEST_BASENAME}"
fi

mkdir -p "${STAGING}" "${STAGING}/.incoming"
mv -f "${SRC}" "${DEST}"
chmod 644 "${DEST}" 2>/dev/null || true
deploy_user="${DEPLOY_SSH_USER:-githubdeploy}"
deploy_group="${THEEXONET_SERVICE_GROUP:-theexonet}"
chown "${deploy_user}:${deploy_group}" "${DEST}" 2>/dev/null || true
echo "Staged ${DEST}"
