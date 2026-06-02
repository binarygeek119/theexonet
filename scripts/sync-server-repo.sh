#!/bin/bash
# Keep the on-server git checkout in sync (manual / local use).
# CI deploy rsyncs the repository from GitHub Actions instead — no server git credentials needed.
#
# Usage:
#   bash scripts/sync-server-repo.sh
#   bash scripts/sync-server-repo.sh /opt/rava/rava abc123def git@github.com:owner/rava.git
set -euo pipefail

REPO_PATH="${1:-${RAVA_REPO_DIR:-/opt/rava/rava}}"
TARGET_REF="${2:-origin/main}"
REPO_URL="${3:-${RAVA_REPO_URL:-git@github.com:binarygeek119/rava.git}}"

looks_like_repo() {
  local path="$1"
  [ -d "${path}/.git" ] \
    && { [ -f "${path}/server/Rava.slnx" ] || [ -f "${path}/server/Rava.Api/Rava.Api.csproj" ]; }
}

sync_existing() {
  cd "${REPO_PATH}"
  git fetch origin --prune
  if git cat-file -e "${TARGET_REF}^{commit}" 2>/dev/null; then
    git reset --hard "${TARGET_REF}"
  else
    git fetch origin "${TARGET_REF}" 2>/dev/null || git fetch origin main
    git reset --hard "${TARGET_REF}"
  fi
  git clean -fd
}

clone_repo() {
  local parent
  parent="$(dirname "${REPO_PATH}")"
  mkdir -p "${parent}"
  if [ -d "${REPO_PATH}" ]; then
    echo "ERROR: ${REPO_PATH} exists but is not a RAVA git checkout." >&2
    exit 1
  fi
  git clone "${REPO_URL}" "${REPO_PATH}"
  cd "${REPO_PATH}"
  git reset --hard "${TARGET_REF}"
}

if looks_like_repo "${REPO_PATH}"; then
  sync_existing
elif [ -d "${REPO_PATH}/.git" ]; then
  echo "ERROR: ${REPO_PATH} is a git repo but missing server/ sources." >&2
  exit 1
else
  clone_repo
fi

if [ ! -f "${REPO_PATH}/server/Rava.Api/Rava.Api.csproj" ]; then
  echo "ERROR: ${REPO_PATH}/server is missing after sync." >&2
  exit 1
fi

echo "Server repo synced at ${REPO_PATH} (${TARGET_REF})"
