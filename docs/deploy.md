# Production auto-deploy

When a build on `main` succeeds, GitHub Actions can deploy automatically:

| Target | Source | Typical path |
|--------|--------|--------------|
| Game site (port 80) | `server/Rava.Api/www/` | `/var/www/rava` |
| API (port 5000) | `dotnet publish` output | `/opt/rava-api` |

## One-time server setup

1. Create deploy user (or use existing) with SSH key access.
2. Create directories and permissions:

```bash
sudo mkdir -p /var/www/rava /opt/rava-api
sudo chown -R deploy:deploy /var/www/rava /opt/rava-api
```

3. Put production `appsettings.json` (and email/DB settings) on the API host at `/opt/rava-api/appsettings.json` — it is **not** overwritten by deploy.
4. Optional: systemd unit for the API, e.g. `/etc/systemd/system/rava-api.service`:

```ini
[Unit]
Description=RAVA API
After=network.target

[Service]
WorkingDirectory=/opt/rava-api
ExecStart=/usr/bin/dotnet /opt/rava-api/Rava.Api.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=DOTNET_ENVIRONMENT=Production
User=deploy

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-api
```

Allow the deploy user to restart the service without a password (adjust user and unit name):

```bash
echo 'deploy ALL=(ALL) NOPASSWD: /bin/systemctl restart rava-api' | sudo tee /etc/sudoers.d/rava-deploy
sudo chmod 440 /etc/sudoers.d/rava-deploy
```

5. Configure your reverse proxy (HTTPS → port 80 for game, HTTPS → port 5000 for API).

## GitHub configuration

In the repo: **Settings → Secrets and variables → Actions**.

### Repository variable

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |

Leave unset (or not `true`) until secrets below are configured.

### Secrets

| Secret | Description |
|--------|-------------|
| `DEPLOY_SSH_KEY` | Private SSH key (PEM) for the deploy user |
| `DEPLOY_USER` | SSH username |
| `DEPLOY_HOST` | Server hostname or IP (used when host-specific secrets are omitted) |
| `DEPLOY_WWW_PATH` | Absolute path for static game files, e.g. `/var/www/rava` |
| `DEPLOY_API_PATH` | Absolute path for API publish output, e.g. `/opt/rava-api` |
| `DEPLOY_WWW_HOST` | Optional; game host if different from `DEPLOY_HOST` |
| `DEPLOY_API_HOST` | Optional; API host if different from `DEPLOY_HOST` |
| `DEPLOY_SSH_PORT` | Optional; default `22` |
| `DEPLOY_API_SERVICE` | Optional; systemd unit to restart after API deploy, e.g. `rava-api` |

Add the matching **public** key to `~/.ssh/authorized_keys` on the server.

## What deploy does

1. **www** — `rsync` from the repo to `DEPLOY_WWW_PATH` (mirrors deletes; game host only).
2. **API** — `rsync` publish artifact to `DEPLOY_API_PATH`, excluding `appsettings*.json` and `www/uploads/profiles/*`.
3. **Restart** — runs `sudo systemctl restart <DEPLOY_API_SERVICE>` when `DEPLOY_API_SERVICE` is set.

Deploy runs only on pushes to `main` (not pull requests), after build and test pass.

## Manual deploy on the server

```bash
# Static game site (from a git checkout)
rsync -av --delete /path/to/rava/server/Rava.Api/www/ /var/www/rava/

# API (from extracted GitHub release zip)
rsync -av --exclude 'appsettings*.json' --exclude 'www/uploads/profiles/*' \
  ./publish/ /opt/rava-api/
sudo systemctl restart rava-api
```
