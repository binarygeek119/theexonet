#!/bin/bash
# Sync /var/www/data/appsettings.json DB password from /etc/theexonet/postgres.env.
#   sudo bash scripts/theexonet/sync-postgres-password.sh
set -euo pipefail

POSTGRES_ENV="${THEEXONET_POSTGRES_ENV:-/etc/theexonet/postgres.env}"
APPS_SETTINGS="${THEEXONET_DATA_DIR:-/var/www/data}/appsettings.json"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if [ ! -f "${POSTGRES_ENV}" ]; then
  echo "ERROR: missing ${POSTGRES_ENV} (run install-host-server.sh first)." >&2
  exit 1
fi
if [ ! -f "${APPS_SETTINGS}" ]; then
  echo "ERROR: missing ${APPS_SETTINGS}" >&2
  exit 1
fi

# shellcheck disable=SC1090
source "${POSTGRES_ENV}"
POSTGRES_USER="${POSTGRES_USER:-theexonet}"
POSTGRES_DB="${POSTGRES_DB:-theexonet}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-}"

if [ -z "${POSTGRES_PASSWORD}" ]; then
  echo "ERROR: POSTGRES_PASSWORD empty in ${POSTGRES_ENV}" >&2
  exit 1
fi

escaped_pass="$(printf '%s' "${POSTGRES_PASSWORD}" | sed 's/[&/\]/\\&/g')"
sed -i \
  -e "s|\"DefaultConnection\": \"Host=[^\"]*\"|\"DefaultConnection\": \"Host=127.0.0.1;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${escaped_pass}\"|g" \
  -e 's|Database=rava|Database=theexonet|g' \
  -e 's|Username=postgres|Username=theexonet|g' \
  "${APPS_SETTINGS}"

chown "${SERVICE_USER}:${SERVICE_USER}" "${APPS_SETTINGS}"
chmod 640 "${APPS_SETTINGS}"

echo "Updated DefaultConnection in ${APPS_SETTINGS} (user=${POSTGRES_USER}, db=${POSTGRES_DB})."

if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' | grep -qx 'theexonet-postgres'; then
  if docker exec theexonet-postgres psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -c 'SELECT 1' >/dev/null 2>&1; then
    echo "Postgres login test: OK"
  else
    echo "WARN: Postgres login test failed — password in postgres.env may not match the running container." >&2
    echo "If the container was created with a different password, reset it or recreate the volume." >&2
  fi
fi
