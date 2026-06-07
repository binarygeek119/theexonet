#!/bin/bash
# Diagnose theexonet-api startup failures on production.
# Run on the server: sudo diagnose-theexonet-api
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=theexonet-hosting-env.sh
[ -f "${SCRIPT_DIR}/theexonet-hosting-env.sh" ] && source "${SCRIPT_DIR}/theexonet-hosting-env.sh"

PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE="${THEEXONET_API_SERVICE:-theexonet-api}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-${SERVICE_USER}}"
API_DOMAIN="${THEEXONET_API_DOMAIN:-api.theexonet.com}"

echo "=== theexonet API diagnostics ==="
echo "Publish dir: ${PUBLISH_DIR}"
echo "Data dir:    ${DATA_DIR} (THEEXONET_DATA_DIR)"
echo

wait_for_http() {
  local label="$1"
  local url="$2"
  local attempts="${3:-18}"
  local delay="${4:-5}"
  local i=1
  while [ "$i" -le "$attempts" ]; do
    if curl -sf --max-time 10 "${url}" >/dev/null; then
      echo "OK  ${url}"
      return 0
    fi
    if command -v ss >/dev/null 2>&1 && ss -tln 2>/dev/null | grep -q ':5000'; then
      echo "WAIT  ${label} (${i}/${attempts}) — port 5000 is listening; endpoint not ready yet"
    else
      echo "WAIT  ${label} (${i}/${attempts}) — API still starting (port 5000 not listening)"
    fi
    sleep "${delay}"
    i=$((i + 1))
  done
  echo "FAILED  ${url} (after ${attempts} attempts, ~$((attempts * delay))s)"
  return 1
}

echo "--- .NET runtimes ---"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet not found in PATH."
else
  dotnet --list-runtimes | grep -E 'AspNetCore|NETCore' || true
  if ! dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10.'; then
    echo "ERROR: Microsoft.AspNetCore.App 10.x is not installed."
    echo "Install: sudo apt-get install -y aspnetcore-runtime-10.0"
  fi
fi
echo

echo "--- Required publish files ---"
publish_required=(
  "${PUBLISH_DIR}/Theexonet.Api.dll"
)
missing=0
for file in "${publish_required[@]}"; do
  if [ -f "$file" ]; then
    echo "OK  $file"
  else
    echo "MISSING  $file"
    missing=1
  fi
done
echo

echo "--- Required data files (${DATA_DIR}) ---"
data_required=(
  "${DATA_DIR}/appsettings.json"
  "${DATA_DIR}/credits.csv"
  "${DATA_DIR}/market-items.csv"
  "${DATA_DIR}/trade-items.csv"
  "${DATA_DIR}/hate-speech-terms.csv"
  "${DATA_DIR}/bad-language-terms.csv"
  "${DATA_DIR}/political-terms.csv"
  "${DATA_DIR}/sexual-terms.csv"
)
for file in "${data_required[@]}"; do
  if [ -f "$file" ]; then
    echo "OK  $file"
  else
    echo "MISSING  $file"
    missing=1
  fi
done
echo

echo "--- Upload folders ---"
for dir in \
  "${DATA_DIR}/images/profile" \
  "${DATA_DIR}/images/profile-backgrounds"; do
  if [ -d "$dir" ]; then
    echo "OK  $dir"
    ls -ld "$dir" 2>/dev/null || true
  else
    echo "MISSING  $dir (create before restart)"
    missing=1
  fi
done
if [ "$(id -u)" -eq 0 ]; then
  mkdir -p "${DATA_DIR}/images/profile" "${DATA_DIR}/images/profile-backgrounds"
  chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${DATA_DIR}/images" 2>/dev/null || true
  echo "Ensured upload folders exist under ${DATA_DIR}/images and are owned by ${SERVICE_USER}:${SERVICE_GROUP}."
fi
echo

echo "--- appsettings.json ---"
if [ -f "${DATA_DIR}/appsettings.json" ]; then
  if python3 -m json.tool "${DATA_DIR}/appsettings.json" >/dev/null 2>&1; then
    echo "JSON syntax: OK"
  else
    echo "ERROR: appsettings.json is invalid JSON."
    python3 -m json.tool "${DATA_DIR}/appsettings.json" || true
    missing=1
  fi
  if grep -q 'DefaultConnection' "${DATA_DIR}/appsettings.json"; then
    echo "ConnectionStrings:DefaultConnection: present"
  else
    echo "ERROR: ConnectionStrings:DefaultConnection missing."
    missing=1
  fi
