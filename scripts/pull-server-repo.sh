#!/bin/bash
# git pull on the production server without "dubious ownership" errors.
# Run as root (or any user) after the repo was cloned by someone else:
#   sudo bash scripts/pull-server-repo.sh
#   sudo pull-theexonet-repo
set -euo pipefail

REPO_PATH="${1:-${THEEXONET_REPO_DIR:-/opt/theexonet/theexonet}}"

if [ ! -d "${REPO_PATH}/.git" ]; then
  echo "ERROR: not a git repo: ${REPO_PATH}" >&2
  exit 1
fi

cd "${REPO_PATH}"
git -c safe.directory="${REPO_PATH}" pull --ff-only
echo "Updated ${REPO_PATH}"
