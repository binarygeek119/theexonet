#!/usr/bin/env bash
# Install theexonet helper scripts to /usr/local/bin (files live under /usr/local/lib/theexonet/scripts).
# Run on the server as root (always use bash — do not rely on ./ if the file has Windows line endings):
#   sudo bash scripts/install-bin-scripts.sh
# Re-run after git pull to refresh lib files and symlinks:
#   sudo install-theexonet-scripts
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
LIB_DIR="${THEEXONET_LIB_DIR:-/usr/local/lib/theexonet/scripts}"

looks_like_scripts_dir() {
  local dir="$1"
  [ -d "${dir}/systemd" ] && [ -f "${dir}/restart-theexonet.sh" ]
}

resolve_repo_scripts_dir() {
  local candidate server_dir repo_root

  if [ -n "${THEEXONET_SCRIPTS_DIR:-}" ] && looks_like_scripts_dir "${THEEXONET_SCRIPTS_DIR}"; then
    cd "${THEEXONET_SCRIPTS_DIR}" && pwd
    return 0
  fi

  if [ -f "${SCRIPT_DIR}/resolve-server-dir.sh" ]; then
    if server_dir="$(bash "${SCRIPT_DIR}/resolve-server-dir.sh" 2>/dev/null || true)"; then
      repo_root="$(dirname "${server_dir}")"
      candidate="${repo_root}/scripts"
      if looks_like_scripts_dir "${candidate}"; then
        cd "${candidate}" && pwd
        return 0
      fi
    fi
  fi

  for candidate in \
    "/opt/theexonet/theexonet/scripts" \
    "/opt/theexonet/scripts" \
    "${PWD}/scripts"; do
    if looks_like_scripts_dir "${candidate}"; then
      cd "${candidate}" && pwd
      return 0
    fi
  done

  return 1
}

if [ -n "${1:-}" ]; then
  SRC_DIR="$(cd "${1}" && pwd)"
elif [ "$(readlink -f "${SCRIPT_DIR}")" = "$(readlink -f "${LIB_DIR}")" ]; then
  if ! SRC_DIR="$(resolve_repo_scripts_dir)"; then
    echo "Could not find git scripts directory. After git pull, run one of:" >&2
    echo "  sudo install-theexonet-scripts /opt/theexonet/theexonet/scripts" >&2
    echo "  cd /opt/theexonet/theexonet && sudo bash scripts/install-bin-scripts.sh" >&2
    exit 1
  fi
  echo "Using git scripts from ${SRC_DIR}"
else
  SRC_DIR="${SCRIPT_DIR}"
fi

copy_script() {
  local src="$1"
  local dest="$2"
  if [ "$(readlink -f "${src}")" = "$(readlink -f "${dest}")" ]; then
    return 0
  fi
  cp -f "${src}" "${dest}"
}
TEMPLATE_DIR="${THEEXONET_TEMPLATE_DATA_DIR:-/usr/local/lib/theexonet/data}"
HTML_TEMPLATE_DIR="${THEEXONET_HTML_TEMPLATE_DIR:-/usr/local/lib/theexonet/html}"
BIN_DIR="/usr/local/bin"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ ! -d "${SRC_DIR}/systemd" ]; then
  echo "Missing ${SRC_DIR}/systemd — pass the repo scripts directory." >&2
  exit 1
fi

mkdir -p "${LIB_DIR}/systemd"

for script in \
  restart-theexonet.sh \
  diagnose-api.sh \
  diagnose-portals.sh \
  diagnose-testing-friends.sh \
  install-systemd-units.sh \
  install-portal-units.sh \
  deploy-html.sh \
  deploy-portals.sh \
  deploy-status.sh \
  resolve-server-dir.sh \
  dotnet-sdk.sh \
  sync-portal-wwwroot.sh \
  sync-status-wwwroot.sh \
  sync-docs-wwwroot.sh \
  sync-publish-wwwroot.sh \
  migrate-publish-data-to-var-www.sh \
  sync-publish-data.sh \
  pull-server-repo.sh \
  fix-hosting-permissions.sh \
  audit-hosting-permissions.sh \
  theexonet-hosting-env.sh \
  theexonet-permissions-watch.sh \
  install-permissions-service.sh \
  install-theexonet-permissions-service.sh \
  install-bin-scripts.sh; do
  if [ ! -f "${SRC_DIR}/${script}" ]; then
    echo "Missing ${SRC_DIR}/${script}" >&2
    exit 1
  fi
  copy_script "${SRC_DIR}/${script}" "${LIB_DIR}/${script}"
  chmod 755 "${LIB_DIR}/${script}"
