#!/bin/bash
# Watch /var/www/staging for CI/FTP deploy zips and auto-run promote-staging.
# Run as root under systemd (theexonet-staging-watcher.service).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
STATE_FILE="${THEEXONET_STAGING_WATCHER_STATE:-/var/run/theexonet-staging-watcher.state}"
POLL_SECONDS="${THEEXONET_STAGING_POLL_SECONDS:-15}"

log() { echo "[staging-watcher] $*"; }

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root (use theexonet-staging-watcher.service)." >&2
  exit 1
fi

promote_if_needed() {
  shopt -s nullglob
  local zip newest_mtime=0 newest_zip=""
  for zip in \
    "${STAGING_DIR}"/theexonet-website-deploy-*.zip \
    "${STAGING_DIR}"/theexonet-website-*.zip \
    "${STAGING_DIR}"/*.zip; do
    [ -f "${zip}" ] || continue
    local mtime
    mtime="$(stat -c %Y "${zip}" 2>/dev/null || echo 0)"
    if [ "${mtime}" -gt "${newest_mtime}" ]; then
      newest_mtime="${mtime}"
      newest_zip="${zip}"
    fi
  done

  if [ -z "${newest_zip}" ]; then
    return 0
  fi

  local last=0
  if [ -f "${STATE_FILE}" ]; then
    last="$(cat "${STATE_FILE}" 2>/dev/null || echo 0)"
  fi
  if [ "${newest_mtime}" -le "${last}" ]; then
    return 0
  fi

  log "New deploy zip detected: ${newest_zip}"
  if command -v promote-theexonet-staging >/dev/null 2>&1; then
    promote-theexonet-staging
  else
    bash "${SCRIPT_DIR}/promote-staging.sh"
  fi
  echo "${newest_mtime}" >"${STATE_FILE}"
  log "Promote complete."
}

log "Watching ${STAGING_DIR} (poll ${POLL_SECONDS}s)…"
while true; do
  promote_if_needed || log "WARN: promote failed — will retry on next poll"
  sleep "${POLL_SECONDS}"
done