elif [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
  echo "WARN: appsettings.json still in publish dir. Run: sudo migrate-theexonet-data"
  missing=1
else
  echo "ERROR: ${DATA_DIR}/appsettings.json not found."
  echo "Copy appsettings.json.example to ${DATA_DIR}/appsettings.json and edit secrets."
  missing=1
fi
echo

echo "--- Offworld News ---"
news_cache="${DATA_DIR}/exonet/offworld-news"
if [ -d "${news_cache}/editions" ]; then
  echo "OK  ${news_cache}/editions"
else
  echo "MISSING  ${news_cache}/editions (create and chown to ${SERVICE_USER})"
  missing=1
fi
if [ "$(id -u)" -eq 0 ]; then
  mkdir -p "${news_cache}/editions" "${news_cache}/images"
  mkdir -p "${DATA_DIR}/exonet/foreverfall/images" "${DATA_DIR}/exonet/foreverfall/rosters"
  mkdir -p "${DATA_DIR}/exonet/lunar-weather/editions"
  chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${DATA_DIR}/exonet" 2>/dev/null || true
fi
echo "--- Foreverfall Penitentiary ---"
foreverfall_cache="${DATA_DIR}/exonet/foreverfall"
for dir in "${foreverfall_cache}/images" "${foreverfall_cache}/rosters"; do
  if [ -d "$dir" ]; then
    echo "OK  $dir"
  else
    echo "MISSING  $dir (required for API startup)"
    missing=1
  fi
done
if [ "$(id -u)" -eq 0 ]; then
  mkdir -p "${foreverfall_cache}/images" "${foreverfall_cache}/rosters"
  chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "${foreverfall_cache}" 2>/dev/null || true
fi
echo

echo "--- Ownership (service user: ${SERVICE_USER}) ---"
ls -ld "${PUBLISH_DIR}" "${DATA_DIR}" "${PUBLISH_DIR}/html" 2>/dev/null || true
echo

echo "--- Recent service logs ---"
journalctl -u "${SERVICE}" -n 40 --no-pager || true
echo

echo "--- Service status ---"
if systemctl is-active --quiet "${SERVICE}" 2>/dev/null; then
  echo "${SERVICE} is active."
  if command -v ss >/dev/null 2>&1; then
    if ss -tln 2>/dev/null | grep -q ':5000'; then
      echo "Port 5000: listening."
    else
      echo "Port 5000: not listening yet (API may still be applying database migrations on startup)."
    fi
  fi
else
  echo "${SERVICE} is not active."
fi
echo

echo "--- API /api/status ---"
if command -v curl >/dev/null 2>&1; then
  if ! wait_for_http "API status" "http://127.0.0.1:5000/api/status" 12 5; then
    missing=1
  fi
else
  echo "SKIP  curl not installed"
fi
echo

echo "--- Offworld News HTTP ---"
if command -v curl >/dev/null 2>&1; then
  if ! wait_for_http "Offworld News" "http://127.0.0.1:5000/api/public/offworld-news" 18 5; then
    echo "Hint: if /api/status works but Offworld News fails, check OffworldNews:Enabled and journalctl for scheduler errors."
    missing=1
  fi
else
  echo "SKIP  curl not installed"
fi
echo

echo "--- CORS (Access-Control-Allow-Origin) ---"
check_cors_origin() {
  local label="$1"
  local origin="$2"
  local url="$3"
  local header
  header="$(curl -sI --max-time 15 -H "Origin: ${origin}" "${url}" 2>/dev/null \
    | tr -d '\r' | awk -F': ' 'tolower($1)=="access-control-allow-origin" {print $2; exit}')"
  if [ -z "${header}" ]; then
    echo "MISSING  ${label} (${origin} → ${url})"
    missing=1
    return
  fi
  if [ "${header}" = "${origin}" ]; then
    echo "OK  ${label}: ${header}"
    return
  fi
  echo "WARN  ${label}: got '${header}' (expected '${origin}')"
  missing=1
}

if command -v curl >/dev/null 2>&1; then
  check_cors_origin "game (localhost)" "https://theexonet.com" "http://127.0.0.1:5000/api/status"
  check_cors_origin "moderator (localhost)" "https://moderator.theexonet.com" "http://127.0.0.1:5000/api/status"
  check_cors_origin "game (HTTPS)" "https://theexonet.com" "https://${API_DOMAIN}/api/status"
  check_cors_origin "moderator (HTTPS)" "https://moderator.theexonet.com" "https://${API_DOMAIN}/api/status"
else
  echo "SKIP  curl not installed"
fi
echo

echo "--- Manual startup test (5s, as ${SERVICE_USER}, port 15000) ---"
if [ -f "${PUBLISH_DIR}/Theexonet.Api.dll" ]; then
  set +e
  timeout 5 sudo -u "${SERVICE_USER}" \
    env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:15000 THEEXONET_DATA_DIR="${DATA_DIR}" \
    dotnet "${PUBLISH_DIR}/Theexonet.Api.dll" 2>&1 | head -n 40
  test_status=${PIPESTATUS[0]}
  set -e
  if [ "$test_status" -eq 124 ]; then
    echo "(Timed out after 5s — API likely started on port 15000.)"
  elif [ "$test_status" -ne 0 ]; then
    echo "Manual startup exited with code ${test_status}."
    missing=1
  fi
fi
echo

if [ "$missing" -ne 0 ]; then
  echo "Fix the errors above, then run:"
  if [ ! -f "${DATA_DIR}/credits.csv" ]; then
    echo "  sudo sync-theexonet-data   # after install-theexonet-scripts"
    echo "  # or: sudo bash $(dirname "$0")/sync-publish-data.sh"
  fi
  if [ ! -f "${DATA_DIR}/appsettings.json" ] && [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
    echo "  sudo migrate-theexonet-data"
  fi
  echo "  sudo systemctl reset-failed ${SERVICE}"
  echo "  sudo restart-theexonet"
  exit 1
fi

echo "Checks passed. If systemd still fails, compare manual output above with journalctl."