done

if [ -f "${SRC_DIR}/theexonet/promote-staging.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/promote-staging.sh" "${LIB_DIR}/promote-staging.sh"
  chmod 755 "${LIB_DIR}/promote-staging.sh"
fi
if [ -f "${SRC_DIR}/install-staging-watcher.sh" ]; then
  copy_script "${SRC_DIR}/install-staging-watcher.sh" "${LIB_DIR}/install-staging-watcher.sh"
  chmod 755 "${LIB_DIR}/install-staging-watcher.sh"
fi
if [ -f "${SRC_DIR}/theexonet/staging-watcher.sh" ]; then
  mkdir -p "${LIB_DIR}/theexonet"
  copy_script "${SRC_DIR}/theexonet/staging-watcher.sh" "${LIB_DIR}/theexonet/staging-watcher.sh"
  copy_script "${SRC_DIR}/theexonet/promote-staging.sh" "${LIB_DIR}/theexonet/promote-staging.sh"
  chmod 755 "${LIB_DIR}/theexonet/staging-watcher.sh" "${LIB_DIR}/theexonet/promote-staging.sh"
fi
if [ -f "${SRC_DIR}/theexonet/install-ssl-certs.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/install-ssl-certs.sh" "${LIB_DIR}/install-ssl-certs.sh"
  chmod 755 "${LIB_DIR}/install-ssl-certs.sh"
fi
if [ -f "${SRC_DIR}/theexonet/install-apache-ssl-vhosts.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/install-apache-ssl-vhosts.sh" "${LIB_DIR}/install-apache-ssl-vhosts.sh"
  chmod 755 "${LIB_DIR}/install-apache-ssl-vhosts.sh"
fi
if [ -f "${SRC_DIR}/theexonet/apache-theexonet-ssl.conf" ]; then
  copy_script "${SRC_DIR}/theexonet/apache-theexonet-ssl.conf" "${LIB_DIR}/apache-theexonet-ssl.conf"
fi
if [ -f "${SRC_DIR}/theexonet/sync-postgres-password.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/sync-postgres-password.sh" "${LIB_DIR}/sync-postgres-password.sh"
  chmod 755 "${LIB_DIR}/sync-postgres-password.sh"
fi
if [ -f "${SRC_DIR}/theexonet/setup-github-ssh-restart.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/setup-github-ssh-restart.sh" "${LIB_DIR}/setup-github-ssh-restart.sh"
  chmod 755 "${LIB_DIR}/setup-github-ssh-restart.sh"
fi
if [ -f "${SRC_DIR}/theexonet/diagnose-ftps.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/diagnose-ftps.sh" "${LIB_DIR}/diagnose-ftps.sh"
  chmod 755 "${LIB_DIR}/diagnose-ftps.sh"
fi
if [ -f "${SRC_DIR}/theexonet/set-gameftp-password.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/set-gameftp-password.sh" "${LIB_DIR}/set-gameftp-password.sh"
  chmod 755 "${LIB_DIR}/set-gameftp-password.sh"
fi
if [ -f "${SRC_DIR}/github-deploy-restart.sh" ]; then
  copy_script "${SRC_DIR}/github-deploy-restart.sh" "${LIB_DIR}/github-deploy-restart.sh"
  chmod 755 "${LIB_DIR}/github-deploy-restart.sh"
fi
if [ -f "${SRC_DIR}/github-deploy-ssh.sh" ]; then
  copy_script "${SRC_DIR}/github-deploy-ssh.sh" "${LIB_DIR}/github-deploy-ssh.sh"
  chmod 755 "${LIB_DIR}/github-deploy-ssh.sh"
fi
if [ -f "${SRC_DIR}/theexonet/stage-github-upload.sh" ]; then
  copy_script "${SRC_DIR}/theexonet/stage-github-upload.sh" "${LIB_DIR}/theexonet/stage-github-upload.sh"
  chmod 755 "${LIB_DIR}/theexonet/stage-github-upload.sh"
fi
if ! command -v unzip >/dev/null 2>&1 || ! command -v rsync >/dev/null 2>&1; then
  echo "WARN: install unzip rsync for promote-theexonet-staging (apt install unzip rsync)" >&2
fi

cp -f "${SRC_DIR}/systemd/"*.service "${LIB_DIR}/systemd/"
if [ -f "${SRC_DIR}/systemd/theexonet-permissions.default" ]; then
  cp -f "${SRC_DIR}/systemd/theexonet-permissions.default" "${LIB_DIR}/systemd/theexonet-permissions.default"
fi

REPO_API_DIR="${SRC_DIR}/../server/Theexonet.Api"
if [ -d "${REPO_API_DIR}" ]; then
  mkdir -p "${TEMPLATE_DIR}"
  for csv in credits.csv market-items.csv trade-items.csv hate-speech-terms.csv bad-language-terms.csv political-terms.csv sexual-terms.csv offworld-news-reporters.csv; do
    if [ -f "${REPO_API_DIR}/${csv}" ]; then
      cp -f "${REPO_API_DIR}/${csv}" "${TEMPLATE_DIR}/${csv}"
    fi
  done
  echo "Installed CSV template files to ${TEMPLATE_DIR}"

  if [ -d "${REPO_API_DIR}/html" ]; then
    mkdir -p "${HTML_TEMPLATE_DIR}"
    rsync -a --delete \
      --exclude 'uploads/' \
      --exclude 'images/profile/' \
      --exclude 'images/profile-backgrounds/' \
      --exclude 'exonet/offworld-news/editions/' \
      --exclude 'exonet/offworld-news/images/' \
      --exclude 'exonet/offworld-news/reporters/' \
      "${REPO_API_DIR}/html/" "${HTML_TEMPLATE_DIR}/"
    echo "Installed html template to ${HTML_TEMPLATE_DIR}"
  fi
fi

declare -A bin_links=(
  [restart-theexonet.sh]=restart-theexonet
  [diagnose-api.sh]=diagnose-theexonet-api
  [diagnose-portals.sh]=diagnose-theexonet-portals
  [diagnose-testing-friends.sh]=diagnose-theexonet-testing-friends
  [install-systemd-units.sh]=install-theexonet-systemd
  [install-portal-units.sh]=install-theexonet-portals
  [install-bin-scripts.sh]=install-theexonet-scripts
  [deploy-html.sh]=deploy-theexonet-html
  [deploy-portals.sh]=deploy-theexonet-portals
  [deploy-status.sh]=deploy-theexonet-status
  [sync-publish-data.sh]=sync-theexonet-data
  [migrate-publish-data-to-var-www.sh]=migrate-theexonet-data
  [fix-hosting-permissions.sh]=fix-theexonet-permissions
  [audit-hosting-permissions.sh]=audit-theexonet-permissions
  [install-permissions-service.sh]=install-theexonet-permissions-service
  [install-theexonet-permissions-service.sh]=install-theexonet-permissions-service
  [promote-staging.sh]=promote-theexonet-staging
  [install-ssl-certs.sh]=install-theexonet-ssl
  [install-apache-ssl-vhosts.sh]=install-theexonet-apache-ssl
  [sync-postgres-password.sh]=sync-theexonet-postgres-password
  [setup-github-ssh-restart.sh]=setup-theexonet-github-ssh
  [diagnose-ftps.sh]=diagnose-theexonet-ftps
  [set-gameftp-password.sh]=set-theexonet-gameftp-password
  [stage-github-upload.sh]=stage-theexonet-upload
  [pull-server-repo.sh]=pull-theexonet-repo
)

for src in "${!bin_links[@]}"; do
  ln -sf "${LIB_DIR}/${src}" "${BIN_DIR}/${bin_links[$src]}"
  echo "Installed ${BIN_DIR}/${bin_links[$src]}"
done

echo "Done. Example: sudo restart-theexonet | sudo install-theexonet-permissions-service"
