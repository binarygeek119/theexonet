#!/bin/bash
# Diagnose rava-admin, rava-moderator, and rava-docs startup failures.
# Run on the server: sudo bash scripts/diagnose-portals.sh
set -euo pipefail

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

units=(rava-admin:7000:Rava.Admin.dll:wwwroot/admin.html rava-moderator:7050:Rava.Moderator.dll:wwwroot/moderator.html rava-docs:9000:Rava.Docs.dll:content/index.md)

echo "=== RAVA portal diagnostics ==="
echo "Publish dir: ${PUBLISH_DIR}"
echo

echo "--- Required publish files ---"
shared=(
  "${PUBLISH_DIR}/appsettings.json"
  "${PUBLISH_DIR}/Rava.Core.dll"
)
for file in "${shared[@]}"; do
  if [ -f "$file" ]; then
    echo "OK  $file"
  else
    echo "MISSING  $file"
  fi
done
echo

for spec in "${units[@]}"; do
  IFS=':' read -r unit port dll asset <<< "$spec"
  echo "--- ${unit} (${port}) ---"
  dll_path="${PUBLISH_DIR}/${dll}"
  asset_path="${PUBLISH_DIR}/${asset}"
  if [ -f "$dll_path" ]; then
    echo "OK  $dll_path"
  else
    echo "MISSING  $dll_path (re-run deploy or copy from latest release publish/)"
  fi
  if [ -f "$asset_path" ]; then
    echo "OK  $asset_path"
  else
    echo "MISSING  $asset_path"
  fi

  if command -v ss >/dev/null 2>&1; then
    if ss -tlnp | grep -q ":${port} "; then
      echo "WARN  port ${port} is already in use:"
      ss -tlnp | grep ":${port} " || true
    else
      echo "OK  port ${port} is free"
    fi
  fi

  if systemctl list-unit-files "${unit}.service" >/dev/null 2>&1; then
    if systemd-analyze verify "/etc/systemd/system/${unit}.service" 2>&1 | grep -q .; then
      systemd-analyze verify "/etc/systemd/system/${unit}.service" 2>&1 || true
    fi
    echo "Unit ASPNETCORE_URLS:"
    grep -E '^Environment=ASPNETCORE_URLS=' "/etc/systemd/system/${unit}.service" || echo "  MISSING (service may bind the wrong port from appsettings.json)"
    echo "Recent logs:"
    journalctl -u "${unit}" -n 15 --no-pager || true
  else
    echo "WARN  systemd unit ${unit}.service is not installed"
  fi

  if [ -f "$dll_path" ]; then
    echo "Manual startup test (5s, as ${SERVICE_USER}):"
    set +e
    timeout 5 sudo -u "${SERVICE_USER}" \
      env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://127.0.0.1:${port}" \
      dotnet "$dll_path" 2>&1 | head -n 25
    test_status=${PIPESTATUS[0]}
    set -e
    if [ "$test_status" -eq 124 ]; then
      echo "(Timed out after 5s — portal likely started.)"
    elif [ "$test_status" -ne 0 ]; then
      echo "Manual startup exited with code ${test_status}."
    fi
  fi
  echo
done

echo "If DLLs are missing, redeploy from main or install units with:"
echo "  sudo bash scripts/install-systemd-units.sh"
