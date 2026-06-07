#!/bin/bash
# Create theexonet on Google Cloud Always Free tier (Midwest = us-central1, Iowa).
#
# Prerequisites:
#   - gcloud CLI installed and logged in: gcloud auth login
#   - Billing enabled on the project (required even for free tier)
#   - SSH public key at ~/.ssh/id_ed25519.pub, or THEEXONET_SSH_PUBLIC_KEY, or THEEXONET_SSH_PUBKEY_FILE
#
# Usage:
#   export GCP_PROJECT_ID=your-project-id
#   bash scripts/theexonet/create-gcp-vm.sh
#
# Optional env:
#   THEEXONET_ZONE=us-central1-a
#   THEEXONET_SSH_PUBKEY_FILE=~/.ssh/id_ed25519.pub
#   THEEXONET_RESERVE_STATIC_IP=true
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VM_NAME="${THEEXONET_VM_NAME:-theexonet}"
ZONE="${THEEXONET_ZONE:-us-central1-a}"
REGION="${THEEXONET_REGION:-us-central1}"
MACHINE_TYPE="${THEEXONET_MACHINE_TYPE:-e2-micro}"
DISK_SIZE_GB="${THEEXONET_DISK_GB:-30}"
DISK_TYPE="${THEEXONET_DISK_TYPE:-pd-standard}"
IMAGE_FAMILY="${THEEXONET_IMAGE_FAMILY:-ubuntu-2204-lts}"
IMAGE_PROJECT="${THEEXONET_IMAGE_PROJECT:-ubuntu-os-cloud}"
SSH_PUBKEY_FILE="${THEEXONET_SSH_PUBKEY_FILE:-${HOME}/.ssh/id_ed25519.pub}"
RESERVE_IP="${THEEXONET_RESERVE_STATIC_IP:-false}"

if ! command -v gcloud >/dev/null 2>&1; then
  echo "ERROR: gcloud not found. Install Google Cloud SDK or use Cloud Shell." >&2
  echo "  https://cloud.google.com/sdk/docs/install" >&2
  exit 1
fi

ACTIVE_ACCOUNT="$(gcloud auth list --filter=status:ACTIVE --format='value(account)' 2>/dev/null || true)"
if [ -z "${ACTIVE_ACCOUNT}" ]; then
  echo "ERROR: No active gcloud account. Run:" >&2
  echo "  gcloud auth login" >&2
  echo "  gcloud config set project YOUR_PROJECT_ID" >&2
  echo "Or use Google Cloud Shell (already logged in): https://shell.cloud.google.com" >&2
  exit 1
fi

if [ -z "${GCP_PROJECT_ID:-}" ]; then
  GCP_PROJECT_ID="$(gcloud config get-value project 2>/dev/null || true)"
fi
if [ -z "${GCP_PROJECT_ID}" ] || [ "${GCP_PROJECT_ID}" = "(unset)" ]; then
  echo "ERROR: Set GCP_PROJECT_ID or run: gcloud config set project YOUR_PROJECT_ID" >&2
  exit 1
fi

gcloud config set project "${GCP_PROJECT_ID}" >/dev/null

if [ -n "${THEEXONET_SSH_PUBLIC_KEY:-}" ]; then
  SSH_KEY_LINE="root:${THEEXONET_SSH_PUBLIC_KEY}"
elif [ -f "${SSH_PUBKEY_FILE}" ]; then
  SSH_KEY_LINE="root:$(cat "${SSH_PUBKEY_FILE}")"
else
  echo "ERROR: No SSH public key. Set THEEXONET_SSH_PUBLIC_KEY or THEEXONET_SSH_PUBKEY_FILE." >&2
  echo "  Windows: cat \$env:USERPROFILE\\.ssh\\id_ed25519.pub" >&2
  echo "  Or: ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -C 'root@theexonet'" >&2
  exit 1
fi
STARTUP_SCRIPT="${SCRIPT_DIR}/startup-install.sh"
if [ ! -f "${STARTUP_SCRIPT}" ]; then
  echo "ERROR: Missing ${STARTUP_SCRIPT}" >&2
  exit 1
fi

echo "Project:     ${GCP_PROJECT_ID}"
echo "VM:          ${VM_NAME}"
echo "Zone:        ${ZONE} (Always Free Midwest)"
echo "Machine:     ${MACHINE_TYPE}"
echo "Disk:        ${DISK_SIZE_GB} GB ${DISK_TYPE}"
echo "Image:       ${IMAGE_FAMILY}"
echo ""

