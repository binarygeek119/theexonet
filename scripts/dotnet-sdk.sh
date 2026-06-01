#!/bin/bash
# Shared helpers for scripts that optionally run dotnet publish on the server.
set -euo pipefail

rava_has_dotnet_sdk() {
  command -v dotnet >/dev/null 2>&1 || return 1
  dotnet --list-sdks 2>/dev/null | grep -qE '^[0-9]+\.'
}

rava_print_missing_sdk_help() {
  cat >&2 <<'EOF'
No .NET SDK found on this server (aspnetcore runtime alone cannot run "dotnet publish").

Option A — install SDK 10 (recommended for manual deploy-rava-portals):
  wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
  export PATH="/usr/share/dotnet:$PATH"
  dotnet --list-sdks

  On Ubuntu 24.04+ you may also try:
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-10.0

Option B — let GitHub Actions deploy DLLs + wwwroot (no SDK on server needed):
  Push to main and wait for the "RAVA CI" workflow to succeed.

Option C — sync portal static files only (wwwroot, no DLL rebuild):
  sudo deploy-rava-portals --static-only
EOF
}
