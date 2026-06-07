#!/bin/bash
# Restart theexonet API (5000), status (6000), admin portal (7000), moderator portal (7050), and docs (9000) on production.
# Run as root: sudo restart-theexonet

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
API_SERVICE="${THEEXONET_API_SERVICE:-theexonet-api}"
STATUS_SERVICE="${THEEXONET_STATUS_SERVICE:-theexonet-status}"
ADMIN_SERVICE="${THEEXONET_ADMIN_SERVICE:-theexonet-admin}"
MODERATOR_SERVICE="${THEEXONET_MODERATOR_SERVICE:-theexonet-moderator}"
DOCS_SERVICE="${THEEXONET_DOCS_SERVICE:-theexonet-docs}"
PERMISSIONS_SERVICE="${THEEXONET_PERMISSIONS_SERVICE:-theexonet-permissions}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-www-data}"

free_port() {
  local port="$1"
  if command -v fuser >/dev/null 2>&1; then
    fuser -k "${port}/tcp" 2>/dev/null || true
  elif command -v lsof >/dev/null 2>&1; then
    lsof -ti:"${port}" | xargs -r kill -9 2>/dev/null || true
  fi
}

show_service_failure() {
  local service="$1"
  echo
  echo "=== ${service} failed — last 40 log lines ==="
  journalctl -u "${service}" -n 40 --no-pager || true
  echo
}

