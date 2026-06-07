#!/bin/bash
# Audit theexonet hosting directory ownership and writability.
# Exit 0 when healthy, 1 when issues are found.
#   sudo audit-rava-permissions
#   sudo audit-rava-permissions -q    # no output unless issues
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=rava-hosting-env.sh
source "${SCRIPT_DIR}/rava-hosting-env.sh"

QUIET=0
if [ "${1:-}" = "-q" ] || [ "${1:-}" = "--quiet" ]; then
  QUIET=1
fi

ISSUES=0

log_issue() {
  ISSUES=$((ISSUES + 1))
  if [ "$QUIET" -eq 0 ]; then
    echo "ISSUE  $*"
  fi
}

require_root() {
  if [ "$(id -u)" -ne 0 ]; then
    echo "Run as root: sudo bash $0" >&2
    exit 2
  fi
}

require_service_user() {
  if ! id "${SERVICE_USER}" >/dev/null 2>&1; then
    echo "Service user not found: ${SERVICE_USER}" >&2
    exit 2
  fi
}

owner_matches() {
  local path="$1"
  local expected="${SERVICE_USER}:${SERVICE_GROUP}"
  local actual
  actual="$(stat -c '%U:%G' "${path}" 2>/dev/null || echo "")"
  [ "${actual}" = "${expected}" ]
}

dir_mode_ok() {
  local path="$1"
  local mode
  mode="$(stat -c '%a' "${path}" 2>/dev/null || echo "")"
  # Accept setgid group-writable dirs (2775) or group-writable (775).
  [ "${mode}" = "2775" ] || [ "${mode}" = "775" ]
}

writable_by_service_user() {
  local path="$1"
  sudo -u "${SERVICE_USER}" test -w "${path}"
}

audit_writable_dir() {
  local path="$1"
  local label="${2:-${path}}"

  if [ ! -d "${path}" ]; then
    log_issue "${label}: missing directory"
    return
  fi

  if ! owner_matches "${path}"; then
    log_issue "${label}: owner is $(stat -c '%U:%G' "${path}") expected ${SERVICE_USER}:${SERVICE_GROUP}"
  fi

  if ! dir_mode_ok "${path}"; then
    log_issue "${label}: mode is $(stat -c '%a' "${path}") expected 2775 (or 775)"
  fi

  if ! writable_by_service_user "${path}"; then
    log_issue "${label}: not writable by ${SERVICE_USER}"
  fi
}

audit_publish_tree() {
  if [ ! -d "${PUBLISH_DIR}" ]; then
    log_issue "publish: missing ${PUBLISH_DIR}"
    return
  fi

  for path in "${PUBLISH_WRITABLE_DIRS[@]}"; do
    audit_writable_dir "${path}" "publish:${path}"
  done

  if [ -d "${PUBLISH_DIR}/wwwroot" ]; then
    if ! owner_matches "${PUBLISH_DIR}/wwwroot"; then
      log_issue "publish:wwwroot: owner is $(stat -c '%U:%G' "${PUBLISH_DIR}/wwwroot") expected ${SERVICE_USER}:${SERVICE_GROUP}"
    fi
  fi
}

audit_data_tree() {
  for path in "${DATA_WRITABLE_DIRS[@]}"; do
    audit_writable_dir "${path}" "data:${path}"
  done
}

require_root
require_service_user

if [ "$QUIET" -eq 0 ]; then
  echo "Auditing theexonet hosting permissions (user=${SERVICE_USER})..."
fi

audit_publish_tree
audit_data_tree

if [ "$QUIET" -eq 0 ]; then
  if [ "$ISSUES" -eq 0 ]; then
    echo "OK  no permission issues detected"
  else
    echo "Found ${ISSUES} issue(s). Run: sudo fix-rava-permissions"
  fi
fi

exit "$([ "$ISSUES" -eq 0 ] && echo 0 || echo 1)"
