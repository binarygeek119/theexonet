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

## 1. Repository variable (Variables tab — not Secrets)

**Settings → Secrets and variables → Actions → Variables → New repository variable**

Copy the **name** exactly (no backticks, no spaces):

```text
ENABLE_PRODUCTION_DEPLOY
```

| Value |
|--------|
| `true` |

## 2. SSH password (Secrets tab)

**Settings → Secrets and variables → Actions → Secrets → New repository secret**

**Name** (copy exactly — underscores only, no spaces or hyphens):

```text
DEPLOY_SSH_PASSWORD
```

**Secret** (value field only): your root SSH password.

### If GitHub says “Secret names can only contain alphanumeric…”

You typed an invalid **name**. Common mistakes:

| Wrong | Use instead |
|-------|-------------|
| `DEPLOY SSH PASSWORD` (spaces) | `DEPLOY_SSH_PASSWORD` |
| `DEPLOY-SSH-PASSWORD` (hyphens) | `DEPLOY_SSH_PASSWORD` |
| `` `DEPLOY_SSH_PASSWORD` `` (backticks) | `DEPLOY_SSH_PASSWORD` |
| Password in the **name** field | Password goes in the **Secret** value field only |

Valid characters: letters, numbers, underscore `_`. Must start with a letter or `_`.

## 3. Optional repository variables (Variables tab)

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

## 4. SSH key alternative (optional)

On the server, for password login, ensure `/etc/ssh/sshd_config` has:

```text
PermitRootLogin yes
PasswordAuthentication yes
```

Then restart SSH: `sudo systemctl restart ssh`

Secret name for a key (if not using password):

```text
DEPLOY_SSH_KEY
```

### SSH key setup (recommended long-term)

On your machine:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/rava-deploy -N ""
ssh-copy-id -i ~/.ssh/rava-deploy.pub root@binarygeek119.duckdns.org
```

Add the **private** key contents as secret `DEPLOY_SSH_KEY` (PEM/OpenSSH format).

## 5. One-time server prep

See [deploy.md](deploy.md) for .NET 10, systemd units, `appsettings.json`, and Apache/nginx.

After first deploy, confirm:

```bash
curl -s http://127.0.0.1:5000/
curl -s http://127.0.0.1:6000/api/dashboard
```

## 6. Trigger a deploy

Push to `main` (changes under `server/` or the workflow file), or run **Actions → Build website → Run workflow**.

Watch the **Deploy to production** job in the Actions tab.

## 7. Troubleshooting

### `hostname contains invalid characters` during rsync

`DEPLOY_HOST` (or `DEPLOY_WWW_HOST` / `DEPLOY_API_HOST`) includes characters SSH/rsync reject — usually from copy-paste:

| Wrong | Correct |
|-------|---------|
| `https://binarygeek119.duckdns.org` | `binarygeek119.duckdns.org` |
| `binarygeek119.duckdns.org/` | `binarygeek119.duckdns.org` |
| trailing space or newline | remove in GitHub Variables UI |

The workflow now normalizes hostnames before deploy. Fix the variable in **Settings → Actions → Variables**, then re-run the workflow.

### `Add SSH host keys` fails (exit code 1)

GitHub Actions could not reach your server on `DEPLOY_SSH_PORT` (default **22**). The workflow now prints DNS and TCP checks; fix the underlying reachability issue on the server or in your GitHub variables.

**On the server** (SSH in locally or from your PC):

```bash
# sshd running and listening on all interfaces?
sudo systemctl status ssh
sudo ss -tlnp | grep ':22'

# firewall allows inbound SSH?
sudo ufw status
sudo ufw allow 22/tcp    # if ufw is active and SSH is blocked

# optional: confirm from outside (run on your PC, not the server)
ssh -p 22 root@binarygeek119.duckdns.org
```

**In GitHub → Settings → Secrets and variables → Actions → Variables:**

| Check | Correct | Wrong |
|-------|---------|-------|
| `DEPLOY_HOST` | `binarygeek119.duckdns.org` | `https://binarygeek119.duckdns.org` |
| `DEPLOY_SSH_PORT` | `22` (or your custom port) | blank, `22/tcp`, or a closed port |
| `DEPLOY_WWW_HOST` / `DEPLOY_API_HOST` | leave unset if same machine | invalid hostname |

If SSH is only reachable on your home network (no port forward), GitHub Actions **cannot** deploy until port **22** is open to the public internet or you use a self-hosted runner on the same network.

### Deploy job skipped entirely

Set repository variable **`ENABLE_PRODUCTION_DEPLOY`** = `true` (Variables tab, not Secrets).

### Auth fails after host keys succeed

Configure **`DEPLOY_SSH_PASSWORD`** or **`DEPLOY_SSH_KEY`** under Secrets. See sections 2 and 4 above.

### `Unit rava-admin.service not found` (or moderator)

The deploy synced files but the server is missing systemd units for the admin/moderator portals. On the server:

```bash
# From your repo clone on the server, or copy scripts/systemd/*.service manually
sudo bash scripts/install-systemd-units.sh
```

Ensure `/var/www/publish/appsettings.json` includes **`AdminPortal`** and **`ModeratorPortal`** sections (see [deploy.md](deploy.md)).

Until those units exist, GitHub Actions will log a **WARNING** and skip restart for missing services instead of failing the deploy.