echo "Ensuring firewall rules for HTTP/HTTPS…"
if ! gcloud compute firewall-rules describe theexonet-http-https --project="${GCP_PROJECT_ID}" >/dev/null 2>&1; then
  gcloud compute firewall-rules create theexonet-http-https \
    --project="${GCP_PROJECT_ID}" \
    --direction=INGRESS \
    --priority=1000 \
    --network=default \
    --action=ALLOW \
    --rules=tcp:22,tcp:80,tcp:443 \
    --source-ranges=0.0.0.0/0 \
    --target-tags=theexonet-web
fi

STATIC_IP=""
if [ "${RESERVE_IP}" = "true" ]; then
  ADDR_NAME="${VM_NAME}-ip"
  if ! gcloud compute addresses describe "${ADDR_NAME}" --region="${REGION}" --project="${GCP_PROJECT_ID}" >/dev/null 2>&1; then
    gcloud compute addresses create "${ADDR_NAME}" --region="${REGION}" --project="${GCP_PROJECT_ID}"
  fi
  STATIC_IP="$(gcloud compute addresses describe "${ADDR_NAME}" --region="${REGION}" --project="${GCP_PROJECT_ID}" --format='get(address)')"
  echo "Static IP:   ${STATIC_IP}"
fi

if gcloud compute instances describe "${VM_NAME}" --zone="${ZONE}" --project="${GCP_PROJECT_ID}" >/dev/null 2>&1; then
  echo "VM ${VM_NAME} already exists in ${ZONE}. Updating SSH key metadata…"
  gcloud compute instances add-metadata "${VM_NAME}" \
    --zone="${ZONE}" \
    --project="${GCP_PROJECT_ID}" \
    --metadata=ssh-keys="${SSH_KEY_LINE}"
  echo "To re-run startup packages, SSH in and run: sudo bash ${STARTUP_SCRIPT}"
else
  CREATE_ARGS=(
    "${VM_NAME}"
    --project="${GCP_PROJECT_ID}"
    --zone="${ZONE}"
    --machine-type="${MACHINE_TYPE}"
    --image-family="${IMAGE_FAMILY}"
    --image-project="${IMAGE_PROJECT}"
    --boot-disk-size="${DISK_SIZE_GB}GB"
    --boot-disk-type="${DISK_TYPE}"
    --tags=theexonet-web,http-server,https-server
    --metadata="ssh-keys=${SSH_KEY_LINE}"
    --metadata-from-file="startup-script=${STARTUP_SCRIPT}"
    --scopes=default,https://www.googleapis.com/auth/cloud-platform
  )
  if [ -n "${STATIC_IP}" ]; then
    CREATE_ARGS+=(--address="${STATIC_IP}")
  fi
  gcloud compute instances create "${CREATE_ARGS[@]}"
fi

EXTERNAL_IP="$(gcloud compute instances describe "${VM_NAME}" \
  --zone="${ZONE}" \
  --project="${GCP_PROJECT_ID}" \
  --format='get(networkInterfaces[0].accessConfigs[0].natIP)')"

if [ -n "${THEEXONET_SSH_PUBLIC_KEY:-}" ]; then
  PRIVATE_KEY_HINT="~/.ssh/id_ed25519  # your Windows private key"
else
  PRIVATE_KEY_HINT="${SSH_PUBKEY_FILE%.pub}"
fi

cat <<EOF

=== theexonet VM ready ===

External IP:  ${EXTERNAL_IP}
Zone:         ${ZONE}
SSH (root):     ssh -i ${PRIVATE_KEY_HINT} root@${EXTERNAL_IP}

DNS (theexonet.com):
  A record @    -> ${EXTERNAL_IP}
  A record www  -> ${EXTERNAL_IP}  (optional)

After DNS propagates, HTTPS:
  sudo apt install -y certbot python3-certbot-apache
  sudo certbot --apache -d theexonet.com -d www.theexonet.com

Verify stack:
  systemctl status apache2 docker
  docker compose version
  curl -sI http://127.0.0.1/

Free tier checklist:
  - Region must stay us-central1 (Midwest)
  - Machine type e2-micro, disk pd-standard <= 30GB
  - Set a \$1 billing budget alert in GCP Console

EOF
