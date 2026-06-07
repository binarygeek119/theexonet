#!/bin/bash
# Replace legacy duckdns.org production URLs with theexonet.com subdomains on a live server.
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

replace_in_tree() {
  local dir="$1"
  [ -d "$dir" ] || return 0
  find "$dir" -type f \( -name '*.html' -o -name '*.js' -o -name '*.json' -o -name '*.md' \) -print0 \
    | while IFS= read -r -d '' file; do
        sed -i \
          -e "s|https://theexonetapi\\.binarygeek119\\.duckdns\\.org|https://api.${DOMAIN}|g" \
          -e "s|https://theexonetstatus\\.binarygeek119\\.duckdns\\.org|https://status.${DOMAIN}|g" \
          -e "s|https://theexonetadmin\\.binarygeek119\\.duckdns\\.org|https://admin.${DOMAIN}|g" \
          -e "s|https://theexonetmoderator\\.binarygeek119\\.duckdns\\.org|https://moderator.${DOMAIN}|g" \
          -e "s|https://theexonetdocs\\.binarygeek119\\.duckdns\\.org|https://docs.${DOMAIN}|g" \
          -e "s|https://theexonet\\.binarygeek119\\.duckdns\\.org|https://${DOMAIN}|g" \
          -e "s|theexonetapi\\.binarygeek119\\.duckdns\\.org|api.${DOMAIN}|g" \
          -e "s|theexonetadmin\\.binarygeek119\\.duckdns\\.org|admin.${DOMAIN}|g" \
          -e "s|theexonetmoderator\\.binarygeek119\\.duckdns\\.org|moderator.${DOMAIN}|g" \
          -e "s|theexonet\\.binarygeek119\\.duckdns\\.org|${DOMAIN}|g" \
          "$file"
      done
}

echo "Migrating URLs under ${PUBLISH_DIR}/html and ${DATA_DIR}…"
replace_in_tree "${PUBLISH_DIR}/html"
replace_in_tree "${PUBLISH_DIR}/wwwroot"
if [ -f "${DATA_DIR}/appsettings.json" ]; then
  sed -i \
    -e "s|https://theexonetapi\\.binarygeek119\\.duckdns\\.org|https://api.${DOMAIN}|g" \
    -e "s|https://theexonetstatus\\.binarygeek119\\.duckdns\\.org|https://status.${DOMAIN}|g" \
    -e "s|https://theexonetadmin\\.binarygeek119\\.duckdns\\.org|https://admin.${DOMAIN}|g" \
    -e "s|https://theexonetmoderator\\.binarygeek119\\.duckdns\\.org|https://moderator.${DOMAIN}|g" \
    -e "s|https://theexonetdocs\\.binarygeek119\\.duckdns\\.org|https://docs.${DOMAIN}|g" \
    -e "s|https://theexonet\\.binarygeek119\\.duckdns\\.org|https://${DOMAIN}|g" \
    -e 's|"SiteTitle": "RAVA Game Docs"|"SiteTitle": "theexonet Game Docs"|g' \
    -e 's|"FromName": "RAVA |"FromName": "theexonet |g' \
    -e 's|Database=rava|Database=theexonet|g' \
    -e 's|Username=postgres|Username=theexonet|g' \
    -e 's|Host=localhost|Host=127.0.0.1|g' \
    "${DATA_DIR}/appsettings.json"
fi

replace_in_tree "${PUBLISH_DIR}/content"

echo "Done. Hard-refresh the game in your browser (Ctrl+Shift+R)."
