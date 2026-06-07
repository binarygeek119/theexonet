#!/bin/bash
# Resolve the theexonet server/ directory (contains Theexonet.slnx and project folders).
# Prints the absolute path on success; exits 1 with a message on stderr when not found.
set -euo pipefail

looks_like_server_dir() {
  local dir="$1"
  [ -f "${dir}/Theexonet.slnx" ] || [ -f "${dir}/Theexonet.Admin/Theexonet.Admin.csproj" ] || [ -f "${dir}/Theexonet.Api/Theexonet.Api.csproj" ]
}

normalize_server_dir() {
  local dir="$1"
  if [ -f "${dir}/Theexonet.slnx" ] || [ -f "${dir}/Theexonet.Admin/Theexonet.Admin.csproj" ]; then
    cd "$dir" && pwd
    return 0
  fi
  if [ -f "${dir}/server/Theexonet.slnx" ] || [ -f "${dir}/server/Theexonet.Admin/Theexonet.Admin.csproj" ]; then
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

if [ -n "${THEEXONET_SERVER_DIR:-}" ]; then
  if resolved="$(try_dir "${THEEXONET_SERVER_DIR}")"; then
    printf '%s\n' "$resolved"
    exit 0
  fi
fi

for candidate in \
  "${SCRIPT_DIR}/../server" \
  "${PWD}/server" \
  "${PWD}" \
  "/opt/theexonet/theexonet/server" \
  "/opt/theexonet/server" \
  "/root/theexonet/server"; do
  if resolved="$(try_dir "$candidate")"; then
    printf '%s\n' "$resolved"
    exit 0
  fi
done

echo "Could not find the theexonet server directory (expected Theexonet.slnx or Theexonet.Admin/Theexonet.Admin.csproj)." >&2
echo "Options:" >&2
echo "  cd /opt/theexonet/theexonet && sudo deploy-theexonet-portals" >&2
echo "  sudo deploy-theexonet-portals /opt/theexonet/theexonet/server" >&2
echo "  export THEEXONET_SERVER_DIR=/opt/theexonet/theexonet/server && sudo deploy-theexonet-portals" >&2
exit 1
