#!/bin/bash
# Diagnose gameftp FTPS login (local + optional remote host).
#   sudo bash scripts/theexonet/diagnose-ftps.sh
#   GAME_FTP_PASSWORD='...' bash scripts/theexonet/diagnose-ftps.sh theexonet.com
set -euo pipefail

GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
TEST_HOST="${1:-127.0.0.1}"
GAME_FTP_PASSWORD="${GAME_FTP_PASSWORD:-${DEPLOY_FTP_PASSWORD:-}}"

echo "=== FTPS diagnostics (${GAME_FTP_USER} @ ${TEST_HOST}) ==="

if ! id "${GAME_FTP_USER}" >/dev/null 2>&1; then
  echo "ERROR: user ${GAME_FTP_USER} does not exist. Run install-ftp-server.sh" >&2
  exit 1
fi
echo "OK  user ${GAME_FTP_USER} exists"

if systemctl is-active --quiet vsftpd 2>/dev/null; then
  echo "OK  vsftpd is active"
else
  echo "ERROR: vsftpd is not active" >&2
  exit 1
fi

if [ -f /etc/vsftpd.userlist ]; then
  if grep -qx "${GAME_FTP_USER}" /etc/vsftpd.userlist; then
    echo "OK  ${GAME_FTP_USER} in /etc/vsftpd.userlist"
  else
    echo "ERROR: ${GAME_FTP_USER} missing from /etc/vsftpd.userlist" >&2
    echo "  echo ${GAME_FTP_USER} | sudo tee /etc/vsftpd.userlist && sudo systemctl restart vsftpd" >&2
    exit 1
  fi
else
  echo "WARN  /etc/vsftpd.userlist missing"
fi

if [ -z "${GAME_FTP_PASSWORD}" ]; then
  read -r -s -p "Password for ${GAME_FTP_USER}: " GAME_FTP_PASSWORD
  echo
fi

if [ -z "${GAME_FTP_PASSWORD}" ]; then
  echo "ERROR: password required (GAME_FTP_PASSWORD or prompt)" >&2
  exit 1
fi

if command -v curl >/dev/null 2>&1; then
  if curl -fsS --ftp-ssl-reqd --insecure -u "${GAME_FTP_USER}:${GAME_FTP_PASSWORD}" "ftp://${TEST_HOST}/" >/dev/null; then
    echo "FTPS login test: OK  ftp://${TEST_HOST}/"
  else
    echo "FTPS login test: FAILED  ftp://${TEST_HOST}/" >&2
    echo "Reset password: sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/set-gameftp-password.sh" >&2
    echo "Then update GitHub secret DEPLOY_FTP_PASSWORD to the same value." >&2
    journalctl -u vsftpd -n 15 --no-pager 2>/dev/null || true
    exit 1
  fi
else
  echo "SKIP  curl not installed"
fi

echo ""
echo "GitHub must use:"
echo "  DEPLOY_FTP_USER = gameftp   (not githubdeploy — that is SSH only)"
echo "  DEPLOY_FTP_PASSWORD = same password tested above"
