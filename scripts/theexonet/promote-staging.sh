#!/bin/bash
# Promote FTP/GitHub staging uploads into the live publish folder and restart services.
# Run as root after uploading a publish bundle to /var/www/staging:
#   sudo bash scripts/theexonet/promote-staging.sh
set -euo pipefail

STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

for cmd in unzip rsync; do
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    echo "ERROR: ${cmd} is required to promote deploy zips. Run: sudo apt-get install -y unzip rsync" >&2
    exit 1
  fi
done

if [ ! -d "${STAGING_DIR}" ] || [ -z "$(ls -A "${STAGING_DIR}" 2>/dev/null)" ]; then
  echo "Nothing to promote in ${STAGING_DIR}" >&2
  exit 1
fi

clean_unpack_dir() {
  local unpack="${STAGING_DIR}/unpack"
  if [ -d "${unpack}" ]; then
    chmod -R u+w "${unpack}" 2>/dev/null || true
    rm -rf "${unpack}"
  fi
}

# Unpack zip if present
shopt -s nullglob
for zip in "${STAGING_DIR}"/*.zip "${STAGING_DIR}"/theexonet-website-*.zip "${STAGING_DIR}"/theexonet-website-deploy-*.zip; do
  echo "Unpacking ${zip}…"
  clean_unpack_dir
  mkdir -p "${STAGING_DIR}/unpack"
  unzip -o -q "${zip}" -d "${STAGING_DIR}/unpack"
  if [ -d "${STAGING_DIR}/unpack/publish" ]; then
    if [ ! -f "${STAGING_DIR}/unpack/publish/Theexonet.Api.dll" ]; then
      echo "ERROR: ${zip} is missing publish/Theexonet.Api.dll — refusing to rsync --delete over live publish." >&2
      clean_unpack_dir
      rm -f "${zip}"
      exit 1
    fi
    rsync -a --delete "${STAGING_DIR}/unpack/publish/" "${PUBLISH_DIR}/"
  else
    echo "ERROR: ${zip} has no publish/ folder." >&2
    clean_unpack_dir
    rm -f "${zip}"
    exit 1
  fi
  if [ -d "${STAGING_DIR}/unpack/data" ]; then
    rsync -a "${STAGING_DIR}/unpack/data/" "${DATA_DIR}/"
  fi
  clean_unpack_dir
  rm -f "${zip}"
done

if [ ! -f "${PUBLISH_DIR}/Theexonet.Api.dll" ]; then
  echo "ERROR: ${PUBLISH_DIR}/Theexonet.Api.dll is missing after promote." >&2
  exit 1
fi

# Direct rsync of publish/ or flat DLL layout
if [ -d "${STAGING_DIR}/publish" ]; then
  rsync -a --delete \
    --exclude 'appsettings.json' \
    "${STAGING_DIR}/publish/" "${PUBLISH_DIR}/"
fi

if [ -f "${STAGING_DIR}/Theexonet.Api.dll" ]; then
  rsync -a \
    --exclude 'appsettings.json' \
    "${STAGING_DIR}/" "${PUBLISH_DIR}/"
fi

chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${PUBLISH_DIR}" "${DATA_DIR}" 2>/dev/null || true

if command -v fix-theexonet-permissions >/dev/null 2>&1; then
  fix-theexonet-permissions -q
elif [ -f /usr/local/lib/theexonet/scripts/fix-hosting-permissions.sh ]; then
  bash /usr/local/lib/theexonet/scripts/fix-hosting-permissions.sh -q
fi

if command -v restart-theexonet >/dev/null 2>&1; then
  restart-theexonet
else
  systemctl restart theexonet-api theexonet-status theexonet-admin theexonet-moderator theexonet-docs || true
fi

echo "Promoted staging → ${PUBLISH_DIR} and restarted services."
