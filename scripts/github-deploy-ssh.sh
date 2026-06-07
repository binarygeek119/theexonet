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
HTML_BUILD_MARKER="${HTML_BUILD_MARKER:-20260607-live-updates}"

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

run_remote_sudo() {
  local sudo_cmd="$1"
  shift
  local remote_cmd="sudo -n ${sudo_cmd}"
  local arg
  for arg in "$@"; do
    remote_cmd+=" '$(printf '%s' "${arg}" | sed "s/'/'\\\\''/g")'"
  done
  SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
    "${remote_cmd}" 2>&1
}

echo "Unpacking ${REMOTE_NAME} in staging (as ${USER})…"
unpack_output=""
if ! unpack_output="$(SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "STAGING='${STAGING_DIR}' ARCHIVE='${REMOTE_PATH}' bash -s" <<'REMOTE_UNPACK'
set -euo pipefail
if [ ! -f "${ARCHIVE}" ]; then
  echo "ERROR: missing archive: ${ARCHIVE}" >&2
  exit 1
fi
unpack_dir="$(mktemp -d "${STAGING}/.unpack.XXXXXX")"
tar -xzf "${ARCHIVE}" -C "${unpack_dir}"
if [ ! -f "${unpack_dir}/publish/Theexonet.Api.dll" ]; then
  echo "ERROR: archive missing publish/Theexonet.Api.dll" >&2
  rm -rf "${unpack_dir}"
  exit 1
fi
if [ ! -f "${unpack_dir}/publish/html/index.html" ]; then
  echo "ERROR: archive missing publish/html/index.html" >&2
  rm -rf "${unpack_dir}"
  exit 1
fi
rm -rf "${STAGING}/publish" "${STAGING}/data"
mv "${unpack_dir}/publish" "${STAGING}/"
if [ -d "${unpack_dir}/data" ]; then
  mv "${unpack_dir}/data" "${STAGING}/"
fi
rm -rf "${unpack_dir}"
shopt -s nullglob
for stale in \
  "${STAGING}"/theexonet-website-deploy-*.tar.gz \
  "${STAGING}"/theexonet-website-deploy-*.zip \
  "${STAGING}"/theexonet-website-deploy-*.tgz \
  "${STAGING}"/theexonet-website-*.tar.gz \
  "${STAGING}"/theexonet-website-*.zip; do
  rm -f "${stale}"
done
echo "Unpacking ${ARCHIVE}…"
REMOTE_UNPACK
)"; then
  echo "${unpack_output}"
  echo "ERROR: failed to unpack deploy bundle in staging." >&2
  exit 1
fi
echo "${unpack_output}"

echo "Promote and restart (sudo promote-theexonet-staging)…"
promote_output=""
if ! promote_output="$(run_remote_sudo promote-theexonet-staging)"; then
  echo "${promote_output}"
  echo "Installed promote failed — trying CI-uploaded ${REMOTE_PROMOTE}…" >&2
  if ! promote_output="$(run_remote_sudo "${REMOTE_PROMOTE}")"; then
    echo "${promote_output}"
    echo "ERROR: promote failed." >&2
    echo "On the VM (one-time after git pull):" >&2
    echo "  sudo bash scripts/theexonet/enable-ci-promote-sudoers.sh" >&2
    echo "  sudo bash scripts/install-bin-scripts.sh scripts" >&2
    exit 1
  fi
fi
echo "${promote_output}"

if ! printf '%s' "${promote_output}" | grep -q 'Promoted staging'; then
  echo "ERROR: promote did not sync staging to /var/www/publish." >&2
  exit 1
fi

STAGING_HTML="${STAGING_DIR%/}/publish/html"
LIVE_HTML="/var/www/publish/html/index.html"

echo "Sync game html from staging…"
html_output=""
if ! html_output="$(SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "STAGING_HTML='${STAGING_HTML}' LIVE_HTML='${LIVE_HTML}' HTML_BUILD_MARKER='${HTML_BUILD_MARKER}' bash -s" <<'REMOTE_HTML'
set -euo pipefail
MARKER="content=\"${HTML_BUILD_MARKER}\""
STAGING_INDEX="${STAGING_HTML}/index.html"
LIVE_DIR="$(dirname "${LIVE_HTML}")"

if [ ! -f "${STAGING_INDEX}" ]; then
  echo "ERROR: missing ${STAGING_INDEX}" >&2
  exit 1
fi
if ! grep -q "${MARKER}" "${STAGING_INDEX}"; then
  echo "ERROR: staging html missing build marker ${HTML_BUILD_MARKER}" >&2
  exit 1
