#!/bin/bash
# Set gameftp password safely and verify shadow + FTPS login.
#
#   sudo GAME_FTP_PASSWORD='YourNewPass' bash scripts/theexonet/set-gameftp-password.sh
#   sudo bash scripts/theexonet/set-gameftp-password.sh   # prompts
set -euo pipefail

GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"

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

if printf '%s' "${GAME_FTP_PASSWORD}" | grep -q ':'; then
  echo "ERROR: password cannot contain ':' (use letters, numbers, and !@#%^&*-_)." >&2
  exit 1
fi

fix_vsftpd_pam() {
  mkdir -p /etc/ssh/sshd_config.d
  cat >/etc/pam.d/vsftpd <<'EOF'
# theexonet gameftp — /usr/sbin/nologin is OK (pam_shells removed).
auth    required    pam_listfile.so item=user sense=deny file=/etc/ftpusers onerr=succeed
auth    required    pam_unix.so
account required    pam_unix.so
session required    pam_loginuid.so
EOF
  if [ -f /etc/vsftpd.conf ]; then
    grep -q '^check_shell=NO' /etc/vsftpd.conf || echo 'check_shell=NO' >>/etc/vsftpd.conf
    grep -q '^local_enable=YES' /etc/vsftpd.conf || echo 'local_enable=YES' >>/etc/vsftpd.conf
  fi
  if [ -f /etc/ftpusers ] && grep -qx "${GAME_FTP_USER}" /etc/ftpusers; then
    sed -i "/^${GAME_FTP_USER}\$/d" /etc/ftpusers
  fi
  echo "${GAME_FTP_USER}" >/etc/vsftpd.userlist
  passwd -u "${GAME_FTP_USER}" 2>/dev/null || true
  usermod -s /usr/sbin/nologin "${GAME_FTP_USER}" 2>/dev/null || true
}

verify_shadow_password() {
  python3 - "${GAME_FTP_USER}" "${GAME_FTP_PASSWORD}" <<'PY'
import crypt
import spwd
import sys

user, password = sys.argv[1], sys.argv[2]
rec = spwd.getspnam(user)
hash_value = rec.sp_pwdp
if not hash_value or hash_value.startswith("!"):
    raise SystemExit(f"shadow: {user} is locked or has no password")
if crypt.crypt(password, hash_value) != hash_value:
    raise SystemExit(f"shadow: password does not match /etc/shadow for {user}")
print(f"shadow: password OK for {user}")
PY
}

wait_for_port_21() {
  local attempt=1
  while [ "${attempt}" -le 10 ]; do
    if ss -tln 2>/dev/null | grep -q ':21 '; then
      return 0
    fi
    sleep 1
    attempt=$((attempt + 1))
  done
  return 1
}

test_ftps_login() {
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

fix_vsftpd_pam

printf '%s:%s\n' "${GAME_FTP_USER}" "${GAME_FTP_PASSWORD}" | chpasswd
verify_shadow_password

systemctl restart vsftpd
if ! wait_for_port_21; then
  echo "ERROR: vsftpd is not listening on port 21." >&2
  systemctl status vsftpd --no-pager || true
  journalctl -u vsftpd -n 30 --no-pager || true
  exit 1
fi

if test_ftps_login "127.0.0.1"; then
  echo "Local FTPS login test: OK (127.0.0.1)"
else
  echo "ERROR: shadow OK but local FTPS login failed — check vsftpd SSL settings." >&2
  journalctl -u vsftpd -n 20 --no-pager || true
  exit 1
fi

echo "Password updated for ${GAME_FTP_USER}."
echo "Update GitHub secret DEPLOY_FTP_PASSWORD to this exact password."
echo "Test remotely: sudo GAME_FTP_PASSWORD='***' bash scripts/theexonet/diagnose-ftps.sh theexonet.com"
