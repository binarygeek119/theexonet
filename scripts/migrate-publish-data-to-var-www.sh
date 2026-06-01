#!/bin/bash
# One-time migration: move appsettings, CSVs, and player images from publish to /var/www/data.
# Run on the server as root: sudo bash scripts/migrate-publish-data-to-var-www.sh
set -euo pipefail

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

mkdir -p "${DATA_DIR}"

files=(
  appsettings.json
  appsettings.Development.json
  credits.csv
  market-items.csv
  trade-items.csv
  hate-speech-terms.csv
  bad-language-terms.csv
  political-terms.csv
  sexual-terms.csv
)

moved=0
for file in "${files[@]}"; do
  src="${PUBLISH_DIR}/${file}"
  dest="${DATA_DIR}/${file}"
  if [ -f "$src" ] && [ ! -f "$dest" ]; then
    mv -f "$src" "$dest"
    echo "Moved ${src} -> ${dest}"
    moved=1
  elif [ -f "$src" ] && [ -f "$dest" ]; then
    echo "SKIP ${file} (already in ${DATA_DIR})"
  fi
done

migrate_image_dir() {
  local subdir="$1"
  local src="${PUBLISH_DIR}/html/images/${subdir}"
  local dest="${DATA_DIR}/images/${subdir}"
  if [ ! -d "$src" ]; then
    return
  fi
  if [ ! -d "$(dirname "$dest")" ]; then
    mkdir -p "${DATA_DIR}/images"
  fi
  if [ ! -d "$dest" ]; then
    mv "$src" "$dest"
    echo "Moved ${src} -> ${dest}"
    moved=1
    return
  fi
  if [ -n "$(ls -A "$src" 2>/dev/null || true)" ]; then
    rsync -a "${src}/" "${dest}/"
    rm -rf "$src"
    echo "Merged ${src} -> ${dest}"
    moved=1
  fi
}

migrate_image_dir profile
migrate_image_dir profile-backgrounds

migrate_offworld_news_dir() {
  local subdir="$1"
  local src="${PUBLISH_DIR}/html/exonet/offworld-news/${subdir}"
  local dest="${DATA_DIR}/exonet/offworld-news/${subdir}"
  if [ ! -d "$src" ]; then
    return
  fi
  mkdir -p "${DATA_DIR}/exonet/offworld-news"
  if [ ! -d "$dest" ]; then
    mv "$src" "$dest"
    echo "Moved ${src} -> ${dest}"
    moved=1
    return
  fi
  if [ -n "$(ls -A "$src" 2>/dev/null || true)" ]; then
    rsync -a "${src}/" "${dest}/"
    rm -rf "$src"
    echo "Merged ${src} -> ${dest}"
    moved=1
  fi
}

migrate_offworld_news_dir editions
migrate_offworld_news_dir images

if [ "$moved" -eq 0 ]; then
  echo "Nothing to migrate from ${PUBLISH_DIR}."
fi

chown "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}" 2>/dev/null || true
chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/images" 2>/dev/null || true
chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/exonet" 2>/dev/null || true
chown "${SERVICE_USER}:${SERVICE_USER}" "${DATA_DIR}/"* 2>/dev/null || true

echo "Ensure systemd units set RAVA_DATA_DIR=${DATA_DIR}, then run: sudo restart-rava"
