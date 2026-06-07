#!/bin/bash
# First-boot setup for theexonet GCP VM: Apache2, Docker, Docker Compose plugin.
# Runs as root via GCE metadata startup-script (Ubuntu 22.04/24.04 LTS).
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

log() {
  echo "[theexonet-startup] $*"
}

log "Updating packages…"
apt-get update -y
apt-get upgrade -y

log "Installing Apache2…"
apt-get install -y apache2
systemctl enable apache2
systemctl start apache2

if [ ! -f /var/www/html/index.html ] || ! grep -q "theexonet.com" /var/www/html/index.html 2>/dev/null; then
  cat >/var/www/html/index.html <<'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>The Exonet</title>
</head>
<body>
  <h1>The Exonet</h1>
  <p>Host is online. Point services at Docker or Apache as needed.</p>
</body>
</html>
EOF
fi

log "Installing Docker Engine and Compose plugin…"
apt-get install -y ca-certificates curl gnupg
install -m 0755 -d /etc/apt/keyrings
if [ ! -f /etc/apt/keyrings/docker.gpg ]; then
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
fi

source /etc/os-release
arch="$(dpkg --print-architecture)"
if [ ! -f /etc/apt/sources.list.d/docker.list ]; then
  echo "deb [arch=${arch} signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
    >/etc/apt/sources.list.d/docker.list
fi

apt-get update -y
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

systemctl enable docker
systemctl start docker

log "Allowing root and default SSH user in docker group (if present)…"
usermod -aG docker root 2>/dev/null || true
for u in ubuntu debian; do
  if id "$u" >/dev/null 2>&1; then
    usermod -aG docker "$u" || true
  fi
done

log "Ensuring PermitRootLogin for key-based root SSH…"
mkdir -p /etc/ssh/sshd_config.d
cat >/etc/ssh/sshd_config.d/99-theexonet-root.conf <<'EOF'
PermitRootLogin prohibit-password
PasswordAuthentication no
EOF
systemctl reload ssh || systemctl reload sshd || true

log "Done. Apache: $(systemctl is-active apache2). Docker: $(systemctl is-active docker)."
docker compose version >/dev/null 2>&1 && docker compose version || true
