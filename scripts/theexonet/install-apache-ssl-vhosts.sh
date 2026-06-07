#!/bin/bash
# Install Apache :443 vhosts so each subdomain proxies to the correct backend.
# Fixes api.theexonet.com serving the game UI over HTTPS when certbot only configured apex SSL.
#
#   sudo bash scripts/theexonet/install-apache-ssl-vhosts.sh
#   sudo install-theexonet-apache-ssl
#
# Env: THEEXONET_DOMAIN (default theexonet.com)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
DOMAIN="${THEEXONET_DOMAIN:-theexonet.com}"
SSL_TEMPLATE="${SCRIPT_DIR}/apache-theexonet-ssl.conf"
SSL_SITE="/etc/apache2/sites-available/theexonet-le-ssl.conf"
CERT_DIR="/etc/letsencrypt/live/${DOMAIN}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ ! -f "${SSL_TEMPLATE}" ]; then
  echo "ERROR: missing ${SSL_TEMPLATE}" >&2
  exit 1
fi

if [ ! -f "${CERT_DIR}/fullchain.pem" ] || [ ! -f "${CERT_DIR}/privkey.pem" ]; then
  echo "ERROR: Let's Encrypt cert not found at ${CERT_DIR}. Run install-ssl-certs.sh first." >&2
  exit 1
fi

a2enmod ssl proxy proxy_http headers rewrite 2>/dev/null || true
a2enmod headers 2>/dev/null || true

sed "s/@@DOMAIN@@/${DOMAIN}/g" "${SSL_TEMPLATE}" >"${SSL_SITE}"
a2ensite theexonet-le-ssl.conf

if ! apache2ctl configtest; then
  echo "ERROR: Apache config test failed." >&2
  exit 1
fi

systemctl reload apache2
echo "Installed ${SSL_SITE} — api.${DOMAIN} proxies to :5000 over HTTPS."
