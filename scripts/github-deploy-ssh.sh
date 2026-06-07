#!/bin/bash
# Upload deploy bundle via SSH (githubdeploy) and promote to /var/www/publish.
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

LOCAL_FILE="${1:?usage: github-deploy-ssh.sh <local-tar.gz>}"
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
REMOTE_PROMOTE="${STAGING_DIR%/}/run-promote-staging.sh"
SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
PROMOTE_SCRIPT="${SCRIPT_DIR}/theexonet/promote-staging.sh"
HTML_BUILD_MARKER="${HTML_BUILD_MARKER:-20260607-checkbox-grid}"

if [ -z "${PASSWORD}" ]; then
  echo "ERROR: set DEPLOY_SSH_PASSWORD." >&2
  exit 1
fi
if [ -z "${HOST}" ]; then
  echo "ERROR: set DEPLOY_SSH_HOST or DEPLOY_HOST." >&2
  exit 1
fi
if [ ! -f "${PROMOTE_SCRIPT}" ]; then
  echo "ERROR: missing ${PROMOTE_SCRIPT}" >&2
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
    "sudo -n stage-theexonet-upload '${tmp_path}'"
fi

echo "Uploading promote script from CI checkout…"
SSHPASS="${PASSWORD}" sshpass -e scp "${scp_opts[@]}" "${PROMOTE_SCRIPT}" "${USER}@${HOST}:${REMOTE_PROMOTE}"
SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "chmod 755 '${REMOTE_PROMOTE}'"

echo "Promote and restart (sudo promote-theexonet-staging)…"
promote_output=""
if ! promote_output="$(SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "sudo -n promote-theexonet-staging '${REMOTE_NAME}'" 2>&1)"; then
  echo "${promote_output}"
  echo "ERROR: promote-theexonet-staging failed." >&2
  echo "On the VM (one-time after git pull):" >&2
  echo "  sudo bash scripts/install-bin-scripts.sh scripts" >&2
  exit 1
fi
echo "${promote_output}"

if ! printf '%s' "${promote_output}" | grep -q 'Unpacking '; then
  echo "ERROR: promote did not unpack the staging archive (installed promote script is stale)." >&2
  echo "On the VM:" >&2
  echo "  cd /opt/theexonet/theexonet && git pull" >&2
  echo "  sudo bash scripts/install-bin-scripts.sh scripts" >&2
  exit 1
fi

echo "Verify game html on production…"
game_host="${HOST#https://}"
game_host="${game_host#http://}"
game_host="${game_host%%/*}"
verify_url="https://${game_host}/index.html"
if ! curl -sf --max-time 30 "${verify_url}" | grep -q "content=\"${HTML_BUILD_MARKER}\""; then
  echo "ERROR: ${verify_url} is missing html build marker ${HTML_BUILD_MARKER}." >&2
  echo "On the VM:" >&2
  echo "  sudo deploy-theexonet-html /opt/theexonet/theexonet/server/Theexonet.Api/html" >&2
  exit 1
fi

echo "SSH deploy complete (${HTML_BUILD_MARKER} live on ${verify_url})."
