# GitHub Actions deploy setup (FTPS)

Production deploy uploads a zip bundle via **FTPS** to `/var/www/staging/`. The server's **staging watcher** auto-promotes to `/var/www/publish` and restarts services. **No SSH** is required in GitHub Actions.

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

GCP firewall must allow **TCP 21** and **40000-40050** from [GitHub Actions IP ranges](https://api.github.com/meta) (`actions` key), or CI FTPS uploads will fail.

## 1. Enable deploy

**Settings → Actions → Variables**

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |
| `DEPLOY_HOST` | `theexonet.com` (or `35.188.26.155`) |

Optional: `DEPLOY_FTP_HOST` if FTP hostname differs from `DEPLOY_HOST`.

Remove obsolete variables if present: `DEPLOY_USER`, `DEPLOY_SSH_PORT`, `DEPLOY_WWW_PATH`, `DEPLOY_METHOD`, etc. (no longer used).

## 2. FTPS password (required)

**Settings → Secrets**

| Name | Value |
|------|--------|
| `DEPLOY_FTP_PASSWORD` | `gameftp` password |

Set safely on the server:

```bash
sudo GAME_FTP_PASSWORD='YourPassword' bash scripts/theexonet/set-gameftp-password.sh
```

Remove unused secrets: `DEPLOY_SSH_PASSWORD`, `DEPLOY_SSH_KEY` (SSH deploy removed).

## 3. What the workflow does

**Actions → theexonet CI** on push to `main`:

1. Build and test
2. Zip `publish/` + `data/`
3. **FTPS upload** to `staging/theexonet-website-deploy-<sha>.zip`
4. Wait for `http://theexonet.com/` and API HTTP (after server auto-promote)

## 4. Manual promote (if watcher is down)

```bash
sudo promote-theexonet-staging
```

## 5. Trigger a deploy

Push to `main` under `server/` or `scripts/`, or run the workflow manually (uncheck skip deploy).
