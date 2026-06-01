#!/bin/bash
# Watch RAVA services and data paths; fix hosting permissions when errors are detected.
# Intended to run as root under systemd (rava-permissions.service).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=rava-hosting-env.sh
source "${SCRIPT_DIR}/rava-hosting-env.sh"

AUDIT_SCRIPT="${SCRIPT_DIR}/audit-hosting-permissions.sh"
FIX_SCRIPT="${SCRIPT_DIR}/fix-hosting-permissions.sh"
LOCK_FILE="${RAVA_PERMISSIONS_LOCK:-/run/rava-permissions.fix.lock}"
POLL_INTERVAL="${POLL_INTERVAL:-30}"
DEBOUNCE_SECONDS="${DEBOUNCE_SECONDS:-10}"
JOURNAL_UNITS="${RAVA_JOURNAL_UNITS:-rava-api rava-admin rava-moderator rava-docs rava-status}"
JOURNAL_PATTERN="${RAVA_JOURNAL_PATTERN:-Permission denied|Access to the path|UnauthorizedAccessException|EACCES|is denied}"

LAST_FIX_EPOCH=0

log_msg() {
  logger -t rava-permissions -- "$*"
  echo "[rava-permissions] $*"
}

require_root() {
  if [ "$(id -u)" -ne 0 ]; then
    echo "Must run as root (use rava-permissions.service)." >&2
    exit 1
  fi
}

can_run_fix() {
  local now
  now="$(date +%s)"
  if [ $((now - LAST_FIX_EPOCH)) -lt "${DEBOUNCE_SECONDS}" ]; then
    return 1
  fi
  LAST_FIX_EPOCH=$now
  return 0
}

run_fix() {
  local reason="$1"

  if ! can_run_fix; then
    return 0
  fi

  if [ ! -x "${FIX_SCRIPT}" ]; then
    log_msg "fix script missing: ${FIX_SCRIPT}"
    return 1
  fi

  log_msg "Applying permission fix (${reason})"
  if "${FIX_SCRIPT}" -q; then
    if "${AUDIT_SCRIPT}" -q; then
      log_msg "Permissions OK after fix"
    else
      log_msg "WARNING: issues remain after fix — check audit-rava-permissions"
    fi
  else
    log_msg "ERROR: fix-rava-permissions failed"
    return 1
  fi
}

on_trigger() {
  local reason="$1"
  (
    flock -n 200 || exit 0
    run_fix "${reason}"
  ) 200>"${LOCK_FILE}"
}

poll_loop() {
  while true; do
    if [ -x "${AUDIT_SCRIPT}" ] && ! "${AUDIT_SCRIPT}" -q; then
      on_trigger "scheduled audit"
    fi
    sleep "${POLL_INTERVAL}"
  done
}

journal_loop() {
  local -a unit_args=()
  local unit

  for unit in ${JOURNAL_UNITS}; do
    unit_args+=("-u" "${unit}")
  done

  if [ "${#unit_args[@]}" -eq 0 ]; then
    log_msg "No journal units configured; journal watch disabled"
    return
  fi

  # Follow new log lines from RAVA units; react to permission-related errors.
  while true; do
    if ! journalctl -f -n 0 "${unit_args[@]}" 2>/dev/null \
      | grep -E --line-buffered -i "${JOURNAL_PATTERN}" \
      | while read -r _; do
          on_trigger "service log"
        done; then
      sleep 5
    fi
  done
}

require_root

if [ ! -x "${AUDIT_SCRIPT}" ] || [ ! -x "${FIX_SCRIPT}" ]; then
  echo "Missing audit or fix script under ${SCRIPT_DIR}" >&2
  exit 1
fi

mkdir -p "$(dirname "${LOCK_FILE}")"
touch "${LOCK_FILE}"

log_msg "Starting watcher (poll=${POLL_INTERVAL}s debounce=${DEBOUNCE_SECONDS}s user=${SERVICE_USER})"

# Fix once at startup if already broken (e.g. after deploy).
if ! "${AUDIT_SCRIPT}" -q; then
  LAST_FIX_EPOCH=0
  on_trigger "startup audit"
fi

poll_loop &
POLL_PID=$!

journal_loop &
JOURNAL_PID=$!

trap 'log_msg "Stopping"; kill ${POLL_PID} ${JOURNAL_PID} 2>/dev/null || true; wait 2>/dev/null || true' INT TERM

wait -n "${POLL_PID}" "${JOURNAL_PID}"
