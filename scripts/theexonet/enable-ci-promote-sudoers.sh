#!/bin/bash
# Patch githubdeploy sudoers for CI deploy (promote + html). One-time on existing VMs.
#
#   cd /opt/theexonet/theexonet && git pull
#   sudo bash scripts/theexonet/enable-ci-promote-sudoers.sh
#   sudo bash scripts/install-bin-scripts.sh scripts
#   sudo fix-theexonet-permissions -q
set -euo pipefail

DEPLOY_USER="${DEPLOY_SSH_USER:-githubdeploy}"
STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"
SUDOERS="/etc/sudoers.d/theexonet-github-deploy"
CI_PROMOTE="${STAGING_DIR}/run-promote-staging.sh"
DEPLOY_HTML_BIN="/usr/local/bin/deploy-theexonet-html"
DEPLOY_HTML_LIB="${LIB_DIR}/deploy-html.sh"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ ! -f "${SUDOERS}" ]; then
  echo "ERROR: missing ${SUDOERS}. Run setup-github-ssh-restart.sh first." >&2
  exit 1
fi

add_to_alias() {
  local alias_name="$1"
  local entry="$2"
  if grep -q "${entry}" "${SUDOERS}"; then
    echo "Already allowed: ${entry}"
    return 0
  fi
  if grep -q "Cmnd_Alias ${alias_name} =" "${SUDOERS}"; then
    sed -i "s|Cmnd_Alias ${alias_name} = |Cmnd_Alias ${alias_name} = ${entry}, |" "${SUDOERS}"
  else
    printf 'Cmnd_Alias %s = %s\n' "${alias_name}" "${entry}" >>"${SUDOERS}"
    if ! grep -q "NOPASSWD: ${alias_name}" "${SUDOERS}"; then
      printf '%s ALL=(ALL) NOPASSWD: %s\n' "${DEPLOY_USER}" "${alias_name}" >>"${SUDOERS}"
    fi
  fi
  echo "Added ${entry} to ${alias_name}"
}

add_to_alias THEEXONET_PROMOTE "${CI_PROMOTE}"
add_to_alias THEEXONET_DEPLOY_HTML "${DEPLOY_HTML_BIN}"
add_to_alias THEEXONET_DEPLOY_HTML "${DEPLOY_HTML_LIB}"
visudo -cf "${SUDOERS}" >/dev/null

echo "Done. Also run:"
echo "  sudo bash scripts/install-bin-scripts.sh scripts"
echo "  sudo fix-theexonet-permissions -q"
