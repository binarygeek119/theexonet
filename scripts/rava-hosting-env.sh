# Shared paths for RAVA hosting permission scripts (source only).
WWW_ROOT="${RAVA_WWW_ROOT:-/var/www}"
PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
SERVICE_USER="${RAVA_SERVICE_USER:-www-data}"
SERVICE_GROUP="${RAVA_SERVICE_GROUP:-$SERVICE_USER}"

# Writable data directories (mode 2775, owned by SERVICE_USER).
DATA_WRITABLE_DIRS=(
  "${DATA_DIR}"
  "${DATA_DIR}/images"
  "${DATA_DIR}/images/profile"
  "${DATA_DIR}/images/profile-backgrounds"
  "${DATA_DIR}/images/company-logos"
  "${DATA_DIR}/exonet"
  "${DATA_DIR}/exonet/offworld-news"
  "${DATA_DIR}/exonet/offworld-news/editions"
  "${DATA_DIR}/exonet/offworld-news/images"
  "${DATA_DIR}/exonet/offworld-news/reporters"
)

# Publish paths that must be writable by the API user.
PUBLISH_WRITABLE_DIRS=(
  "${PUBLISH_DIR}/.aspnet"
)
