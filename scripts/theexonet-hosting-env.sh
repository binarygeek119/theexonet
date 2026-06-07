# Shared paths for theexonet hosting permission scripts (source only).
WWW_ROOT="${THEEXONET_WWW_ROOT:-/var/www}"
PUBLISH_DIR="${THEEXONET_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${THEEXONET_DATA_DIR:-/var/www/data}"
SERVICE_USER="${THEEXONET_SERVICE_USER:-theexonet}"
SERVICE_GROUP="${THEEXONET_SERVICE_GROUP:-$SERVICE_USER}"

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
