#!/bin/bash
# Build the merged publish/ bundle used by CI and local deploy checks.
# Run from server/ (same as GitHub Actions working-directory).
set -euo pipefail

require() {
  local path="$1"
  if [ ! -e "$path" ]; then
    echo "ERROR: missing required publish artifact: ${path}" >&2
    exit 1
  fi
}

require_png() {
  local path="$1"
  require "$path"
  if [ "$(wc -c < "$path" | tr -d ' ')" -lt 256 ]; then
    echo "ERROR: ${path} is too small (likely a Git LFS pointer)." >&2
    exit 1
  fi
  local magic
  magic="$(head -c 8 "$path" | od -An -tx1 | tr -d ' \n')"
  if [ "$magic" != "89504e470d0a1a0a" ]; then
    echo "ERROR: ${path} is not a valid PNG file." >&2
    exit 1
  fi
}

echo "Publishing theexonet web bundle..."

dotnet publish Theexonet.Status/Theexonet.Status.csproj --configuration Release --output ./publish-status
dotnet publish Theexonet.Admin/Theexonet.Admin.csproj --configuration Release --output ./publish-admin
dotnet publish Theexonet.Moderator/Theexonet.Moderator.csproj --configuration Release --output ./publish-moderator
dotnet publish Theexonet.Docs/Theexonet.Docs.csproj --configuration Release --output ./publish-docs
bash ../scripts/sync-portal-wwwroot.sh Theexonet.Api/html publish-admin/wwwroot
bash ../scripts/sync-portal-wwwroot.sh Theexonet.Api/html publish-moderator/wwwroot

mkdir -p publish
rsync -a publish-status/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-admin/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-moderator/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-docs/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'

dotnet publish Theexonet.Api/Theexonet.Api.csproj --configuration Release --output ./publish-api
rsync -a publish-api/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a Theexonet.Api/html/ publish/html/

require publish/Theexonet.Api.dll
require publish/Theexonet.Core.dll
require publish/Theexonet.Infrastructure.dll
require publish/Theexonet.Status.dll
require publish/Theexonet.Admin.dll
require publish/Theexonet.Moderator.dll
require publish/Theexonet.Docs.dll
require publish/content/index.md
require publish/wwwroot/index.html
require publish/wwwroot/favicon.svg
require publish/wwwroot/js/status.js
require publish/wwwroot/admin.html
require publish/wwwroot/moderator.html
require publish/wwwroot/js/currency.js
require publish/wwwroot/js/admin-testing-mode.js

bash ../scripts/sync-publish-wwwroot.sh . publish/wwwroot

require publish/status-wwwroot/index.html
require publish/status-wwwroot/ai.html
require publish/status-wwwroot/favicon.svg
require publish/html/images/profile-defaults/female.svg
require publish/html/images/profile-defaults/male.svg
require publish/html/images/profile-defaults/neutral.svg
require_png publish/html/images/currency.png
require_png publish/wwwroot/images/currency.png

mkdir -p data
cp -f Theexonet.Api/*.csv data/
require data/credits.csv
require data/market-items.csv
require data/trade-items.csv
require data/hate-speech-terms.csv
require data/bad-language-terms.csv
require data/political-terms.csv
require data/sexual-terms.csv
require data/offworld-news-reporters.csv

echo "Publish bundle checks passed."