fi

if [ -f "${LIVE_HTML}" ] && grep -q "${MARKER}" "${LIVE_HTML}"; then
  echo "Game html already has build marker ${HTML_BUILD_MARKER}."
  exit 0
fi

sync_html() {
  rsync -a --delete \
    --exclude 'uploads/' \
    --exclude 'images/profile/' \
    --exclude 'images/profile-backgrounds/' \
    --exclude 'exonet/offworld-news/editions/' \
    --exclude 'exonet/offworld-news/images/' \
    --exclude 'exonet/offworld-news/reporters/' \
    "${STAGING_HTML}/" "${LIVE_DIR}/"
}

if sync_html 2>/dev/null; then
  echo "Synced html from staging (as $(whoami))."
  exit 0
fi

sudo -n fix-theexonet-permissions -q 2>/dev/null || true
if sync_html 2>/dev/null; then
  echo "Synced html from staging after fix-theexonet-permissions."
  exit 0
fi

for deploy_cmd in deploy-theexonet-html /usr/local/bin/deploy-theexonet-html; do
  if sudo -n "${deploy_cmd}" "${STAGING_HTML}" 2>/dev/null; then
    echo "Deployed html via ${deploy_cmd}."
    exit 0
  fi
done

echo "ERROR: could not sync html to ${LIVE_DIR}." >&2
echo "Staging perms:" >&2
ls -ld "${STAGING_HTML}" "${STAGING_INDEX}" >&2 || true
echo "Live perms:" >&2
ls -ld "${LIVE_DIR}" "${LIVE_HTML}" >&2 || true
echo "On the VM (one-time after git pull):" >&2
echo "  sudo bash scripts/theexonet/enable-ci-promote-sudoers.sh" >&2
echo "  sudo fix-theexonet-permissions -q" >&2
exit 1
REMOTE_HTML
)"; then
  echo "${html_output}"
  exit 1
fi
echo "${html_output}"

echo "Verify html on server disk…"
if ! SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "grep -q 'content=\"${HTML_BUILD_MARKER}\"' '${LIVE_HTML}'"; then
  echo "ERROR: ${LIVE_HTML} is missing build marker ${HTML_BUILD_MARKER} after html sync." >&2
  exit 1
fi

echo "Verify Apache serves game html…"
game_host="${HOST#https://}"
game_host="${game_host#http://}"
game_host="${game_host%%/*}"
if ! SSHPASS="${PASSWORD}" sshpass -e ssh "${ssh_opts[@]}" "${USER}@${HOST}" \
  "GAME_HOST='${game_host}' HTML_BUILD_MARKER='${HTML_BUILD_MARKER}' bash -s" <<'REMOTE_VERIFY'
set -euo pipefail
MARKER="content=\"${HTML_BUILD_MARKER}\""
check_url() {
  local url="$1"
  local -a curl_opts=(-sf --max-time 15 -H "Host: ${GAME_HOST}")
  case "${url}" in
    https:*) curl_opts=(-k "${curl_opts[@]}") ;;
  esac
  curl "${curl_opts[@]}" "${url}" | grep -q "${MARKER}"
}
if check_url "https://127.0.0.1/index.html" 2>/dev/null \
  || check_url "http://127.0.0.1/index.html" 2>/dev/null; then
  echo "Apache serves ${HTML_BUILD_MARKER} for Host: ${GAME_HOST}"
  exit 0
fi
echo "ERROR: Apache on 127.0.0.1 is not serving ${HTML_BUILD_MARKER} for Host: ${GAME_HOST}." >&2
apache2ctl -S 2>/dev/null | grep -E 'DocumentRoot|${GAME_HOST}' >&2 || true
exit 1
REMOTE_VERIFY
then
  echo "ERROR: local Apache html verify failed." >&2
  exit 1
fi

cache_bust="${GITHUB_SHA:-ci}"
verify_url="https://${game_host}/index.html?ci=${cache_bust}"
if curl -sf --max-time 30 -H 'Cache-Control: no-cache' "${verify_url}" | grep -q "content=\"${HTML_BUILD_MARKER}\""; then
  echo "SSH deploy complete (${HTML_BUILD_MARKER} live on ${verify_url})."
else
  echo "WARN: public ${verify_url} did not return ${HTML_BUILD_MARKER} (CDN/DNS cache may lag)."
  echo "SSH deploy complete on server disk and local Apache (${HTML_BUILD_MARKER})."
fi
