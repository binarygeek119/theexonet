#!/bin/bash
# Diagnose theexonet-admin, theexonet-moderator, and theexonet-docs startup failures.
# Run on the server: sudo diagnose-theexonet-portals
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=theexonet-hosting-env.sh
[ -f "${SCRIPT_DIR}/theexonet-hosting-env.sh" ] && source "${SCRIPT_DIR}/theexonet-hosting-env.sh"

PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"

# unit:port:dll:asset:http_path:manual_test_port
units=(
  "theexonet-admin:7000:Theexonet.Admin.dll:wwwroot/admin.html:/admin.html:17000"
  "theexonet-moderator:7050:Theexonet.Moderator.dll:wwwroot/moderator.html:/moderator.html:17050"
  "theexonet-docs:9000:Theexonet.Docs.dll:content/index.md:/:19000"
)

echo "=== theexonet portal diagnostics ==="
echo "Publish dir: ${PUBLISH_DIR}"
echo "Data dir:    ${DATA_DIR}"
echo "Service user: ${SERVICE_USER}"
echo

missing=0

echo "--- Required publish files ---"
shared=(
  "${PUBLISH_DIR}/Theexonet.Core.dll"
)
for file in "${shared[@]}"; do
  if [ -f "$file" ]; then
    echo "OK  $file"
  else
    echo "MISSING  $file"
    missing=1
  fi
done
echo

if [ -f "${DATA_DIR}/appsettings.json" ]; then
  echo "OK  ${DATA_DIR}/appsettings.json"
else
  echo "MISSING  ${DATA_DIR}/appsettings.json (portals read config from THEEXONET_DATA_DIR)"
  missing=1
fi
echo

port_in_use() {
  local port="$1"
  if command -v ss >/dev/null 2>&1; then
    ss -tlnp | grep -q ":${port} "
    return
  fi
  command -v netstat >/dev/null 2>&1 && netstat -tlnp 2>/dev/null | grep -q ":${port} "
}

probe_http() {
  local url="$1"
  command -v curl >/dev/null 2>&1 && curl -sf --max-time 10 "${url}" >/dev/null
}

for spec in "${units[@]}"; do
  IFS=':' read -r unit port dll asset http_path test_port <<< "$spec"
  echo "--- ${unit} (${port}) ---"
  dll_path="${PUBLISH_DIR}/${dll}"
  asset_path="${PUBLISH_DIR}/${asset}"

  if [ -f "$dll_path" ]; then
    echo "OK  $dll_path"
  else
    echo "MISSING  $dll_path (re-run deploy or copy from latest release publish/)"
    missing=1
  fi
  if [ -f "$asset_path" ]; then
    echo "OK  $asset_path"
  else
    echo "MISSING  $asset_path"
    missing=1
  fi

  service_active=0
  if systemctl is-active --quiet "${unit}" 2>/dev/null; then
    service_active=1
    echo "OK  ${unit} is active"
  else
    echo "WARN  ${unit} is not active"
    missing=1
  fi

  if port_in_use "${port}"; then
    if [ "${service_active}" -eq 1 ]; then
      echo "OK  port ${port} is in use by the running service"
    else
      echo "WARN  port ${port} is in use but ${unit} is not active:"
      ss -tlnp 2>/dev/null | grep ":${port} " || true
      missing=1
    fi
  elif [ "${service_active}" -eq 1 ]; then
    echo "WARN  ${unit} is active but port ${port} is not listening"
    missing=1
  else
    echo "OK  port ${port} is free"
  fi

  if [ "${service_active}" -eq 1 ] && command -v curl >/dev/null 2>&1; then
    if probe_http "http://127.0.0.1:${port}${http_path}"; then
      echo "HTTP probe: OK  http://127.0.0.1:${port}${http_path}"
    else
      echo "HTTP probe: FAILED  http://127.0.0.1:${port}${http_path}"
      missing=1
    fi
  fi

  if systemctl list-unit-files "${unit}.service" >/dev/null 2>&1; then
    echo "Unit ASPNETCORE_URLS:"
    grep -E '^Environment=ASPNETCORE_URLS=' "/etc/systemd/system/${unit}.service" 2>/dev/null \
      || echo "  MISSING (service may bind the wrong port from appsettings.json)"
    echo "Recent logs:"
    journalctl -u "${unit}" -n 10 --no-pager 2>/dev/null || true
  else
    echo "WARN  systemd unit ${unit}.service is not installed"
    missing=1
  fi

  if [ -f "$dll_path" ] && [ "${service_active}" -eq 0 ] && ! port_in_use "${test_port}"; then
    echo "Manual startup test (5s, as ${SERVICE_USER}, port ${test_port}):"
    set +e
    timeout 5 sudo -u "${SERVICE_USER}" \
      env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://127.0.0.1:${test_port}" THEEXONET_DATA_DIR="${DATA_DIR}" \
      dotnet "$dll_path" 2>&1 | head -n 25
    test_status=${PIPESTATUS[0]}
    set -e
    if [ "$test_status" -eq 124 ]; then
      echo "(Timed out after 5s — portal likely started on port ${test_port}.)"
    elif [ "$test_status" -ne 0 ]; then
      echo "Manual startup exited with code ${test_status}."
      missing=1
    fi
  elif [ -f "$dll_path" ] && [ "${service_active}" -eq 1 ]; then
    echo "Manual startup: skipped (${unit} already running on port ${port})"
  fi
  echo
done

if [ "$missing" -ne 0 ]; then
  echo "Fix the errors above, then run:"
  echo "  sudo deploy-theexonet-portals --static-only   # or full deploy"
  echo "  sudo restart-theexonet"
  exit 1
fi

echo "All portal checks passed."
