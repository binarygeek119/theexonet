#!/bin/bash
# Restart RAVA API (5000), status (6000), admin portal (7000), and moderator portal (7050) on production.
# Run as root: sudo bash /path/to/restart-rava.sh

set -euo pipefail

API_SERVICE="${RAVA_API_SERVICE:-rava-api}"
STATUS_SERVICE="${RAVA_STATUS_SERVICE:-rava-status}"
ADMIN_SERVICE="${RAVA_ADMIN_SERVICE:-rava-admin}"
MODERATOR_SERVICE="${RAVA_MODERATOR_SERVICE:-rava-moderator}"

free_port() {
  local port="$1"
  if command -v fuser >/dev/null 2>&1; then
    fuser -k "${port}/tcp" 2>/dev/null || true
  elif command -v lsof >/dev/null 2>&1; then
    lsof -ti:"${port}" | xargs -r kill -9 2>/dev/null || true
  fi
}

echo "Stopping ${API_SERVICE}, ${STATUS_SERVICE}, ${ADMIN_SERVICE}, and ${MODERATOR_SERVICE}..."
systemctl stop "${MODERATOR_SERVICE}" 2>/dev/null || true
systemctl stop "${ADMIN_SERVICE}" 2>/dev/null || true
systemctl stop "${STATUS_SERVICE}" 2>/dev/null || true
systemctl stop "${API_SERVICE}" 2>/dev/null || true

echo "Clearing orphan listeners on ports 5000, 6000, 7000, and 7050..."
free_port 5000
free_port 6000
free_port 7000
free_port 7050

systemctl reset-failed "${API_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${STATUS_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${ADMIN_SERVICE}" 2>/dev/null || true
systemctl reset-failed "${MODERATOR_SERVICE}" 2>/dev/null || true

sleep 2

echo "Starting ${API_SERVICE}..."
systemctl start "${API_SERVICE}"

echo "Starting ${STATUS_SERVICE}..."
systemctl start "${STATUS_SERVICE}"

echo "Starting ${ADMIN_SERVICE}..."
systemctl start "${ADMIN_SERVICE}"

echo "Starting ${MODERATOR_SERVICE}..."
systemctl start "${MODERATOR_SERVICE}"

sleep 2

systemctl is-active --quiet "${API_SERVICE}" && echo "${API_SERVICE}: running" || echo "${API_SERVICE}: FAILED"
systemctl is-active --quiet "${STATUS_SERVICE}" && echo "${STATUS_SERVICE}: running" || echo "${STATUS_SERVICE}: FAILED"
systemctl is-active --quiet "${ADMIN_SERVICE}" && echo "${ADMIN_SERVICE}: running" || echo "${ADMIN_SERVICE}: FAILED"
systemctl is-active --quiet "${MODERATOR_SERVICE}" && echo "${MODERATOR_SERVICE}: running" || echo "${MODERATOR_SERVICE}: FAILED"

if command -v curl >/dev/null 2>&1; then
  curl -sf http://127.0.0.1:5000/api/status >/dev/null && echo "API health: OK" || echo "API health: unreachable"
  curl -sf http://127.0.0.1:7000/admin.html >/dev/null && echo "Admin portal: OK" || echo "Admin portal: unreachable"
  curl -sf http://127.0.0.1:7050/moderator.html >/dev/null && echo "Moderator portal: OK" || echo "Moderator portal: unreachable"
fi

echo "Done."
