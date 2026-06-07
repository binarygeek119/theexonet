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

### SSH deploy user for CI restart (recommended)

Creates user **`githubdeploy`** with password SSH and sudo only for promote/restart/fix-permissions (not full root).

On the **VM** as root:

```bash
cd /opt/theexonet/theexonet
git -c safe.directory=/opt/theexonet/theexonet pull
sudo DEPLOY_SSH_PASSWORD='YourStrongDeployPassword' bash scripts/theexonet/setup-github-ssh-restart.sh
```

Test from your PC:

```bash
SSHPASS='YourStrongDeployPassword' sshpass -e ssh -o PubkeyAuthentication=no githubdeploy@35.188.26.155 'sudo restart-theexonet'
```

**Personal admin SSH** stays key-only on `root` (GCP metadata). Only `githubdeploy` may use password login.

GCP firewall must allow **TCP 22** (SSH), **TCP 21**, and **40000-40050** (FTPS passive) from [GitHub Actions IP ranges](https://api.github.com/meta) (`actions` key), or CI deploy will fail.

## 1. Enable deploy

**Settings → Actions → Variables**

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |
| `DEPLOY_HOST` | `theexonet.com` (or `35.188.26.155`) |
| `DEPLOY_USER` | `githubdeploy` |

Optional: `DEPLOY_FTP_HOST` if FTP hostname differs from `DEPLOY_HOST`. Optional: `DEPLOY_SSH_PORT` (default `22`).

Remove obsolete variables if present: `DEPLOY_WWW_PATH`, `DEPLOY_API_PATH`, `DEPLOY_METHOD`, etc.

## 2. FTPS password (required)

**Settings → Secrets**

| Name | Value |
|------|--------|
| `DEPLOY_FTP_PASSWORD` | `gameftp` password |
| `DEPLOY_SSH_PASSWORD` | `githubdeploy` password — same value passed to `setup-github-ssh-restart.sh` |

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

## 6. Troubleshooting

### `530 Login incorrect` (FTPS)

Wrong `DEPLOY_FTP_PASSWORD`, wrong `DEPLOY_FTP_USER`, or `gameftp` not installed on the server.

1. On the VM, reset the password and verify local login:

```bash
sudo GAME_FTP_PASSWORD='YourNewFtpPassword' bash scripts/theexonet/set-gameftp-password.sh
```

2. GitHub → **Secrets** → update `DEPLOY_FTP_PASSWORD` to the **exact same** value (no extra spaces; password must not contain `:`).

3. GitHub → **Variables** → set `DEPLOY_HOST` to `theexonet.com` or `35.188.26.155` (not `binarygeek119.duckdns.org` unless that DNS still points at this VM).

4. Optional variable `DEPLOY_FTP_USER` = `gameftp` (default).

5. Re-run the workflow.

### `DEPLOY_SSH_USER: root` in logs

Set GitHub variable `DEPLOY_USER` = `githubdeploy` (overrides the old `root` value).
