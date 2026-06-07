#!/bin/bash
# Resolve the theexonet server/ directory (contains Rava.slnx and project folders).
# Prints the absolute path on success; exits 1 with a message on stderr when not found.
set -euo pipefail

looks_like_server_dir() {
  local dir="$1"
  [ -f "${dir}/Rava.slnx" ] || [ -f "${dir}/Rava.Admin/Rava.Admin.csproj" ] || [ -f "${dir}/Rava.Api/Rava.Api.csproj" ]
}

normalize_server_dir() {
  local dir="$1"
  if [ -f "${dir}/Rava.slnx" ] || [ -f "${dir}/Rava.Admin/Rava.Admin.csproj" ]; then
    cd "$dir" && pwd
    return 0
  fi
  if [ -f "${dir}/server/Rava.slnx" ] || [ -f "${dir}/server/Rava.Admin/Rava.Admin.csproj" ]; then
    cd "${dir}/server" && pwd
    return 0
  fi
  return 1
}

try_dir() {
  local dir="${1:-}"
  [ -n "$dir" ] || return 1
  [ -d "$dir" ] || return 1
  normalize_server_dir "$dir"
}

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"

if resolved="$(try_dir "${1:-}")"; then
  printf '%s\n' "$resolved"
  exit 0
fi

if [ -n "${RAVA_SERVER_DIR:-}" ]; then
  if resolved="$(try_dir "${RAVA_SERVER_DIR}")"; then
    printf '%s\n' "$resolved"
    exit 0
  fi
fi

for candidate in \
  "${SCRIPT_DIR}/../server" \
  "${PWD}/server" \
  "${PWD}" \
  "/opt/rava/rava/server" \
  "/opt/rava/server" \
  "/root/rava/server"; do
  if resolved="$(try_dir "$candidate")"; then
    printf '%s\n' "$resolved"
    exit 0
  fi
done

echo "Could not find the theexonet server directory (expected Rava.slnx or Rava.Admin/Rava.Admin.csproj)." >&2
echo "Options:" >&2
echo "  cd /opt/rava/rava && sudo deploy-rava-portals" >&2
echo "  sudo deploy-rava-portals /opt/rava/rava/server" >&2
echo "  export RAVA_SERVER_DIR=/opt/rava/rava/server && sudo deploy-rava-portals" >&2
exit 1
