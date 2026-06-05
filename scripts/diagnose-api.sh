#!/bin/bash
# Diagnose rava-api startup failures on production.
# Run on the server: sudo diagnose-rava-api
set -euo pipefail

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
SERVICE="${RAVA_API_SERVICE:-rava-api}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

echo "=== RAVA API diagnostics ==="
echo "Publish dir: ${PUBLISH_DIR}"
echo "Data dir:    ${DATA_DIR} (RAVA_DATA_DIR)"
echo

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
  "${PUBLISH_DIR}/Rava.Api.dll"
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
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/images" 2>/dev/null || true
  echo "Ensured upload folders exist under ${DATA_DIR}/images and are owned by ${SERVICE_USER}."
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
  echo "WARN: appsettings.json still in publish dir. Run: sudo migrate-rava-data"
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
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/exonet" 2>/dev/null || true
fi
if command -v curl >/dev/null 2>&1; then
  if curl -sf --max-time 15 "http://127.0.0.1:5000/api/public/offworld-news" >/dev/null; then
    echo "Offworld News endpoint: OK (localhost:5000)"
  else
    echo "Offworld News endpoint: FAILED — deploy latest Rava.Api.dll or check journalctl for errors"
    missing=1
  fi
fi
echo

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
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${foreverfall_cache}" 2>/dev/null || true
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
  echo "${SERVICE} is running (port 5000 may already be in use)."
else
  echo "${SERVICE} is not active."
fi
echo

echo "--- Manual startup test (5s, as ${SERVICE_USER}, port 15000) ---"
if [ -f "${PUBLISH_DIR}/Rava.Api.dll" ]; then
  set +e
  timeout 5 sudo -u "${SERVICE_USER}" \
    env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:15000 RAVA_DATA_DIR="${DATA_DIR}" \
    dotnet "${PUBLISH_DIR}/Rava.Api.dll" 2>&1 | head -n 40
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
    echo "  sudo sync-rava-data   # after install-rava-scripts"
    echo "  # or: sudo bash $(dirname "$0")/sync-publish-data.sh"
  fi
  if [ ! -f "${DATA_DIR}/appsettings.json" ] && [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
    echo "  sudo migrate-rava-data"
  fi
  echo "  sudo systemctl reset-failed ${SERVICE}"
  echo "  sudo restart-rava"
  exit 1
fi

echo "Checks passed. If systemd still fails, compare manual output above with journalctl."
