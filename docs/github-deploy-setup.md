# GitHub Actions deploy setup (FTPS)

Production deploy uploads a zip bundle via **FTPS** to `/var/www/staging/`, then **SSH** runs `promote-theexonet-staging` (restart all services). The **staging watcher** is a backup if SSH is not configured.

**Never commit passwords to the repo.** Add them only in GitHub **Settings → Secrets and variables → Actions**.

## Server (theexonet.com / GCP VM)

| Setting | Value |
|---------|--------|
| FTPS host | `theexonet.com` or VM IP |
| FTPS user | `gameftp` |
| FTPS chroot | `/var/www` (upload to `staging/`) |
| Live publish | `/var/www/publish` |
| Auto-promote | `theexonet-staging-watcher.service` |

### One-time on the server

```bash
cd /opt/theexonet/theexonet
git pull
sudo bash scripts/theexonet/install-ftp-server.sh
sudo bash scripts/install-staging-watcher.sh
sudo systemctl status theexonet-staging-watcher
```

### SSH password for CI restart (recommended)

Pick a **strong password** used only for deploy (not your personal login). On the **VM** as root:

```bash
cd /opt/theexonet/theexonet && git pull
sudo DEPLOY_SSH_PASSWORD='YourStrongDeployPassword' bash scripts/theexonet/setup-github-ssh-restart.sh
```

This sets the `root` password and enables password SSH for promote/restart.

Test from your PC (with `sshpass` installed, or use PuTTY/plink):

```bash
SSHPASS='YourStrongDeployPassword' sshpass -e ssh -o PubkeyAuthentication=no root@35.188.26.155 'restart-theexonet'
```

**Personal SSH** with keys still works if you use `create-gcp-vm.sh` metadata keys; password login is added for CI.

GCP firewall must allow **TCP 22** (SSH), **TCP 21**, and **40000-40050** (FTPS passive) from [GitHub Actions IP ranges](https://api.github.com/meta) (`actions` key), or CI deploy will fail.

## 1. Enable deploy

**Settings → Actions → Variables**

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |
| `DEPLOY_HOST` | `theexonet.com` (or `35.188.26.155`) |

Optional: `DEPLOY_FTP_HOST` if FTP hostname differs from `DEPLOY_HOST`.

Optional: `DEPLOY_USER` (default `root`), `DEPLOY_SSH_PORT` (default `22`).

Remove obsolete variables if present: `DEPLOY_WWW_PATH`, `DEPLOY_API_PATH`, `DEPLOY_METHOD`, etc.

## 2. FTPS password (required)

**Settings → Secrets**

| Name | Value |
|------|--------|
| `DEPLOY_FTP_PASSWORD` | `gameftp` password |
| `DEPLOY_SSH_PASSWORD` | Root (or `DEPLOY_USER`) SSH password — same value passed to `setup-github-ssh-restart.sh` |

Set FTPS password safely on the server:

```bash
sudo GAME_FTP_PASSWORD='YourPassword' bash scripts/theexonet/set-gameftp-password.sh
```

Remove unused secrets: `DEPLOY_SSH_KEY` (key-based SSH not used).

## 3. What the workflow does

**Actions → theexonet CI** on push to `main`:

1. Build and test
2. Zip `publish/` + `data/`
3. **FTPS upload** to `staging/theexonet-website-deploy-<sha>.zip`
4. **SSH** `promote-theexonet-staging` (if `DEPLOY_SSH_PASSWORD` is set; otherwise staging-watcher promotes within ~30s)
5. Wait for `http://theexonet.com/` and API HTTP

## 4. Manual promote (if watcher is down)

```bash
sudo promote-theexonet-staging
```

## 5. Trigger a deploy

Push to `main` under `server/` or `scripts/`, or run the workflow manually (uncheck skip deploy).
