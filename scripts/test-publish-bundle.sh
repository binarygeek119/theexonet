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

echo "Publishing RAVA web bundle..."

dotnet publish Rava.Status/Rava.Status.csproj --configuration Release --output ./publish-status
dotnet publish Rava.Admin/Rava.Admin.csproj --configuration Release --output ./publish-admin
dotnet publish Rava.Moderator/Rava.Moderator.csproj --configuration Release --output ./publish-moderator
dotnet publish Rava.Docs/Rava.Docs.csproj --configuration Release --output ./publish-docs
bash ../scripts/sync-portal-wwwroot.sh Rava.Api/html publish-admin/wwwroot
bash ../scripts/sync-portal-wwwroot.sh Rava.Api/html publish-moderator/wwwroot

mkdir -p publish
rsync -a publish-status/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-admin/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-moderator/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a publish-docs/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'

dotnet publish Rava.Api/Rava.Api.csproj --configuration Release --output ./publish-api
rsync -a publish-api/ publish/ --exclude 'appsettings.json' --exclude 'appsettings.Development.json'
rsync -a Rava.Api/html/ publish/html/

require publish/Rava.Api.dll
require publish/Rava.Core.dll
require publish/Rava.Infrastructure.dll
require publish/Rava.Status.dll
require publish/Rava.Admin.dll
require publish/Rava.Moderator.dll
require publish/Rava.Docs.dll
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
require publish/wwwroot/css/docs.css

mkdir -p data
cp -f Rava.Api/*.csv data/
require data/credits.csv
require data/market-items.csv
require data/hate-speech-terms.csv

echo "Publish bundle checks passed."
