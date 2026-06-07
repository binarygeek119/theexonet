#!/bin/bash
# SSH to production (password auth), promote FTPS staging zip, and restart services.
# Used by GitHub Actions after github-deploy-ftp.sh when DEPLOY_SSH_PASSWORD is set.
#
# Env:
#   DEPLOY_SSH_PASSWORD   — SSH login password (required to run; empty = skip)
#   DEPLOY_SSH_HOST       — hostname or IP (default DEPLOY_FTP_HOST or DEPLOY_HOST)
#   DEPLOY_SSH_USER       — default githubdeploy
#   DEPLOY_SSH_PORT       — default 22
#   DEPLOY_SSH_WAIT_SEC   — seconds after FTPS before promote (default 5)
set -euo pipefail

HOST="${DEPLOY_SSH_HOST:-${DEPLOY_FTP_HOST:-${DEPLOY_HOST:-}}}"
USER="${DEPLOY_SSH_USER:-githubdeploy}"
PORT="${DEPLOY_SSH_PORT:-22}"
WAIT_SEC="${DEPLOY_SSH_WAIT_SEC:-5}"
PASSWORD="${DEPLOY_SSH_PASSWORD:-}"

if [ -z "${PASSWORD}" ]; then
  echo "DEPLOY_SSH_PASSWORD not set — skipping SSH promote/restart (staging-watcher must promote)."
  exit 0
fi

if [ -z "${HOST}" ]; then
  echo "ERROR: Set DEPLOY_SSH_HOST, DEPLOY_FTP_HOST, or DEPLOY_HOST." >&2
  exit 1
fi

if ! command -v ssh >/dev/null 2>&1; then
  echo "ERROR: ssh not found." >&2
  exit 1
fi

if ! command -v sshpass >/dev/null 2>&1; then
  echo "ERROR: sshpass not found (required for password SSH)." >&2
  exit 1
fi

ssh_opts=(
  -p "${PORT}"
  -o StrictHostKeyChecking=accept-new
  -o ConnectTimeout=30
  -o PreferredAuthentications=password
  -o PubkeyAuthentication=no
)

echo "Waiting ${WAIT_SEC}s for FTPS upload to finish…"
sleep "${WAIT_SEC}"

remote_cmd='sudo promote-theexonet-staging || sudo restart-theexonet'

echo "SSH promote/restart on ${USER}@${HOST}:${PORT} (password auth)…"
SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" "${remote_cmd}"
echo "SSH promote/restart complete."
