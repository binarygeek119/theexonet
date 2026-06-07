#!/bin/bash
# Install vsftpd on Ubuntu for game file uploads (theexonet VM).
# Run on the VM as root: sudo bash install-ftp-server.sh
#
# Env (optional):
#   GAME_FTP_USER=gameftp
#   GAME_FTP_PASSWORD=your-strong-password   # if unset, you will be prompted
#   GAME_FTP_ROOT=/var/www                   # FTPS chroot (upload to staging/ or publish/)
#   GAME_FTP_PASV_MIN=40000
#   GAME_FTP_PASV_MAX=40050
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=wait-for-apt-lock.sh
source "${SCRIPT_DIR}/wait-for-apt-lock.sh"

GAME_FTP_USER="${GAME_FTP_USER:-gameftp}"
GAME_FTP_ROOT="${GAME_FTP_ROOT:-/var/www}"
GAME_FTP_STAGING="${GAME_FTP_STAGING:-${GAME_FTP_ROOT}/staging}"
PASV_MIN="${GAME_FTP_PASV_MIN:-40000}"
PASV_MAX="${GAME_FTP_PASV_MAX:-40050}"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [ -z "${GAME_FTP_PASSWORD:-}" ]; then
  read -r -s -p "Password for FTP user ${GAME_FTP_USER}: " GAME_FTP_PASSWORD
  echo
  if [ -z "${GAME_FTP_PASSWORD}" ]; then
    echo "ERROR: Password cannot be empty." >&2
    exit 1
  fi
fi

echo "Installing vsftpd…"
wait_for_apt_lock
apt-get update -y
apt-get install -y vsftpd openssl

GAME_GROUP="${THEEXONET_SERVICE_GROUP:-theexonet}"
if ! getent group "${GAME_GROUP}" >/dev/null; then
  groupadd --system "${GAME_GROUP}"
fi

mkdir -p "${GAME_FTP_ROOT}" "${GAME_FTP_STAGING}"
# Keep /var/www owned by root; writable game paths use group theexonet.
chmod 755 "${GAME_FTP_ROOT}"
chown root:"${GAME_GROUP}" "${GAME_FTP_ROOT}"
chmod 2775 "${GAME_FTP_STAGING}"
chown "${GAME_FTP_USER}:${GAME_GROUP}" "${GAME_FTP_STAGING}"
for sub in publish data; do
  if [ -d "${GAME_FTP_ROOT}/${sub}" ]; then
    chown "${THEEXONET_SERVICE_USER:-theexonet}:${GAME_GROUP}" "${GAME_FTP_ROOT}/${sub}" 2>/dev/null || true
    chmod 2775 "${GAME_FTP_ROOT}/${sub}" 2>/dev/null || true
  fi
done

if ! id "${GAME_FTP_USER}" >/dev/null 2>&1; then
  useradd -g "${GAME_GROUP}" -d "${GAME_FTP_ROOT}" -s /usr/sbin/nologin "${GAME_FTP_USER}"
else
  usermod -d "${GAME_FTP_ROOT}" "${GAME_FTP_USER}" 2>/dev/null || true
fi
usermod -aG "${GAME_GROUP}" "${GAME_FTP_USER}" 2>/dev/null || true
echo "${GAME_FTP_USER}:${GAME_FTP_PASSWORD}" | chpasswd

# Self-signed cert for explicit FTPS (recommended over plain FTP).
if [ ! -f /etc/ssl/private/vsftpd.pem ]; then
  openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
    -keyout /etc/ssl/private/vsftpd.pem \
    -out /etc/ssl/private/vsftpd.pem \
    -subj "/CN=theexonet-ftp"
  chmod 600 /etc/ssl/private/vsftpd.pem
fi

cp -a /etc/vsftpd.conf "/etc/vsftpd.conf.bak.$(date +%Y%m%d%H%M%S)" 2>/dev/null || true

# GCP/NAT: passive mode must advertise the public IP, not the internal 10.x address.
EXTERNAL_IP="$(curl -fsS -H Metadata-Flavor:Google \
  http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip \
  2>/dev/null || true)"
if [ -z "${EXTERNAL_IP}" ]; then
  EXTERNAL_IP="$(hostname -I | awk '{print $1}')"
fi
PASV_ADDRESS_LINE=""
if [ -n "${EXTERNAL_IP}" ]; then
  PASV_ADDRESS_LINE="pasv_addr_resolve=NO
pasv_address=${EXTERNAL_IP}"
fi

