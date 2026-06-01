#!/bin/bash
# Wrapper so PATH command matches script name (install-rava-permissions-service).
exec "$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)/install-permissions-service.sh" "$@"
