# theexonet.com — Ubuntu host server setup

One-shot installer for **Ubuntu 22.04 / 24.04 LTS** (GCP VM or any VPS): Apache2, Docker, PostgreSQL, FTPS deploy user, `theexonet` service account, and subdomain routing.

## What gets installed

| Component | Details |
|-----------|---------|
| **Apache2** | `theexonet.com` + `www` → game static files; subdomains proxy to backends |
| **Docker** | Engine + Compose plugin |
| **PostgreSQL 16** | Container `theexonet-postgres`, bound to `127.0.0.1:5432` |
| **ASP.NET 10** | Runtime for published DLLs |
| **Users** | `theexonet` runs services; `gameftp` uploads to staging (FTPS) |
| **Paths** | `/var/www/publish` (live), `/var/www/data` (config + CSVs), `/var/www/staging` (FTP uploads) |
| **systemd** | `theexonet-api`, `theexonet-status`, `theexonet-admin`, `theexonet-moderator`, `theexonet-docs` |

## Subdomains

| Host | Backend |
|------|---------|
| `theexonet.com`, `www.theexonet.com` | `/var/www/publish/html` (game UI) |
| `api.theexonet.com` | `127.0.0.1:5000` |
| `status.theexonet.com` | `127.0.0.1:6000` |
| `admin.theexonet.com` | `127.0.0.1:7000` |
| `moderator.theexonet.com` | `127.0.0.1:7050` |
| `docs.theexonet.com` | `127.0.0.1:9000` |

## 1. Create the VM (GCP)

See [theexonet-gcp.md](theexonet-gcp.md) for `create-gcp-vm.sh` and SSH key setup.

## 2. Run the host installer

SSH as root, clone the repo (or copy scripts), then:

```bash
git clone --depth 1 https://github.com/binarygeek119/theexonet.git
cd theexonet
sudo bash scripts/theexonet/install-host-server.sh
```

Optional environment:

```bash
export THEEXONET_DOMAIN=theexonet.com
export POSTGRES_PASSWORD='your-strong-db-password'
export GAME_FTP_PASSWORD='your-ftp-password'
export SKIP_FTP=1                    # skip FTPS user (manual SFTP only)
sudo -E bash scripts/theexonet/install-host-server.sh
```

Credentials are written to `/etc/theexonet/install-credentials.txt` (root-only).

## 3. DNS

Point **A records** at the server public IP:

- `@`, `www`, `api`, `status`, `admin`, `moderator`, `docs`

## 4. HTTPS

After DNS resolves:

```bash
sudo apt install -y certbot python3-certbot-apache
sudo certbot --apache -d theexonet.com -d www.theexonet.com \
  -d api.theexonet.com -d status.theexonet.com -d admin.theexonet.com \
  -d moderator.theexonet.com -d docs.theexonet.com
```

## 5. Deploy game files

### Option A — GitHub Actions (recommended, FTPS)

See [github-deploy-setup.md](github-deploy-setup.md). CI uploads a zip to `staging/` via **FTPS**; `theexonet-staging-watcher.service` auto-promotes to `/var/www/publish`.

Install the watcher once:

```bash
sudo bash scripts/install-staging-watcher.sh
```

Open firewall **TCP 21** and **40000–40050** for [GitHub Actions IPs](https://api.github.com/meta) (and your IP for manual FTP).

### Option B — Manual FTPS (FileZilla)

1. **FTP over explicit TLS**, host = server IP or domain, user `gameftp`.
2. FTPS chroot is `/var/www` — upload `theexonet-website-*.zip` into `staging/`.
3. Watcher promotes automatically, or run `sudo promote-theexonet-staging`.

### Option C — SFTP (SSH key)

```bash
sftp -i ~/.ssh/id_ed25519 root@YOUR_SERVER
put theexonet-website-*.zip /var/www/staging/
```

Then `sudo promote-theexonet-staging`.

## Troubleshooting: ASP.NET 10 on Ubuntu 22.04

Microsoft's apt feed for **jammy** does not include .NET 10. The installer uses Ubuntu's `ppa:dotnet/backports`. If install failed at the runtime step, run either:

```bash
# Option A — Ubuntu backports PPA (recommended for apt-managed servers)
sudo apt-get install -y software-properties-common
sudo add-apt-repository -y ppa:dotnet/backports
sudo apt-get update -y
sudo apt-get install -y aspnetcore-runtime-10.0
dotnet --list-runtimes | grep AspNetCore
```

```bash
# Option B — Microsoft install script (always works)
wget -O /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
sudo /tmp/dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
dotnet --list-runtimes | grep AspNetCore
```

Then re-run: `sudo bash scripts/theexonet/install-host-server.sh` (completed steps are skipped).

## 6. Verify

```bash
sudo diagnose-theexonet-api
sudo systemctl status theexonet-api theexonet-postgres
curl -sI http://127.0.0.1:5000/api/status
curl -sI -H 'Host: theexonet.com' http://127.0.0.1/
```

## Folder permissions

- **`theexonet`** owns `/var/www/publish` and `/var/www/data` (systemd `User=theexonet`).
- **`gameftp`** FTPS root is `/var/www` (chroot); uploads go in `staging/` (group `theexonet`, mode `2775`).
- **`www-data`** is in group `theexonet` so Apache can read game static files.

Fix permissions anytime:

```bash
sudo fix-theexonet-permissions
```

## Re-run parts only

| Task | Command |
|------|---------|
| Apache + Docker only | `sudo bash scripts/theexonet/startup-install.sh` |
| Game users | `sudo bash scripts/theexonet/setup-game-user.sh` |
| FTPS only | `sudo bash scripts/theexonet/install-ftp-server.sh` |
| systemd + helpers | `sudo install-theexonet-scripts && sudo install-theexonet-systemd` |
| Restart stack | `sudo restart-theexonet` |
