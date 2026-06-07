#!/bin/bash
# Upload a deploy artifact to theexonet production via explicit FTPS (gameftp).
# Used by GitHub Actions production deploy (FTPS only).
#
# Env:
#   DEPLOY_FTP_HOST          — server hostname or IP (default: DEPLOY_HOST)
#   DEPLOY_FTP_USER          — must be gameftp (default gameftp)
#   DEPLOY_FTP_PASSWORD      — required
#   DEPLOY_FTP_PORT          — default 21
#   DEPLOY_FTP_STAGING_DIR   — path under FTPS chroot /var/www (default: staging)
#
# Usage:
#   bash scripts/github-deploy-ftp.sh path/to/theexonet-website-deploy.zip
set -euo pipefail

LOCAL_FILE="${1:?usage: github-deploy-ftp.sh <local-file>}"
if [ ! -f "${LOCAL_FILE}" ]; then
  echo "ERROR: file not found: ${LOCAL_FILE}" >&2
  exit 1
fi

FTP_HOST="${DEPLOY_FTP_HOST:-${DEPLOY_HOST:-}}"
FTP_USER="${DEPLOY_FTP_USER:-gameftp}"
FTP_PASSWORD="${DEPLOY_FTP_PASSWORD:-}"
FTP_PORT="${DEPLOY_FTP_PORT:-21}"
FTP_STAGING="${DEPLOY_FTP_STAGING_DIR:-staging}"

if [ -z "${FTP_HOST}" ]; then
  echo "ERROR: set DEPLOY_FTP_HOST or DEPLOY_HOST" >&2
  exit 1
fi
if [ -z "${FTP_PASSWORD}" ]; then
  echo "ERROR: set DEPLOY_FTP_PASSWORD" >&2
  exit 1
fi
if [ "${FTP_USER}" != "gameftp" ]; then
  echo "ERROR: DEPLOY_FTP_USER must be gameftp (not ${FTP_USER}). githubdeploy is SSH-only." >&2
  echo "Remove DEPLOY_FTP_USER from GitHub secrets/variables or set it to gameftp." >&2
  exit 1
fi

if ! command -v lftp >/dev/null 2>&1; then
  echo "ERROR: lftp is required (apt install lftp)" >&2
  exit 1
fi

REMOTE_NAME="$(basename "${LOCAL_FILE}")"
echo "Uploading ${LOCAL_FILE} → ftps://${FTP_HOST}:${FTP_PORT}/${FTP_STAGING}/${REMOTE_NAME} (user=${FTP_USER})"

# LFTP_PASSWORD avoids special-character breakage from -u user,pass on the command line.
export LFTP_PASSWORD="${FTP_PASSWORD}"
lftp --env-password -u "${FTP_USER}" -p "${FTP_PORT}" "${FTP_HOST}" <<EOF
set cmd:fail-exit yes
set ftp:ssl-force true
set ftp:ssl-protect-data true
set ssl:verify-certificate no
cd ${FTP_STAGING}
put ${LOCAL_FILE} -o ${REMOTE_NAME}
ls -la
bye
EOF

echo "FTPS upload complete: ${FTP_STAGING}/${REMOTE_NAME}"
