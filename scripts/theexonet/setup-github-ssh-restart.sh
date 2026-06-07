#!/bin/bash
# Allow GitHub Actions (or your laptop) to SSH in and promote/restart after FTPS deploy.
#
# One-time on the GCP VM as root:
#   1) On your PC, generate a deploy key:
#        ssh-keygen -t ed25519 -f ~/.ssh/theexonet-github-deploy -C "github-actions-deploy" -N ""
#   2) Copy the PUBLIC key to the server and run:
#        sudo DEPLOY_SSH_PUBLIC_KEY="$(cat ~/.ssh/theexonet-github-deploy.pub)" \
#          bash scripts/theexonet/setup-github-ssh-restart.sh
#   3) Add the PRIVATE key to GitHub → Settings → Secrets → DEPLOY_SSH_KEY
#
# Optional: also authorize your personal key (default ~/.ssh/id_ed25519.pub on the machine you run this from):
#   sudo bash scripts/theexonet/setup-github-ssh-restart.sh
#
# Env:
#   DEPLOY_SSH_PUBLIC_KEY   — public key line for CI (required unless DEPLOY_SSH_PUBKEY_FILE)
#   DEPLOY_SSH_PUBKEY_FILE  — path to CI public key file
#   DEPLOY_SSH_USER         — login user (default root)
#   PERSONAL_SSH_PUBKEY_FILE — extra authorized key (default $HOME/.ssh/id_ed25519.pub if present)
set -euo pipefail

DEPLOY_USER="${DEPLOY_SSH_USER:-root}"
DEPLOY_PUBKEY_FILE="${DEPLOY_SSH_PUBKEY_FILE:-}"
PERSONAL_PUBKEY_FILE="${PERSONAL_SSH_PUBKEY_FILE:-${HOME}/.ssh/id_ed25519.pub}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if ! id "${DEPLOY_USER}" >/dev/null 2>&1; then
  echo "ERROR: user ${DEPLOY_USER} does not exist." >&2
  exit 1
fi

home_dir="$(getent passwd "${DEPLOY_USER}" | cut -d: -f6)"
auth_keys="${home_dir}/.ssh/authorized_keys"
mkdir -p "${home_dir}/.ssh"
chmod 700 "${home_dir}/.ssh"
touch "${auth_keys}"
chmod 600 "${auth_keys}"

add_key() {
  local label="$1"
  local pubkey="$2"
  pubkey="$(printf '%s' "${pubkey}" | tr -d '\r\n')"
  if [ -z "${pubkey}" ]; then
    return 0
  fi
  if grep -qF "${pubkey}" "${auth_keys}" 2>/dev/null; then
    echo "Already authorized (${label})"
    return 0
  fi
  echo "${pubkey} ${label}" >>"${auth_keys}"
  echo "Added SSH key (${label}) for ${DEPLOY_USER}"
}

if [ -n "${DEPLOY_SSH_PUBLIC_KEY:-}" ]; then
  add_key "github-actions-deploy" "${DEPLOY_SSH_PUBLIC_KEY}"
elif [ -n "${DEPLOY_PUBKEY_FILE}" ] && [ -f "${DEPLOY_PUBKEY_FILE}" ]; then
  add_key "github-actions-deploy" "$(cat "${DEPLOY_PUBKEY_FILE}")"
else
  echo "WARN: No DEPLOY_SSH_PUBLIC_KEY — only personal key will be added (if found)." >&2
  echo "      Generate a dedicated CI key; do not put your personal private key in GitHub." >&2
fi

if [ -f "${PERSONAL_PUBKEY_FILE}" ]; then
  add_key "personal-$(basename "${PERSONAL_PUBKEY_FILE}" .pub)" "$(cat "${PERSONAL_PUBKEY_FILE}")"
fi

chown -R "${DEPLOY_USER}:${DEPLOY_USER}" "${home_dir}/.ssh"

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
echo "=== SSH restart deploy ready ==="
echo "Login user: ${DEPLOY_USER}"
echo "Test from your PC:"
echo "  ssh -i ~/.ssh/theexonet-github-deploy ${DEPLOY_USER}@\$(dig +short theexonet.com | head -1) 'promote-theexonet-staging || restart-theexonet'"
echo ""
echo "GitHub secret: DEPLOY_SSH_KEY = contents of theexonet-github-deploy (private key, including BEGIN/END lines)"
echo "Optional vars: DEPLOY_USER=${DEPLOY_USER}, DEPLOY_SSH_PORT=22"
