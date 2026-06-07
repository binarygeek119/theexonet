#!/bin/bash
# Enable password SSH so GitHub Actions can promote/restart after FTPS deploy.
#
# One-time on the GCP VM as root (use the same password you will store in GitHub):
#   sudo DEPLOY_SSH_PASSWORD='YourStrongPassword' bash scripts/theexonet/setup-github-ssh-restart.sh
#
# Then GitHub → Settings → Secrets → DEPLOY_SSH_PASSWORD (same value).
#
# Env:
#   DEPLOY_SSH_PASSWORD  — required; sets login password for DEPLOY_SSH_USER
#   DEPLOY_SSH_USER      — default root
set -euo pipefail

DEPLOY_USER="${DEPLOY_SSH_USER:-root}"
DEPLOY_PASSWORD="${DEPLOY_SSH_PASSWORD:-}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ -z "${DEPLOY_PASSWORD}" ]; then
  echo "ERROR: Set DEPLOY_SSH_PASSWORD (the password GitHub Actions will use)." >&2
  echo "  sudo DEPLOY_SSH_PASSWORD='...' bash scripts/theexonet/setup-github-ssh-restart.sh" >&2
  exit 1
fi

if ! id "${DEPLOY_USER}" >/dev/null 2>&1; then
  echo "ERROR: user ${DEPLOY_USER} does not exist." >&2
  exit 1
fi

echo "${DEPLOY_USER}:${DEPLOY_PASSWORD}" | chpasswd
echo "Set password for ${DEPLOY_USER}"

mkdir -p /etc/ssh/sshd_config.d
cat >/etc/ssh/sshd_config.d/99-theexonet-deploy-password.conf <<'EOF'
# GitHub Actions deploy: password SSH for promote/restart (setup-github-ssh-restart.sh).
PasswordAuthentication yes
PermitRootLogin yes
KbdInteractiveAuthentication no
EOF
chmod 644 /etc/ssh/sshd_config.d/99-theexonet-deploy-password.conf

if sshd -t 2>/dev/null; then
  systemctl reload ssh 2>/dev/null || systemctl reload sshd 2>/dev/null || true
  echo "Reloaded sshd (password login enabled)."
else
  echo "WARN: sshd -t failed — check /etc/ssh/sshd_config.d/99-theexonet-deploy-password.conf" >&2
fi

# Non-root deploy users need passwordless sudo for promote/restart.
if [ "${DEPLOY_USER}" != "root" ]; then
  sudoers="/etc/sudoers.d/theexonet-deploy"
  cat >"${sudoers}" <<EOF
# Managed by setup-github-ssh-restart.sh — GitHub Actions deploy restart only.
${DEPLOY_USER} ALL=(ALL) NOPASSWD: /usr/local/bin/promote-theexonet-staging
${DEPLOY_USER} ALL=(ALL) NOPASSWD: /usr/local/lib/theexonet/scripts/theexonet/promote-staging.sh
${DEPLOY_USER} ALL=(ALL) NOPASSWD: /usr/local/bin/restart-theexonet
${DEPLOY_USER} ALL=(ALL) NOPASSWD: /usr/local/lib/theexonet/scripts/restart-theexonet.sh
EOF
  chmod 440 "${sudoers}"
  visudo -cf "${sudoers}" >/dev/null
  echo "Wrote ${sudoers} for user ${DEPLOY_USER}"
fi

echo ""
echo "=== Password SSH deploy ready ==="
echo "Login user: ${DEPLOY_USER}"
echo "GitHub secret: DEPLOY_SSH_PASSWORD (same password you passed to this script)"
echo "Optional vars: DEPLOY_USER=${DEPLOY_USER}, DEPLOY_SSH_PORT=22"
echo ""
echo "Test from your PC (install sshpass locally if needed):"
echo "  SSHPASS='***' sshpass -e ssh -o PubkeyAuthentication=no ${DEPLOY_USER}@theexonet.com 'restart-theexonet'"
