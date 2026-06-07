#!/bin/bash
# Issue Let's Encrypt certificates for theexonet.com and all subdomains (Apache + certbot).
#
# Prerequisites:
#   - Apache theexonet vhost installed (install-host-server.sh)
#   - DNS A records for apex + www + api + status + admin + moderator + docs → this server
#   - TCP 80 open to the internet (HTTP-01 challenge)
#
# Usage:
#   sudo CERTBOT_EMAIL=you@example.com bash scripts/theexonet/install-ssl-certs.sh
#   sudo bash scripts/theexonet/install-ssl-certs.sh          # prompts for email
#   sudo CERTBOT_DRY_RUN=1 bash scripts/theexonet/install-ssl-certs.sh
#
# Env:
#   THEEXONET_DOMAIN     — default theexonet.com
#   CERTBOT_EMAIL        — Let's Encrypt account email (prompted if unset)
#   CERTBOT_DRY_RUN=1    — certbot --dry-run only
#   SKIP_DNS_CHECK=1     — skip DNS preflight
#   THEEXONET_PUBLIC_IP  — override auto-detected server IP for DNS checks
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"

# shellcheck source=wait-for-apt-lock.sh
source "${SCRIPT_DIR}/wait-for-apt-lock.sh"

DOMAIN="${THEEXONET_DOMAIN:-theexonet.com}"
CERTBOT_DRY_RUN="${CERTBOT_DRY_RUN:-0}"
SKIP_DNS_CHECK="${SKIP_DNS_CHECK:-0}"

log() { echo "[install-ssl-certs] $*"; }

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -f /etc/apache2/sites-available/theexonet.conf ] && [ ! -f /etc/apache2/sites-enabled/theexonet.conf ]; then
  echo "ERROR: Apache theexonet site not found. Run install-host-server.sh first." >&2
  exit 1
fi

hosts=(
  "${DOMAIN}"
  "www.${DOMAIN}"
  "api.${DOMAIN}"
  "status.${DOMAIN}"
  "admin.${DOMAIN}"
  "moderator.${DOMAIN}"
  "docs.${DOMAIN}"
)

detect_public_ip() {
  if [ -n "${THEEXONET_PUBLIC_IP:-}" ]; then
    printf '%s' "${THEEXONET_PUBLIC_IP}"
    return
  fi
  curl -fsS --max-time 8 https://ifconfig.me/ip 2>/dev/null \
    || curl -fsS --max-time 8 https://api.ipify.org 2>/dev/null \
    || true
}

check_dns() {
  local server_ip
  server_ip="$(detect_public_ip)"
  local failed=0

  log "Checking DNS A records…"
  for host in "${hosts[@]}"; do
    local resolved
    resolved="$(getent ahostsv4 "${host}" 2>/dev/null | awk 'NR==1 {print $1}' || true)"
    if [ -z "${resolved}" ]; then
      echo "ERROR: no A record for ${host} (NXDOMAIN or not propagated yet)." >&2
      failed=1
      continue
    fi
    echo "  ${host} → ${resolved}"
    if [ -n "${server_ip}" ] && [ "${resolved}" != "${server_ip}" ]; then
      echo "  WARN: ${host} does not point at this server (${server_ip}). Certbot may fail." >&2
    fi
  done

  if [ "${failed}" -ne 0 ]; then
    echo "Fix DNS, wait for propagation, then re-run. Or SKIP_DNS_CHECK=1 to force." >&2
    exit 1
  fi
}

if [ "${SKIP_DNS_CHECK}" != "1" ]; then
  check_dns
else
  log "Skipping DNS preflight (SKIP_DNS_CHECK=1)."
fi

if [ -z "${CERTBOT_EMAIL:-}" ]; then
  read -r -p "Let's Encrypt account email: " CERTBOT_EMAIL
fi
if [ -z "${CERTBOT_EMAIL}" ]; then
  echo "ERROR: set CERTBOT_EMAIL=you@example.com" >&2
  exit 1
fi

wait_for_apt_lock
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y certbot python3-certbot-apache

a2enmod ssl headers 2>/dev/null || true
if ! a2query -s theexonet.conf >/dev/null 2>&1; then
  a2ensite theexonet.conf
fi
systemctl reload apache2

certbot_args=(
  --apache
  --agree-tos
  --no-eff-email
  -m "${CERTBOT_EMAIL}"
  --redirect
  --expand
)
for host in "${hosts[@]}"; do
  certbot_args+=(-d "${host}")
done

if [ "${CERTBOT_DRY_RUN}" = "1" ]; then
  log "Dry run (no certificates issued)…"
  certbot "${certbot_args[@]}" --dry-run
  echo "Dry run OK. Re-run without CERTBOT_DRY_RUN=1 to issue certificates."
  exit 0
fi

log "Requesting certificates for: ${hosts[*]}"
if certbot "${certbot_args[@]}" --non-interactive --keep-until-expiring; then
  log "Certificates issued/updated."
else
  echo "ERROR: certbot failed. Common causes: DNS not pointing here, port 80 blocked, Apache misconfigured." >&2
  echo "Try: sudo CERTBOT_DRY_RUN=1 bash $0" >&2
  exit 1
fi

log "Installing Apache HTTPS vhosts (per-subdomain proxy)…"
bash "${SCRIPT_DIR}/install-apache-ssl-vhosts.sh"

log "Testing renewal…"
certbot renew --dry-run

cat <<EOF

=== HTTPS ready ===

  https://${DOMAIN}/
  https://api.${DOMAIN}/
  https://status.${DOMAIN}/
  https://admin.${DOMAIN}/
  https://moderator.${DOMAIN}/
  https://docs.${DOMAIN}/

Renewal: certbot timer (systemctl status certbot.timer)
Re-run:  sudo install-theexonet-ssl

EOF
