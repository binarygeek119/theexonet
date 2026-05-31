# GitHub Actions deploy setup

Deploy runs automatically on pushes to `main` when **`ENABLE_PRODUCTION_DEPLOY`** is `true` and SSH credentials are configured.

**Never commit passwords or private keys to the repo.** Add them only in GitHub **Settings → Secrets and variables → Actions**.

## Server (binarygeek119.duckdns.org)

| Setting | Value |
|---------|--------|
| Host | `binarygeek119.duckdns.org` |
| Port | `22` |
| SSH user | `root` |
| Game static files | `/var/www/rava` |
| API + status publish | `/var/www/publish` |
| API systemd unit | `rava-api` |
| Status systemd unit | `rava-status` |

## 1. Repository variable

**Settings → Secrets and variables → Actions → Variables → New repository variable**

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |

## 2. Repository variables (optional — avoids repeating in secrets)

| Name | Value |
|------|--------|
| `DEPLOY_HOST` | `binarygeek119.duckdns.org` |
| `DEPLOY_USER` | `root` |
| `DEPLOY_SSH_PORT` | `22` |
| `DEPLOY_WWW_PATH` | `/var/www/rava` |
| `DEPLOY_API_PATH` | `/var/www/publish` |
| `DEPLOY_API_SERVICE` | `rava-api` |
| `DEPLOY_STATUS_SERVICE` | `rava-status` |

If these variables are set, you do not need the matching secrets.

## 3. SSH authentication (pick one)

### Option A — Password (quick setup)

**Settings → Secrets → New repository secret**

| Name | Value |
|------|--------|
| `DEPLOY_SSH_PASSWORD` | your root SSH password |

On the server, ensure root password login is enabled in `/etc/ssh/sshd_config`:

```text
PermitRootLogin yes
PasswordAuthentication yes
```

Then restart SSH: `sudo systemctl restart ssh`

### Option B — SSH key (recommended)

On your machine:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/rava-deploy -N ""
ssh-copy-id -i ~/.ssh/rava-deploy.pub root@binarygeek119.duckdns.org
```

Add the **private** key contents as secret `DEPLOY_SSH_KEY` (PEM/OpenSSH format).

## 4. One-time server prep

See [deploy.md](deploy.md) for .NET 10, systemd units, `appsettings.json`, and Apache/nginx.

After first deploy, confirm:

```bash
curl -s http://127.0.0.1:5000/
curl -s http://127.0.0.1:6000/api/dashboard
```

## 5. Trigger a deploy

Push to `main` (changes under `server/` or the workflow file), or run **Actions → Build website → Run workflow**.

Watch the **Deploy to production** job in the Actions tab.
