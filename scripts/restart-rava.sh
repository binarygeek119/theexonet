#!/bin/bash
# Restart RAVA API (5000), status (6000), admin portal (7000), moderator portal (7050), and docs (9000) on production.
# Run as root: sudo restart-rava

set -euo pipefail

API_SERVICE="${RAVA_API_SERVICE:-rava-api}"
STATUS_SERVICE="${RAVA_STATUS_SERVICE:-rava-status}"
ADMIN_SERVICE="${RAVA_ADMIN_SERVICE:-rava-admin}"
MODERATOR_SERVICE="${RAVA_MODERATOR_SERVICE:-rava-moderator}"
DOCS_SERVICE="${RAVA_DOCS_SERVICE:-rava-docs}"
PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

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

  if [ ! -f "${DATA_DIR}/credits.csv" ]; then
    echo "Missing data CSV spreadsheets — syncing to ${DATA_DIR}..."
    if [ -x /usr/local/bin/sync-rava-data ]; then
      sync-rava-data || {
        echo "WARN: sync-rava-data failed. Run: sudo install-rava-scripts (from git checkout)" >&2
      }
    elif [ -f /usr/local/lib/rava/scripts/sync-publish-data.sh ]; then
      bash /usr/local/lib/rava/scripts/sync-publish-data.sh || true
    else
      echo "WARN: credits.csv missing and sync-rava-data not installed." >&2
    fi
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
    "${DATA_DIR}/exonet/offworld-news/editions" \
    "${DATA_DIR}/exonet/offworld-news/images"

  for subdir in profile profile-backgrounds; do
    src="${PUBLISH_DIR}/html/images/${subdir}"
    dest="${DATA_DIR}/images/${subdir}"
    if [ -d "$src" ] && [ -z "$(ls -A "$dest" 2>/dev/null || true)" ] && [ -n "$(ls -A "$src" 2>/dev/null || true)" ]; then
      echo "Migrating player images ${src} -> ${dest}..."
      rsync -a "${src}/" "${dest}/"
    fi
  done

  for subdir in editions images; do
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

sleep 2

systemctl is-active --quiet "${API_SERVICE}" && echo "${API_SERVICE}: running" || { echo "${API_SERVICE}: FAILED"; show_service_failure "${API_SERVICE}"; }
systemctl is-active --quiet "${STATUS_SERVICE}" && echo "${STATUS_SERVICE}: running" || { echo "${STATUS_SERVICE}: FAILED"; show_service_failure "${STATUS_SERVICE}"; }
systemctl is-active --quiet "${ADMIN_SERVICE}" && echo "${ADMIN_SERVICE}: running" || { echo "${ADMIN_SERVICE}: FAILED"; show_service_failure "${ADMIN_SERVICE}"; }
systemctl is-active --quiet "${MODERATOR_SERVICE}" && echo "${MODERATOR_SERVICE}: running" || { echo "${MODERATOR_SERVICE}: FAILED"; show_service_failure "${MODERATOR_SERVICE}"; }
systemctl is-active --quiet "${DOCS_SERVICE}" && echo "${DOCS_SERVICE}: running" || { echo "${DOCS_SERVICE}: FAILED"; show_service_failure "${DOCS_SERVICE}"; }

if command -v curl >/dev/null 2>&1; then
  curl -sf http://127.0.0.1:5000/api/status >/dev/null && echo "API health: OK" || echo "API health: unreachable"
  curl -sf --max-time 15 http://127.0.0.1:5000/api/public/offworld-news >/dev/null \
    && echo "Offworld News API: OK" \
    || echo "Offworld News API: unreachable (deploy latest Rava.Api.dll and restart)"
  curl -sf http://127.0.0.1:7000/admin.html >/dev/null && echo "Admin portal: OK" || echo "Admin portal: unreachable"
  curl -sf http://127.0.0.1:7050/moderator.html >/dev/null && echo "Moderator portal: OK" || echo "Moderator portal: unreachable"
  curl -sf http://127.0.0.1:9000/ >/dev/null && echo "Docs portal: OK" || echo "Docs portal: unreachable"
fi

echo "Done."