prepare_publish_dir() {
  if [ ! -d "${PUBLISH_DIR}" ]; then
    echo "ERROR: publish dir missing: ${PUBLISH_DIR}"
    exit 1
  fi

  mkdir -p "${DATA_DIR}"
  if [ "$(id -u)" -eq 0 ]; then
    chown "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}" 2>/dev/null || true
  fi

  echo "Syncing data CSV spreadsheets to ${DATA_DIR}..."
  if [ -x /usr/local/bin/sync-theexonet-data ]; then
    sync-theexonet-data "${PUBLISH_DIR}" || {
      echo "WARN: sync-theexonet-data failed. Run: sudo install-theexonet-scripts (from git checkout)" >&2
    }
  elif [ -f /usr/local/lib/theexonet/scripts/sync-publish-data.sh ]; then
    bash /usr/local/lib/theexonet/scripts/sync-publish-data.sh "${PUBLISH_DIR}" || true
  elif [ -f "${SCRIPT_DIR}/sync-publish-data.sh" ]; then
    bash "${SCRIPT_DIR}/sync-publish-data.sh" "${PUBLISH_DIR}" || true
  elif [ ! -f "${DATA_DIR}/credits.csv" ]; then
    echo "WARN: credits.csv missing and sync-theexonet-data not installed." >&2
  fi

  if [ ! -f "${DATA_DIR}/appsettings.json" ] && [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
    echo "Migrating appsettings.json from publish to ${DATA_DIR}..."
    cp -f "${PUBLISH_DIR}/appsettings.json" "${DATA_DIR}/appsettings.json"
    if [ "$(id -u)" -eq 0 ]; then
      chown "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/appsettings.json" 2>/dev/null || true
    fi
  fi

  mkdir -p \
    "${DATA_DIR}/images/profile" \
    "${DATA_DIR}/images/profile-backgrounds" \
    "${DATA_DIR}/images/company-logos" \
    "${DATA_DIR}/exonet/offworld-news/editions" \
    "${DATA_DIR}/exonet/offworld-news/images" \
    "${DATA_DIR}/exonet/offworld-news/reporters"

  if [ "$(id -u)" -eq 0 ] && command -v fix-theexonet-permissions >/dev/null 2>&1; then
    fix-theexonet-permissions -q || true
  fi

  for subdir in profile profile-backgrounds; do
    src="${PUBLISH_DIR}/html/images/${subdir}"
    dest="${DATA_DIR}/images/${subdir}"
    if [ -d "$src" ] && [ -z "$(ls -A "$dest" 2>/dev/null || true)" ] && [ -n "$(ls -A "$src" 2>/dev/null || true)" ]; then
      echo "Migrating player images ${src} -> ${dest}..."
      rsync -a "${src}/" "${dest}/"
    fi
  done

  for subdir in editions images reporters; do
    src="${PUBLISH_DIR}/html/exonet/offworld-news/${subdir}"
    dest="${DATA_DIR}/exonet/offworld-news/${subdir}"
    if [ -d "$src" ] && [ -z "$(ls -A "$dest" 2>/dev/null || true)" ] && [ -n "$(ls -A "$src" 2>/dev/null || true)" ]; then
      echo "Migrating Offworld News ${subdir} ${src} -> ${dest}..."
      rsync -a "${src}/" "${dest}/"
    fi
  done

  if [ "$(id -u)" -eq 0 ]; then
    chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/images" 2>/dev/null || true
    chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/exonet" 2>/dev/null || true
  fi

  ensure_shared_wwwroot
}

ensure_shared_wwwroot() {
  local wwwroot="${PUBLISH_DIR}/wwwroot"
  local status_wwwroot="${PUBLISH_DIR}/status-wwwroot"
  local server_dir=""
  local html_source=""

  mkdir -p "${wwwroot}" "${status_wwwroot}"

  if [ -f "${SCRIPT_DIR}/../server/Theexonet.Api/html/admin.html" ]; then
    server_dir="$(cd "${SCRIPT_DIR}/../server" && pwd)"
    html_source="${server_dir}/Theexonet.Api/html"
  elif [ -f "${SCRIPT_DIR}/resolve-server-dir.sh" ]; then
    if server_dir="$(bash "${SCRIPT_DIR}/resolve-server-dir.sh" 2>/dev/null)"; then
      html_source="${server_dir}/Theexonet.Api/html"
    fi
  elif [ -f "/usr/local/lib/theexonet/html/admin.html" ]; then
    html_source="/usr/local/lib/theexonet/html"
  elif [ -f "${PUBLISH_DIR}/html/admin.html" ]; then
    html_source="${PUBLISH_DIR}/html"
  fi

  if [ -n "$server_dir" ] && [ -f "${SCRIPT_DIR}/sync-publish-wwwroot.sh" ]; then
    if [ ! -f "${wwwroot}/index.html" ] \
      || [ ! -f "${wwwroot}/js/status.js" ] \
      || [ ! -f "${status_wwwroot}/index.html" ] \
      || [ ! -f "${wwwroot}/admin.html" ] \
      || [ ! -f "${wwwroot}/moderator.html" ] \
      || [ ! -f "${wwwroot}/js/currency.js" ]; then
      echo "Syncing missing status + portal wwwroot files from ${server_dir}..."
      bash "${SCRIPT_DIR}/sync-publish-wwwroot.sh" "${server_dir}" "${wwwroot}"
    fi
  elif [ -n "${html_source}" ] && [ -f "${SCRIPT_DIR}/sync-portal-wwwroot.sh" ]; then
    if [ ! -f "${wwwroot}/admin.html" ] || [ ! -f "${wwwroot}/moderator.html" ] || [ ! -f "${wwwroot}/js/currency.js" ]; then
      echo "Syncing missing portal wwwroot files from ${html_source}..."
      bash "${SCRIPT_DIR}/sync-portal-wwwroot.sh" "${html_source}" "${wwwroot}"
    fi
    if [ -n "$server_dir" ] && [ -f "${SCRIPT_DIR}/sync-status-wwwroot.sh" ] \
      && { [ ! -f "${status_wwwroot}/index.html" ] || [ ! -f "${status_wwwroot}/js/status.js" ]; }; then
      echo "Syncing missing status wwwroot files from ${server_dir}..."
      bash "${SCRIPT_DIR}/sync-status-wwwroot.sh" "${server_dir}" "${status_wwwroot}"
      bash "${SCRIPT_DIR}/sync-status-wwwroot.sh" "${server_dir}" "${wwwroot}"
    fi
  elif [ ! -f "${wwwroot}/admin.html" ] || [ ! -f "${wwwroot}/moderator.html" ]; then
    echo "WARN: missing ${wwwroot}/admin.html or moderator.html — run: sudo deploy-theexonet-portals --static-only" >&2
  fi

  if [ ! -f "${status_wwwroot}/index.html" ] || [ ! -f "${status_wwwroot}/js/status.js" ]; then
    echo "WARN: missing status dashboard files — run: sudo deploy-theexonet-status --static-only" >&2
  fi

  if [ "$(id -u)" -eq 0 ]; then
    for dir in "${wwwroot}" "${status_wwwroot}"; do
      if [ ! -d "$dir" ]; then
        continue
      fi
      chown -R "${SERVICE_USER}:${SERVICE_USER}" "$dir" 2>/dev/null || true
      find "$dir" -type d -exec chmod 755 {} + 2>/dev/null || true
      find "$dir" -type f -exec chmod 644 {} + 2>/dev/null || true
    done
  fi
}

probe_http() {
  local label="$1"
  local url="$2"
  local attempt code

  for attempt in 1 2 3 4 5; do
    code="$(curl -s -o /dev/null -w '%{http_code}' --connect-timeout 2 --max-time 5 "${url}" 2>/dev/null || echo "000")"
    if [ "${code}" = "200" ]; then
      echo "${label}: OK"
      return 0
    fi
    sleep 1
  done

  echo "${label}: unreachable (HTTP ${code} from ${url})"
  return 1
}

show_portal_failure() {
  local service="$1"
  show_service_failure "${service}"
  if command -v ss >/dev/null 2>&1; then
    echo "Listening sockets on 7000/7050:"
    ss -tlnp | grep -E ':7000 |:7050 ' || echo "  (none)"
  fi
}

echo "Stopping ${API_SERVICE}, ${STATUS_SERVICE}, ${ADMIN_SERVICE}, ${MODERATOR_SERVICE}, and ${DOCS_SERVICE}..."
systemctl stop "${DOCS_SERVICE}" 2>/dev/null || true
systemctl stop "${MODERATOR_SERVICE}" 2>/dev/null || true
systemctl stop "${ADMIN_SERVICE}" 2>/dev/null || true
systemctl stop "${STATUS_SERVICE}" 2>/dev/null || true
systemctl stop "${API_SERVICE}" 2>/dev/null || true

echo "Clearing orphan listeners on ports 5000, 6000, 7000, 7050, and 9000..."
free_port 5000
free_port 6000
free_port 7000
free_port 7050
free_port 9000

systemctl reset-failed "${API_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${STATUS_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${ADMIN_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${MODERATOR_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${DOCS_SERVICE}" 2>/dev/null || true

sleep 2

prepare_publish_dir

echo "Starting ${API_SERVICE}..."
systemctl start "${API_SERVICE}"

echo "Starting ${STATUS_SERVICE}..."
systemctl start "${STATUS_SERVICE}"

echo "Starting ${ADMIN_SERVICE}..."
systemctl start "${ADMIN_SERVICE}"

echo "Starting ${MODERATOR_SERVICE}..."
systemctl start "${MODERATOR_SERVICE}"

echo "Starting ${DOCS_SERVICE}..."
systemctl start "${DOCS_SERVICE}"

if systemctl list-unit-files "${PERMISSIONS_SERVICE}.service" --no-legend 2>/dev/null | grep -q "${PERMISSIONS_SERVICE}"; then
  echo "Starting ${PERMISSIONS_SERVICE}..."
  systemctl start "${PERMISSIONS_SERVICE}" 2>/dev/null || true
fi

sleep 5

systemctl is-active --quiet "${API_SERVICE}" && echo "${API_SERVICE}: running" || { echo "${API_SERVICE}: FAILED"; show_service_failure "${API_SERVICE}"; }
systemctl is-active --quiet "${STATUS_SERVICE}" && echo "${STATUS_SERVICE}: running" || { echo "${STATUS_SERVICE}: FAILED"; show_service_failure "${STATUS_SERVICE}"; }
systemctl is-active --quiet "${ADMIN_SERVICE}" && echo "${ADMIN_SERVICE}: running" || { echo "${ADMIN_SERVICE}: FAILED"; show_service_failure "${ADMIN_SERVICE}"; }
systemctl is-active --quiet "${MODERATOR_SERVICE}" && echo "${MODERATOR_SERVICE}: running" || { echo "${MODERATOR_SERVICE}: FAILED"; show_service_failure "${MODERATOR_SERVICE}"; }
systemctl is-active --quiet "${DOCS_SERVICE}" && echo "${DOCS_SERVICE}: running" || { echo "${DOCS_SERVICE}: FAILED"; show_service_failure "${DOCS_SERVICE}"; }
if systemctl is-active --quiet "${PERMISSIONS_SERVICE}" 2>/dev/null; then
  echo "${PERMISSIONS_SERVICE}: running"
fi

admin_ok=1
moderator_ok=1
if command -v curl >/dev/null 2>&1; then
  curl -sf http://127.0.0.1:5000/api/status >/dev/null && echo "API health: OK" || echo "API health: unreachable"
  curl -sf --max-time 15 http://127.0.0.1:5000/api/public/offworld-news >/dev/null \
    && echo "Offworld News API: OK" \
    || echo "Offworld News API: unreachable (deploy latest Theexonet.Api.dll and restart)"
  admin_ok=0
  moderator_ok=0
  probe_http "Admin portal" "http://127.0.0.1:7000/admin.html" && admin_ok=1 || show_portal_failure "${ADMIN_SERVICE}"
  probe_http "Moderator portal" "http://127.0.0.1:7050/moderator.html" && moderator_ok=1 || show_portal_failure "${MODERATOR_SERVICE}"
  curl -sf http://127.0.0.1:9000/ >/dev/null && echo "Docs portal: OK" || echo "Docs portal: unreachable"
fi

if [ "$admin_ok" -eq 0 ] || [ "$moderator_ok" -eq 0 ]; then
  echo "ERROR: One or more portal HTTP checks failed. Run: sudo diagnose-theexonet-portals" >&2
  exit 1
fi

echo "Done."
