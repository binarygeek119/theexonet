#!/bin/bash
# Paste this entire file into Google Cloud Shell (https://shell.cloud.google.com).
# 1. Replace YOUR_GCP_PROJECT_ID below.
# 2. Run: bash cloud-shell-bootstrap.sh
#
# Or one-liner after editing project id:
#   curl -fsSL ... | bash   (once scripts are on GitHub main)
set -euo pipefail

export GCP_PROJECT_ID="${GCP_PROJECT_ID:-YOUR_GCP_PROJECT_ID}"
export THEEXONET_SSH_PUBLIC_KEY="${THEEXONET_SSH_PUBLIC_KEY:-ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIK4wYtpaB65Jn20lmbi89dqXC7nS4VQvj13tio5C5aHH root@theexonet}"

if [ "${GCP_PROJECT_ID}" = "YOUR_GCP_PROJECT_ID" ]; then
  echo "ERROR: Set GCP_PROJECT_ID to your Google Cloud project id." >&2
  echo "  export GCP_PROJECT_ID=my-project-123" >&2
  exit 1
fi

WORKDIR="${HOME}/theexonet-setup"
mkdir -p "${WORKDIR}"
cd "${WORKDIR}"

if [ -d theexonet/.git ]; then
  git -C theexonet pull --ff-only
else
  git clone --depth 1 https://github.com/binarygeek119/theexonet.git
fi

bash theexonet/scripts/theexonet/create-gcp-vm.sh
