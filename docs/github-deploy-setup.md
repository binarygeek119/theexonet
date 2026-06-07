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

### SSH for CI restart (recommended)

On your **PC** (PowerShell or Git Bash), generate a key used only by GitHub Actions:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/theexonet-github-deploy -C "github-actions-deploy" -N ""
```

On the **VM**, authorize the public key (one-liner from your **PC**):

```bash
ssh root@35.188.26.155 "cd /opt/theexonet/theexonet && git pull && \
  sudo DEPLOY_SSH_PUBLIC_KEY='$(cat ~/.ssh/theexonet-github-deploy.pub)' \
  bash scripts/theexonet/setup-github-ssh-restart.sh"
```

Or paste the `.pub` line manually on the VM:

```bash
sudo DEPLOY_SSH_PUBLIC_KEY='ssh-ed25519 AAAA...' bash scripts/theexonet/setup-github-ssh-restart.sh
```

Test from your PC:

```bash
ssh -i ~/.ssh/theexonet-github-deploy root@35.188.26.155 'restart-theexonet'
```

**Personal SSH** (manual restarts): your normal `~/.ssh/id_ed25519` key is added automatically if you run the setup script from a machine that has it. Or re-run `create-gcp-vm.sh` to refresh VM metadata.

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
| `DEPLOY_SSH_KEY` | Private key for `theexonet-github-deploy` (full PEM, including `BEGIN`/`END` lines) |

Set FTPS password safely on the server:

```bash
sudo GAME_FTP_PASSWORD='YourPassword' bash scripts/theexonet/set-gameftp-password.sh
```

Remove unused secrets: `DEPLOY_SSH_PASSWORD` (password SSH not used).

## 3. What the workflow does

**Actions → theexonet CI** on push to `main`:

1. Build and test
2. Zip `publish/` + `data/`
3. **FTPS upload** to `staging/theexonet-website-deploy-<sha>.zip`
4. **SSH** `promote-theexonet-staging` (if `DEPLOY_SSH_KEY` is set; otherwise staging-watcher promotes within ~30s)
5. Wait for `http://theexonet.com/` and API HTTP

## 4. Manual promote (if watcher is down)

```bash
sudo promote-theexonet-staging
```

## 5. Trigger a deploy

Push to `main` under `server/` or `scripts/`, or run the workflow manually (uncheck skip deploy).
