#!/bin/bash
# Diagnose gameftp FTPS login (shadow check + FTPS probe).
#   sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/diagnose-ftps.sh
#   sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/diagnose-ftps.sh theexonet.com
set -euo pipefail

GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
TEST_HOST="${1:-127.0.0.1}"
GAME_FTP_PASSWORD="${GAME_FTP_PASSWORD:-${DEPLOY_FTP_PASSWORD:-}}"

echo "=== FTPS diagnostics (${GAME_FTP_USER} @ ${TEST_HOST}) ==="

if ! id "${GAME_FTP_USER}" >/dev/null 2>&1; then
  echo "ERROR: user ${GAME_FTP_USER} does not exist. Run install-ftp-server.sh" >&2
  exit 1
fi
echo "OK  user ${GAME_FTP_USER} exists (shell=$(getent passwd "${GAME_FTP_USER}" | cut -d: -f7))"

if systemctl is-active --quiet vsftpd 2>/dev/null; then
  echo "OK  vsftpd is active"
else
  echo "ERROR: vsftpd is not active" >&2
  exit 1
fi

if ss -tln 2>/dev/null | grep -q ':21 '; then
  echo "OK  port 21 is listening"
else
  echo "ERROR: nothing listening on port 21" >&2
  exit 1
fi

if [ -f /etc/vsftpd.userlist ] && grep -qx "${GAME_FTP_USER}" /etc/vsftpd.userlist; then
  echo "OK  ${GAME_FTP_USER} in /etc/vsftpd.userlist"
else
  echo "ERROR: ${GAME_FTP_USER} missing from /etc/vsftpd.userlist" >&2
  exit 1
fi

if [ -f /etc/pam.d/vsftpd ] && grep -q 'pam_shells.so' /etc/pam.d/vsftpd; then
  echo "WARN  /etc/pam.d/vsftpd still has pam_shells.so — run set-gameftp-password.sh to fix" >&2
fi

shadow_hash="$(getent shadow "${GAME_FTP_USER}" | cut -d: -f2)"
if [ -z "${shadow_hash}" ] || [[ "${shadow_hash}" == "!"* ]] || [ "${shadow_hash}" = "*" ]; then
  echo "ERROR: ${GAME_FTP_USER} shadow password is locked or empty" >&2
  echo "  sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/set-gameftp-password.sh" >&2
  exit 1
fi
echo "OK  ${GAME_FTP_USER} has an unlocked shadow password"

if [ -z "${GAME_FTP_PASSWORD}" ]; then
  read -r -s -p "Password for ${GAME_FTP_USER}: " GAME_FTP_PASSWORD
  echo
fi

if [ -z "${GAME_FTP_PASSWORD}" ]; then
  echo "ERROR: password required (GAME_FTP_PASSWORD or prompt)" >&2
  exit 1
fi

python3 - "${GAME_FTP_USER}" "${GAME_FTP_PASSWORD}" <<'PY' || {
import crypt
import spwd
import sys

user, password = sys.argv[1], sys.argv[2]
rec = spwd.getspnam(user)
if crypt.crypt(password, rec.sp_pwdp) != rec.sp_pwdp:
    raise SystemExit(1)
PY
  echo "ERROR: password does not match /etc/shadow for ${GAME_FTP_USER}" >&2
  echo "  Re-run: sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/set-gameftp-password.sh" >&2
  exit 1
}
echo "OK  password matches /etc/shadow"

test_ftps() {
  local host="$1"
  if command -v lftp >/dev/null 2>&1; then
    export LFTP_PASSWORD="${GAME_FTP_PASSWORD}"
    lftp --env-password -u "${GAME_FTP_USER}" -p 21 "${host}" <<EOF
set cmd:fail-exit yes
set ftp:ssl-force true
set ftp:ssl-protect-data true
set ssl:verify-certificate no
pwd
bye
EOF
    return
  fi
  curl -fsS --ftp-ssl-reqd --insecure -u "${GAME_FTP_USER}:${GAME_FTP_PASSWORD}" "ftp://${host}/" >/dev/null
}

if test_ftps "${TEST_HOST}"; then
  echo "FTPS login test: OK  ftp://${TEST_HOST}/"
else
  echo "FTPS login test: FAILED  ftp://${TEST_HOST}/" >&2
  journalctl -u vsftpd -n 15 --no-pager 2>/dev/null || true
  exit 1
fi

echo ""
echo "GitHub:"
echo "  DEPLOY_FTP_USER = gameftp"
echo "  DEPLOY_FTP_PASSWORD = same password verified above"
