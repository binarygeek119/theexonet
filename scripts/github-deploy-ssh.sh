#!/bin/bash
# Upload deploy zip via SSH (githubdeploy) and promote to /var/www/publish.
# Replaces FTPS in GitHub Actions — one password (DEPLOY_SSH_PASSWORD).
#
# Env:
#   DEPLOY_SSH_PASSWORD   — required
#   DEPLOY_SSH_HOST       — hostname or IP
#   DEPLOY_SSH_USER       — default githubdeploy
#   DEPLOY_SSH_PORT       — default 22
#   DEPLOY_STAGING_DIR    — default /var/www/staging
#
# Usage:
#   bash scripts/github-deploy-ssh.sh theexonet-website-deploy-<sha>.tar.gz
set -euo pipefail

LOCAL_FILE="${1:?usage: github-deploy-ssh.sh <local-zip>}"
if [ ! -f "${LOCAL_FILE}" ]; then
  echo "ERROR: file not found: ${LOCAL_FILE}" >&2
  exit 1
fi

HOST="${DEPLOY_SSH_HOST:-${DEPLOY_FTP_HOST:-${DEPLOY_HOST:-}}}"
USER="${DEPLOY_SSH_USER:-githubdeploy}"
PORT="${DEPLOY_SSH_PORT:-22}"
PASSWORD="${DEPLOY_SSH_PASSWORD:-}"
STAGING_DIR="${DEPLOY_STAGING_DIR:-/var/www/staging}"
REMOTE_NAME="$(basename "${LOCAL_FILE}")"
REMOTE_PATH="${STAGING_DIR%/}/${REMOTE_NAME}"

if [ -z "${PASSWORD}" ]; then
  echo "ERROR: set DEPLOY_SSH_PASSWORD." >&2
  exit 1
fi
if [ -z "${HOST}" ]; then
  echo "ERROR: set DEPLOY_SSH_HOST or DEPLOY_HOST." >&2
  exit 1
fi

for cmd in ssh scp sshpass; do
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    echo "ERROR: ${cmd} not found." >&2
    exit 1
  fi
done

ssh_common_opts=(
  -o BatchMode=no
  -o StrictHostKeyChecking=accept-new
  -o ConnectTimeout=60
  -o PreferredAuthentications=password
  -o PubkeyAuthentication=no
)
ssh_opts=(-p "${PORT}" "${ssh_common_opts[@]}")
# scp uses -P for port (-p means preserve file times).
scp_opts=(-P "${PORT}" "${ssh_common_opts[@]}")

export SSHPASS="${PASSWORD}"

echo "Uploading ${LOCAL_FILE} → ${USER}@${HOST}:${REMOTE_PATH}"
if ! SSHPASS="${PASSWORD}" sshpass -e scp "${scp_opts[@]}" "${LOCAL_FILE}" "${USER}@${HOST}:${REMOTE_PATH}"; then
  echo "Direct scp to ${REMOTE_PATH} failed — trying home dir + sudo mv…" >&2
  tmp_path="upload-${REMOTE_NAME}"
  SSHPASS="${PASSWORD}" sshpass -e scp "${scp_opts[@]}" "${LOCAL_FILE}" "${USER}@${HOST}:${tmp_path}"
  SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
    "sudo stage-theexonet-upload '${tmp_path}'"
fi

echo "Promote and restart…"
SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "sudo promote-theexonet-staging '${REMOTE_NAME}'"

echo "Verify game html on server…"
if ! SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "grep -q 'theexonet-html-build' /var/www/publish/html/index.html"; then
  echo "ERROR: /var/www/publish/html/index.html was not updated by promote." >&2
  echo "On the VM as root: sudo deploy-theexonet-html /opt/theexonet/theexonet/server/Theexonet.Api/html" >&2
  exit 1
fi

echo "SSH deploy complete."
