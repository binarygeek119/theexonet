#!/bin/bash
# Allow githubdeploy to run the CI-uploaded promote script (one-time on existing VMs).
#
#   cd /opt/theexonet/theexonet && git pull
#   sudo bash scripts/theexonet/enable-ci-promote-sudoers.sh
#   sudo bash scripts/install-bin-scripts.sh scripts
set -euo pipefail

DEPLOY_USER="${DEPLOY_SSH_USER:-githubdeploy}"
STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
SUDOERS="/etc/sudoers.d/theexonet-github-deploy"
CI_PROMOTE="${STAGING_DIR}/run-promote-staging.sh"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ ! -f "${SUDOERS}" ]; then
  echo "ERROR: missing ${SUDOERS}. Run setup-github-ssh-restart.sh first." >&2
  exit 1
fi

if grep -q "${CI_PROMOTE}" "${SUDOERS}"; then
  echo "Already allowed: ${CI_PROMOTE}"
else
  sed -i "s|Cmnd_Alias THEEXONET_PROMOTE = |Cmnd_Alias THEEXONET_PROMOTE = ${CI_PROMOTE}, |" "${SUDOERS}"
  visudo -cf "${SUDOERS}" >/dev/null
  echo "Added ${CI_PROMOTE} to THEEXONET_PROMOTE in ${SUDOERS}"
fi

echo "Done. Also run: sudo bash scripts/install-bin-scripts.sh scripts"
