#!/bin/bash
# Create a dedicated SSH user for GitHub Actions deploy (promote staging + restart game).
#
# One-time on the GCP VM as root:
#   sudo DEPLOY_SSH_PASSWORD='YourStrongDeployPassword' bash scripts/theexonet/setup-github-ssh-restart.sh
#
# GitHub → Secrets → DEPLOY_SSH_PASSWORD (same password)
# GitHub → Variables → DEPLOY_USER = githubdeploy
#
# Env:
#   DEPLOY_SSH_PASSWORD  — required
#   DEPLOY_SSH_USER      — default githubdeploy
set -euo pipefail

DEPLOY_USER="${DEPLOY_SSH_USER:-githubdeploy}"
DEPLOY_PASSWORD="${DEPLOY_SSH_PASSWORD:-}"
GAME_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"
STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ -z "${DEPLOY_PASSWORD}" ]; then
  echo "ERROR: Set DEPLOY_SSH_PASSWORD." >&2
  echo "  sudo DEPLOY_SSH_PASSWORD='...' bash scripts/theexonet/setup-github-ssh-restart.sh" >&2
  exit 1
fi

if ! getent group "${GAME_GROUP}" >/dev/null 2>&1; then
  groupadd "${GAME_GROUP}" 2>/dev/null || true
fi

if id "${DEPLOY_USER}" >/dev/null 2>&1; then
  echo "User ${DEPLOY_USER} already exists — updating password and sudoers."
  usermod -aG "${GAME_GROUP}" "${DEPLOY_USER}" 2>/dev/null || true
else
  useradd -m -s /bin/bash -G "${GAME_GROUP}" "${DEPLOY_USER}"
  echo "Created user ${DEPLOY_USER} (group ${GAME_GROUP})."
fi

echo "${DEPLOY_USER}:${DEPLOY_PASSWORD}" | chpasswd
echo "Set password for ${DEPLOY_USER}."

mkdir -p /etc/ssh/sshd_config.d
cat >/etc/ssh/sshd_config.d/99-theexonet-github-deploy.conf <<EOF
# Dedicated GitHub deploy user — password SSH only (setup-github-ssh-restart.sh).
PasswordAuthentication no
PermitRootLogin prohibit-password
KbdInteractiveAuthentication no

Match User ${DEPLOY_USER}
    PasswordAuthentication yes
    PermitTTY no
    X11Forwarding no
EOF
chmod 644 /etc/ssh/sshd_config.d/99-theexonet-github-deploy.conf

if sshd -t 2>/dev/null; then
  systemctl reload ssh 2>/dev/null || systemctl reload sshd 2>/dev/null || true
  echo "Reloaded sshd (${DEPLOY_USER} may use password; root key-only)."
else
  echo "WARN: sshd -t failed — check /etc/ssh/sshd_config.d/99-theexonet-github-deploy.conf" >&2
fi

sudoers="/etc/sudoers.d/theexonet-github-deploy"
cat >"${sudoers}" <<EOF
# Managed by setup-github-ssh-restart.sh — GitHub Actions game deploy only.
Cmnd_Alias THEEXONET_PROMOTE = /usr/local/bin/promote-theexonet-staging, ${LIB_DIR}/promote-staging.sh, ${LIB_DIR}/theexonet/promote-staging.sh, ${STAGING_DIR}/run-promote-staging.sh
Cmnd_Alias THEEXONET_RESTART = /usr/local/bin/restart-theexonet, ${LIB_DIR}/restart-theexonet.sh
Cmnd_Alias THEEXONET_FIX_PERMS = /usr/local/bin/fix-theexonet-permissions, ${LIB_DIR}/fix-hosting-permissions.sh
Cmnd_Alias THEEXONET_SYSTEMCTL = /bin/systemctl restart theexonet-api, /bin/systemctl restart theexonet-status, /bin/systemctl restart theexonet-admin, /bin/systemctl restart theexonet-moderator, /bin/systemctl restart theexonet-docs
Cmnd_Alias THEEXONET_STAGE_UPLOAD = /usr/local/bin/stage-theexonet-upload, ${LIB_DIR}/theexonet/stage-github-upload.sh
Cmnd_Alias THEEXONET_DEPLOY_HTML = /usr/local/bin/deploy-theexonet-html, ${LIB_DIR}/deploy-html.sh

${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_PROMOTE
${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_RESTART
${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_FIX_PERMS
${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_SYSTEMCTL
${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_STAGE_UPLOAD
${DEPLOY_USER} ALL=(ALL) NOPASSWD: THEEXONET_DEPLOY_HTML
EOF
chmod 440 "${sudoers}"
visudo -cf "${sudoers}" >/dev/null
echo "Wrote ${sudoers}"

mkdir -p "${STAGING_DIR}"
chown root:"${GAME_GROUP}" "${STAGING_DIR}"
chmod 2775 "${STAGING_DIR}"
usermod -aG "${GAME_GROUP}" "${DEPLOY_USER}" 2>/dev/null || true
echo "Staging upload dir: ${STAGING_DIR} (group ${GAME_GROUP}, mode 2775)"

echo ""
echo "=== GitHub deploy user ready ==="
echo "SSH user:     ${DEPLOY_USER}"
echo "Permissions:  upload to ${STAGING_DIR}, promote staging (delegates to run-promote-staging.sh), restart services"
echo "After git pull also run: sudo install-theexonet-scripts   # refresh promote-theexonet-staging delegation"
echo "GitHub secret: DEPLOY_SSH_PASSWORD (upload + promote — FTPS not required)"
echo "GitHub variable: DEPLOY_USER = ${DEPLOY_USER}"
echo ""
echo "Test:"
echo "  SSHPASS='***' sshpass -e ssh -o PubkeyAuthentication=no ${DEPLOY_USER}@theexonet.com 'sudo restart-theexonet'"
