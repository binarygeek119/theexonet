#!/bin/bash
# Replace legacy duckdns.org / RAVA production URLs with theexonet.com subdomains.
#   sudo bash scripts/theexonet/migrate-duckdns-urls.sh
#   sudo THEEXONET_DOMAIN=theexonet.com bash scripts/theexonet/migrate-duckdns-urls.sh
set -euo pipefail

DOMAIN="${THEEXONET_DOMAIN:-theexonet.com}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

# Shared sed rules for theexonet*, rava*, and broken partial migrations (ravaadmin.theexonet.com).
apply_legacy_url_sed() {
  local file="$1"
  sed -i \
    -e "s|https://theexonetapi\\.binarygeek119\\.duckdns\\.org|https://api.${DOMAIN}|g" \
    -e "s|https://theexonetstatus\\.binarygeek119\\.duckdns\\.org|https://status.${DOMAIN}|g" \
    -e "s|https://theexonetadmin\\.binarygeek119\\.duckdns\\.org|https://admin.${DOMAIN}|g" \
    -e "s|https://theexonetmoderator\\.binarygeek119\\.duckdns\\.org|https://moderator.${DOMAIN}|g" \
    -e "s|https://theexonetdocs\\.binarygeek119\\.duckdns\\.org|https://docs.${DOMAIN}|g" \
    -e "s|https://theexonet\\.binarygeek119\\.duckdns\\.org|https://${DOMAIN}|g" \
    -e "s|https://ravaapi\\.binarygeek119\\.duckdns\\.org|https://api.${DOMAIN}|g" \
    -e "s|https://ravastatus\\.binarygeek119\\.duckdns\\.org|https://status.${DOMAIN}|g" \
    -e "s|https://ravaadmin\\.binarygeek119\\.duckdns\\.org|https://admin.${DOMAIN}|g" \
    -e "s|https://ravamoderator\\.binarygeek119\\.duckdns\\.org|https://moderator.${DOMAIN}|g" \
    -e "s|https://ravadocs\\.binarygeek119\\.duckdns\\.org|https://docs.${DOMAIN}|g" \
    -e "s|https://rava\\.binarygeek119\\.duckdns\\.org|https://${DOMAIN}|g" \
    -e "s|https://ravaapi\\.${DOMAIN}|https://api.${DOMAIN}|g" \
    -e "s|https://ravastatus\\.${DOMAIN}|https://status.${DOMAIN}|g" \
    -e "s|https://ravaadmin\\.${DOMAIN}|https://admin.${DOMAIN}|g" \
    -e "s|https://ravamoderator\\.${DOMAIN}|https://moderator.${DOMAIN}|g" \
    -e "s|https://ravadocs\\.${DOMAIN}|https://docs.${DOMAIN}|g" \
    -e "s|theexonetapi\\.binarygeek119\\.duckdns\\.org|api.${DOMAIN}|g" \
    -e "s|theexonetadmin\\.binarygeek119\\.duckdns\\.org|admin.${DOMAIN}|g" \
    -e "s|theexonetmoderator\\.binarygeek119\\.duckdns\\.org|moderator.${DOMAIN}|g" \
    -e "s|theexonet\\.binarygeek119\\.duckdns\\.org|${DOMAIN}|g" \
    -e "s|ravaapi\\.binarygeek119\\.duckdns\\.org|api.${DOMAIN}|g" \
    -e "s|ravaadmin\\.binarygeek119\\.duckdns\\.org|admin.${DOMAIN}|g" \
    -e "s|rava\\.binarygeek119\\.duckdns\\.org|${DOMAIN}|g" \
    "$file"
}

replace_in_tree() {
  local dir="$1"
  [ -d "$dir" ] || return 0
  find "$dir" -type f \( -name '*.html' -o -name '*.js' -o -name '*.json' -o -name '*.md' \) -print0 \
    | while IFS= read -r -d '' file; do
        apply_legacy_url_sed "$file"
      done
}

echo "Migrating URLs under ${PUBLISH_DIR}/html and ${DATA_DIR}…"
replace_in_tree "${PUBLISH_DIR}/html"
replace_in_tree "${PUBLISH_DIR}/wwwroot"
replace_in_tree "${PUBLISH_DIR}/content"

if [ -f "${DATA_DIR}/appsettings.json" ]; then
  apply_legacy_url_sed "${DATA_DIR}/appsettings.json"
  sed -i \
    -e 's|"SiteTitle": "RAVA Game Docs"|"SiteTitle": "theexonet Game Docs"|g' \
    -e 's|"FromName": "RAVA |"FromName": "theexonet |g' \
    "${DATA_DIR}/appsettings.json"
fi

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
if [ -f "${SCRIPT_DIR}/sync-postgres-password.sh" ]; then
  bash "${SCRIPT_DIR}/sync-postgres-password.sh"
fi

echo "Done. Restart services: sudo restart-theexonet"
echo "Hard-refresh the game in your browser (Ctrl+Shift+R)."
