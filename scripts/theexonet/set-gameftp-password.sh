#!/bin/bash
# Set gameftp password safely and verify local FTPS login.
# Avoid interactive passwd — use this script (chpasswd + curl test).
#
#   sudo GAME_FTP_PASSWORD='YourNewPass' bash scripts/theexonet/set-gameftp-password.sh
#   sudo bash scripts/theexonet/set-gameftp-password.sh   # prompts
set -euo pipefail

GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root." >&2
  exit 1
fi

if ! id "${GAME_FTP_USER}" >/dev/null 2>&1; then
  echo "ERROR: user ${GAME_FTP_USER} does not exist. Run install-ftp-server.sh first." >&2
  exit 1
fi

if [ -z "${GAME_FTP_PASSWORD:-}" ]; then
  read -r -s -p "New password for ${GAME_FTP_USER}: " GAME_FTP_PASSWORD
  echo
  read -r -s -p "Confirm password: " confirm
  echo
  if [ "${GAME_FTP_PASSWORD}" != "${confirm}" ]; then
    echo "ERROR: passwords do not match." >&2
    exit 1
  fi
fi

if [ -z "${GAME_FTP_PASSWORD}" ]; then
  echo "ERROR: password cannot be empty." >&2
  exit 1
fi

# chpasswd uses the first ':' as delimiter — colons in the password break this path.
if printf '%s' "${GAME_FTP_PASSWORD}" | grep -q ':'; then
  echo "ERROR: password cannot contain ':' (use letters, numbers, and !@#%^&*-_)." >&2
  exit 1
fi

printf '%s:%s\n' "${GAME_FTP_USER}" "${GAME_FTP_PASSWORD}" | chpasswd

# Ensure vsftpd auth fixes remain (nologin shell + PAM).
if [ -f /etc/vsftpd.conf ] && ! grep -q '^check_shell=NO' /etc/vsftpd.conf; then
  echo 'check_shell=NO' >>/etc/vsftpd.conf
fi
if [ -f /etc/pam.d/vsftpd ]; then
  sed -i '/pam_shells\.so/d' /etc/pam.d/vsftpd
fi
echo "${GAME_FTP_USER}" >/etc/vsftpd.userlist
systemctl restart vsftpd

if command -v curl >/dev/null 2>&1; then
  if curl -fsS --ftp-ssl-reqd --insecure -u "${GAME_FTP_USER}:${GAME_FTP_PASSWORD}" "ftp://127.0.0.1/" >/dev/null; then
    echo "Password updated. Local FTPS login test: OK"
    echo "Update FileZilla Site Manager password (do not rely on saved old password)."
  else
    echo "ERROR: password set but local FTPS login test failed." >&2
    echo "  journalctl -u vsftpd -n 20 --no-pager" >&2
    exit 1
  fi
else
  echo "Password updated for ${GAME_FTP_USER}."
fi
