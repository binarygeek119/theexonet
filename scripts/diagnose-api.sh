#!/bin/bash
# Diagnose rava-api startup failures on production.
# Run on the server: sudo bash scripts/diagnose-api.sh
set -euo pipefail

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
SERVICE="${RAVA_API_SERVICE:-rava-api}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

echo "=== RAVA API diagnostics ==="
echo "Publish dir: ${PUBLISH_DIR}"
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

echo "--- Required files ---"
required=(
  "${PUBLISH_DIR}/Rava.Api.dll"
  "${PUBLISH_DIR}/appsettings.json"
  "${PUBLISH_DIR}/credits.csv"
  "${PUBLISH_DIR}/market-items.csv"
  "${PUBLISH_DIR}/trade-items.csv"
  "${PUBLISH_DIR}/hate-speech-terms.csv"
)
missing=0
for file in "${required[@]}"; do
  if [ -f "$file" ]; then
    echo "OK  $file"
  else
    echo "MISSING  $file"
    missing=1
  fi
done
echo

echo "--- appsettings.json ---"
if [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
  if python3 -m json.tool "${PUBLISH_DIR}/appsettings.json" >/dev/null 2>&1; then
    echo "JSON syntax: OK"
  else
    echo "ERROR: appsettings.json is invalid JSON."
    python3 -m json.tool "${PUBLISH_DIR}/appsettings.json" || true
    missing=1
  fi
  if grep -q 'DefaultConnection' "${PUBLISH_DIR}/appsettings.json"; then
    echo "ConnectionStrings:DefaultConnection: present"
  else
    echo "ERROR: ConnectionStrings:DefaultConnection missing."
    missing=1
  fi
else
  echo "ERROR: ${PUBLISH_DIR}/appsettings.json not found."
  echo "Copy appsettings.production.example.json to appsettings.json and edit secrets."
  missing=1
fi
echo

echo "--- Ownership (service user: ${SERVICE_USER}) ---"
ls -ld "${PUBLISH_DIR}" "${PUBLISH_DIR}/html" 2>/dev/null || true
echo

echo "--- Recent service logs ---"
journalctl -u "${SERVICE}" -n 40 --no-pager || true
echo

echo "--- Manual startup test (5s, as ${SERVICE_USER}) ---"
if [ -f "${PUBLISH_DIR}/Rava.Api.dll" ]; then
  set +e
  timeout 5 sudo -u "${SERVICE_USER}" \
    env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5000 \
    dotnet "${PUBLISH_DIR}/Rava.Api.dll" 2>&1 | head -n 40
  test_status=${PIPESTATUS[0]}
  set -e
  if [ "$test_status" -eq 124 ]; then
    echo "(Timed out after 5s — API likely started; press Ctrl+C is normal.)"
  elif [ "$test_status" -ne 0 ]; then
    echo "Manual startup exited with code ${test_status}."
    missing=1
  fi
fi
echo

if [ "$missing" -ne 0 ]; then
  echo "Fix the errors above, then run:"
  echo "  sudo systemctl reset-failed ${SERVICE}"
  echo "  sudo systemctl restart ${SERVICE}"
  exit 1
fi

echo "Checks passed. If systemd still fails, compare manual output above with journalctl."
