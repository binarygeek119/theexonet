# Production auto-deploy

When a build on `main` succeeds, GitHub Actions can deploy automatically:

| Target | Source | Typical path |
|--------|--------|--------------|
| Game site (port 80) | `server/Rava.Api/html/` | `/var/www/rava` |
| API (port 5000) | `dotnet publish` output | `/var/www` |

## One-time server setup

1. Create deploy user (or use existing) with SSH key access.
2. Create directories and permissions:

```bash
sudo mkdir -p /var/www/rava /var/www
sudo chown -R deploy:deploy /var/www/rava /var/www
```

   Install the **ASP.NET Core 10 runtime** (the published API targets `net10.0`; .NET 8 is not enough):

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
dotnet --list-runtimes
```

   You should see `Microsoft.AspNetCore.App 10.0.x` in the list. If the package is unavailable on your distro, use the [install script](https://learn.microsoft.com/dotnet/core/install/linux-scripted-manual#scripted-install) with `--runtime aspnetcore --channel 10.0`.

3. Copy `appsettings.json.example` to `appsettings.json` on the API host (e.g. `/var/www/appsettings.json`) and set production secrets — deploy does **not** ship or overwrite this file.

   Required files in the API folder (`/var/www`):

   | File | Source |
   |------|--------|
   | `Rava.Api.dll` + dependencies | GitHub release / `dotnet publish` |
   | `credits.json` | Included in publish output |
   | `appsettings.json` | Copy from `appsettings.json.example`, then edit |

   **Connection string:** use the same PostgreSQL host, database name, username, and password that work from your dev machine. If Postgres runs on another machine (e.g. `192.168.1.2`), do **not** use `Host=localhost` unless Postgres is installed on the API server itself. The API server must be able to reach the DB host on port 5432.

4. Optional: systemd unit for the API, e.g. `/etc/systemd/system/rava-api.service`:

```ini
[Unit]
Description=RAVA API
After=network.target

[Service]
WorkingDirectory=/var/www
ExecStart=/usr/bin/dotnet /var/www/Rava.Api.dll
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

## Troubleshooting `appsettings.json`

If the API returns **502** or `/api/status` shows **database offline**:

1. **Check the service log:** `sudo journalctl -u rava-api -n 50 --no-pager`
2. **Verify Postgres from the API server:**  
   `psql "Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD" -c "SELECT 1"`
3. **Common mistakes:**
   - `Host=localhost` when PostgreSQL is on another machine
   - Wrong database name or username (must match your real Postgres setup)
   - `appsettings.json` missing from the API folder (publish does not include it)
   - `credits.json` missing from the API folder
4. **Test locally on the server:**  
   `curl http://127.0.0.1:5000/api/status` — should return JSON with `"databaseStatus":"online"`
5. **`.NET runtime missing`:** log shows `Framework: Microsoft.NETCore.App, version '10.0.0'` but only 8.x installed — install `aspnetcore-runtime-10.0` (see step 2 above).

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
| `DEPLOY_API_PATH` | Absolute path for API publish output, e.g. `/var/www` |
| `DEPLOY_WWW_HOST` | Optional; game host if different from `DEPLOY_HOST` |
| `DEPLOY_API_HOST` | Optional; API host if different from `DEPLOY_HOST` |
| `DEPLOY_SSH_PORT` | Optional; default `22` |
| `DEPLOY_API_SERVICE` | Optional; systemd unit to restart after API deploy, e.g. `rava-api` |

Add the matching **public** key to `~/.ssh/authorized_keys` on the server.

## What deploy does

1. **html** — `rsync` from the repo to `DEPLOY_WWW_PATH` (mirrors deletes; game host only).
2. **API** — `rsync` publish artifact to `DEPLOY_API_PATH`, excluding `appsettings*.json` and `html/uploads/profiles/*`. The bundle includes an `html/` folder (game UI + avatar uploads path).
3. **Restart** — runs `sudo systemctl restart <DEPLOY_API_SERVICE>` when `DEPLOY_API_SERVICE` is set.

Deploy runs only on pushes to `main` (not pull requests), after build and test pass.

## Manual deploy on the server

```bash
# Static game site (from a git checkout)
rsync -av --delete /path/to/rava/server/Rava.Api/html/ /var/www/rava/

# API (from extracted GitHub release zip)
rsync -av --exclude 'appsettings*.json' --exclude 'html/uploads/profiles/*' \
  ./publish/ /var/www/
sudo systemctl restart rava-api
```
