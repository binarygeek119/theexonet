#!/bin/bash
# Promote FTP/GitHub staging uploads into the live publish folder and restart services.
# Run as root after uploading a publish bundle to /var/www/staging:
#   sudo bash scripts/theexonet/promote-staging.sh
#   sudo bash scripts/theexonet/promote-staging.sh theexonet-website-deploy-<sha>.tar.gz
set -euo pipefail

REQUESTED_ARCHIVE="${1:-}"

STAGING_DIR="${THEEXONET_STAGING_DIR:-/var/www/staging}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

for cmd in unzip rsync tar; do
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    echo "ERROR: ${cmd} is required to promote deploy archives. Run: sudo apt-get install -y unzip rsync tar" >&2
    exit 1
  fi
done

if [ ! -d "${STAGING_DIR}" ] || [ -z "$(ls -A "${STAGING_DIR}" 2>/dev/null)" ]; then
  echo "Nothing to promote in ${STAGING_DIR}" >&2
  exit 1
fi

force_remove() {
  local target="$1"
  [ -e "${target}" ] || return 0
  chmod -R u+w "${target}" 2>/dev/null || true
  if command -v chattr >/dev/null 2>&1; then
    chattr -R -i "${target}" 2>/dev/null || true
  fi
  rm -rf "${target}" 2>/dev/null || true
  if [ -e "${target}" ]; then
    find "${target}" -depth -exec chmod u+w {} + 2>/dev/null || true
    find "${target}" -depth -delete 2>/dev/null || true
    rm -rf "${target}"
  fi
}

extract_archive() {
  local archive="$1"
  local dest="$2"
  case "${archive}" in
    *.tar.gz|*.tgz)
      tar -xzf "${archive}" -C "${dest}"
      ;;
    *.zip)
      unzip -o -q "${archive}" -d "${dest}"
      ;;
    *)
      echo "ERROR: unsupported archive type: ${archive}" >&2
      return 1
      ;;
  esac
}

# Legacy failed promotes left a sticky staging/unpack tree (generated reporter assets).
force_remove "${STAGING_DIR}/unpack"
find "${STAGING_DIR}" -maxdepth 1 -type d -name '.unpack.*' -exec rm -rf {} + 2>/dev/null || true

discover_archives() {
  shopt -s nullglob
  declare -A seen=()
  local -a found=()
  local pattern file canon

  for pattern in \
    "${STAGING_DIR}"/theexonet-website-deploy-*.tar.gz \
    "${STAGING_DIR}"/theexonet-website-deploy-*.zip \
    "${STAGING_DIR}"/theexonet-website-deploy-*.tgz \
    "${STAGING_DIR}"/theexonet-website-*.tar.gz \
    "${STAGING_DIR}"/theexonet-website-*.zip \
    "${STAGING_DIR}"/*.tar.gz \
    "${STAGING_DIR}"/*.zip \
    "${STAGING_DIR}"/*.tgz; do
    for file in ${pattern}; do
      [ -f "${file}" ] || continue
      canon="$(readlink -f "${file}")"
      if [ -n "${seen[${canon}]+x}" ]; then
        continue
      fi
      seen["${canon}"]=1
      found+=("${file}")
    done
  done

  printf '%s\0' "${found[@]}"
}

newest_archive() {
  local -a files=("$@")
  local newest="" file mtime newest_mtime=0
  for file in "${files[@]}"; do
    mtime="$(stat -c %Y "${file}" 2>/dev/null || echo 0)"
    if [ "${mtime}" -gt "${newest_mtime}" ]; then
      newest_mtime="${mtime}"
      newest="${file}"
    fi
  done
  printf '%s' "${newest}"
}

promote_archive() {
  local archive="$1"
  echo "Unpacking ${archive}…"
  unpack_dir="$(mktemp -d "${STAGING_DIR}/.unpack.XXXXXX")"
  if ! extract_archive "${archive}" "${unpack_dir}"; then
    force_remove "${unpack_dir}"
    rm -f "${archive}"
    exit 1
  fi
  if [ -d "${unpack_dir}/publish" ]; then
    if [ ! -f "${unpack_dir}/publish/Theexonet.Api.dll" ]; then
      echo "ERROR: ${archive} is missing publish/Theexonet.Api.dll — refusing to rsync --delete over live publish." >&2
      force_remove "${unpack_dir}"
      rm -f "${archive}"
      exit 1
    fi
    rsync -a --delete "${unpack_dir}/publish/" "${PUBLISH_DIR}/"
  else
    echo "ERROR: ${archive} has no publish/ folder." >&2
    force_remove "${unpack_dir}"
    rm -f "${archive}"
    exit 1
  fi
  if [ -d "${unpack_dir}/data" ]; then
    rsync -a "${unpack_dir}/data/" "${DATA_DIR}/"
  fi
  force_remove "${unpack_dir}"
  rm -f "${archive}"
}

mapfile -d '' -t STAGING_ARCHIVES < <(discover_archives)

if [ -n "${REQUESTED_ARCHIVE}" ]; then
  REQUESTED_ARCHIVE="$(basename "${REQUESTED_ARCHIVE}")"
  TARGET_ARCHIVE="${STAGING_DIR}/${REQUESTED_ARCHIVE}"
  if [ ! -f "${TARGET_ARCHIVE}" ]; then
    echo "ERROR: requested archive not found: ${TARGET_ARCHIVE}" >&2
    exit 1
  fi
  # Drop stale CI/FTP deploy bundles so overlapping globs cannot re-process them.
  shopt -s nullglob
  for stale in \
    "${STAGING_DIR}"/theexonet-website-deploy-*.tar.gz \
    "${STAGING_DIR}"/theexonet-website-deploy-*.zip \
    "${STAGING_DIR}"/theexonet-website-deploy-*.tgz; do
    if [ "${stale}" != "${TARGET_ARCHIVE}" ]; then
      rm -f "${stale}"
    fi
  done
  promote_archive "${TARGET_ARCHIVE}"
elif [ "${#STAGING_ARCHIVES[@]}" -eq 0 ]; then
  echo "Nothing to promote in ${STAGING_DIR}" >&2
  exit 1
else
  TARGET_ARCHIVE="$(newest_archive "${STAGING_ARCHIVES[@]}")"
  if [ -z "${TARGET_ARCHIVE}" ]; then
    echo "Nothing to promote in ${STAGING_DIR}" >&2
    exit 1
  fi
  promote_archive "${TARGET_ARCHIVE}"
fi

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