cat >/etc/vsftpd.conf <<EOF
listen=YES
listen_ipv6=NO
anonymous_enable=NO
local_enable=YES
write_enable=YES
local_umask=022
dirmessage_enable=YES
use_localtime=YES
xferlog_enable=YES
connect_from_port_20=YES
chroot_local_user=YES
allow_writeable_chroot=YES
check_shell=NO
secure_chroot_dir=/var/run/vsftpd/empty
pam_service_name=vsftpd
rsa_cert_file=/etc/ssl/private/vsftpd.pem
rsa_private_key_file=/etc/ssl/private/vsftpd.pem
ssl_enable=YES
allow_anon_ssl=NO
force_local_data_ssl=YES
force_local_logins_ssl=YES
ssl_tlsv1=NO
ssl_sslv2=NO
ssl_sslv3=NO
require_ssl_reuse=NO
pasv_enable=YES
pasv_min_port=${PASV_MIN}
pasv_max_port=${PASV_MAX}
${PASV_ADDRESS_LINE}
userlist_enable=YES
userlist_file=/etc/vsftpd.userlist
userlist_deny=NO
local_root=${GAME_FTP_ROOT}
EOF

echo "${GAME_FTP_USER}" >/etc/vsftpd.userlist

# Ubuntu PAM: drop pam_shells (FTP-only users use /usr/sbin/nologin).
if [ -f /etc/pam.d/vsftpd ] && grep -q 'pam_shells.so' /etc/pam.d/vsftpd; then
  cp -a /etc/pam.d/vsftpd "/etc/pam.d/vsftpd.bak.$(date +%Y%m%d%H%M%S)"
  sed -i '/pam_shells\.so/d' /etc/pam.d/vsftpd
fi
# Remove from legacy deny list if present.
if [ -f /etc/ftpusers ] && grep -qx "${GAME_FTP_USER}" /etc/ftpusers; then
  sed -i "/^${GAME_FTP_USER}\$/d" /etc/ftpusers
fi

systemctl enable vsftpd
systemctl restart vsftpd

# Quick local auth check (does not verify passive mode / firewall).
if command -v curl >/dev/null 2>&1; then
  if curl -fsS --ftp-ssl-reqd --insecure -u "${GAME_FTP_USER}:${GAME_FTP_PASSWORD}" "ftp://127.0.0.1/" >/dev/null 2>&1; then
    echo "Local FTPS login test: OK"
  else
    echo "WARN: Local FTPS login test failed — check journalctl -u vsftpd" >&2
  fi
fi

cat <<EOF

=== FTP server ready (FTPS) ===

User:     ${GAME_FTP_USER}
Root:     ${GAME_FTP_ROOT}  (chroot — upload to staging/ then promote)
Staging:  ${GAME_FTP_STAGING}
Host:     ${EXTERNAL_IP}  (or theexonet.com)
Port:     21 (control), passive ${PASV_MIN}-${PASV_MAX}

FileZilla / WinSCP:
  Protocol:  FTP over TLS (explicit FTPS) — or SFTP if you prefer SSH keys
  Host:      ${EXTERNAL_IP}
  User:      ${GAME_FTP_USER}
  Password:  (what you set)
  Encryption: Require explicit FTP over TLS
  Change password: sudo GAME_FTP_PASSWORD='...' bash scripts/theexonet/set-gameftp-password.sh
  (Do not use passwd — use set-gameftp-password.sh; update FileZilla Site Manager after change.)
  Passive IP: ${EXTERNAL_IP} (required on GCP — pasv_address in vsftpd.conf)

GCP firewall (run from Cloud Shell or WSL with gcloud):
  # Your IP (manual FileZilla):
  gcloud compute firewall-rules create theexonet-ftp \\
    --direction=INGRESS --action=ALLOW \\
    --rules=tcp:21,tcp:${PASV_MIN}-${PASV_MAX} \\
    --source-ranges=YOUR.HOME.IP/32 \\
    --target-tags=theexonet-web
  # GitHub Actions FTPS deploy — allow https://api.github.com/meta "actions" CIDRs too.

After upload, staging-watcher or: sudo promote-theexonet-staging

Safer alternative (no extra ports): SFTP over SSH with your existing key:
  sftp -i ~/.ssh/id_ed25519 root@${EXTERNAL_IP}
  put localfile ${GAME_FTP_ROOT}/

Apache can serve static game files from ${GAME_FTP_ROOT} if you point a vhost or symlink:
  ln -sfn ${GAME_FTP_ROOT} /var/www/html/game

Status: $(systemctl is-active vsftpd)

EOF
