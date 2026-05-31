# Production auto-deploy

When a build on `main` succeeds, GitHub Actions can deploy automatically:

| Target | Source | Typical path |
|--------|--------|--------------|
| Game site (port 80) | `server/Rava.Api/html/` | `/var/www/rava` |
| API + status (ports 5000 / 6000) | `dotnet publish` output | `/var/www/publish` |

## One-time server setup

1. Ensure SSH access for GitHub Actions or manual deploy (often your normal login user or `root` — this is **not** required to be the same account that runs the API).
2. Create directories and permissions:

```bash
sudo mkdir -p /var/www/rava /var/www/publish
sudo mkdir -p /var/www/publish/html/images/profile
# Use the same user as User= in your systemd units (www-data is typical with Apache):
sudo chown -R www-data:www-data /var/www/rava /var/www/publish
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

3. Copy `appsettings.json.example` to `appsettings.json` on the API host (e.g. `/var/www/publish/appsettings.json`) and set production secrets — deploy does **not** ship or overwrite this file.

   Required files in `/var/www/publish`:

   | File | Source |
   |------|--------|
   | `Rava.Api.dll` + dependencies | GitHub release / `dotnet publish` |
   | `Rava.Status.dll` + `wwwroot/` | Same publish bundle |
   | `credits.json` | Included in publish output |
   | `appsettings.json` | Copy from `appsettings.json.example`, then edit |

   Add a **`StatusMonitor`** section to the same `appsettings.json` for the status dashboard (port 6000). Do **not** add a top-level `"Urls"` key — each systemd service sets its own port via `ASPNETCORE_URLS`.

   Set **`Hosting:ServeGameUi`** to **`false`** (default in `appsettings.json.example`) so the API subdomain shows a status page at `/` instead of the game UI. The game is served from the game host (`/var/www/rava`), not from the API.

   **Connection string:** use the same PostgreSQL host, database name, username, and password that work from your dev machine. If Postgres runs on another machine (e.g. `192.168.1.2`), do **not** use `Host=localhost` unless Postgres is installed on the API server itself. The API server must be able to reach the DB host on port 5432.

   **Upload folder permissions** — the API creates `html/images/profile/` at startup. The systemd service user must own (or be able to write to) `/var/www/publish`:

```bash
sudo mkdir -p /var/www/publish/html/images/profile
sudo chown -R www-data:www-data /var/www/publish
```

   Use the same username as `User=` in your systemd units (`www-data`, your login user, or omit `User=` to run as root — not recommended for production).

   The API resolves paths from the folder containing `Rava.Api.dll`. With `WorkingDirectory=/var/www/publish`, uploads are written to **`/var/www/publish/html/images/profile/`** (URL path `/images/profile/...`).

4. Optional: systemd unit for the API, e.g. `/etc/systemd/system/rava-api.service`:

```ini
[Unit]
Description=RAVA API
After=network.target

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Rava.Api.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-api
```

Optional status dashboard on port **6000** — uses the same `/var/www/publish` folder as the API:

Add to `/var/www/publish/appsettings.json`:

```json
"StatusMonitor": {
  "ApiBaseUrl": "http://127.0.0.1:5000",
  "StatusPublicUrl": "https://ravastatus.binarygeek119.duckdns.org/"
}
```

`/etc/systemd/system/rava-status.service`:

```ini
[Unit]
Description=RAVA Status Dashboard
After=network.target rava-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Rava.Status.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:6000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-status
```

Allow passwordless service restarts for GitHub Actions (optional — use your SSH login user, not necessarily `www-data`):

```bash
echo 'YOUR_SSH_USER ALL=(ALL) NOPASSWD: /bin/systemctl restart rava-api, /bin/systemctl restart rava-status' | sudo tee /etc/sudoers.d/rava-deploy
sudo chmod 440 /etc/sudoers.d/rava-deploy
```

5. Configure your reverse proxy:

| Host | Backend |
|------|---------|
| `rava.binarygeek119.duckdns.org` | port 80 (game static site) |
| `ravaapi.binarygeek119.duckdns.org` | port 5000 (`Rava.Api`) |
| `ravastatus.binarygeek119.duckdns.org` | port 6000 (`Rava.Status`) |

Example nginx server block for the status dashboard:

```nginx
server {
    listen 443 ssl;
    server_name ravastatus.binarygeek119.duckdns.org;

    location / {
        proxy_pass http://127.0.0.1:6000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

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
6. **`Address already in use` / socket bind error:** another process holds port 5000 or 6000 (`sudo ss -tlnp | grep -E '5000|6000'`). Do **not** put `"Urls"` in the shared `/var/www/publish/appsettings.json` — set ports only in each systemd unit (`ASPNETCORE_URLS=http://0.0.0.0:5000` for API, `:6000` for status).
7. **`Access to the path .../html/images/profile is denied`:** fix ownership on `/var/www/publish` (see upload folder permissions in step 3 above).

## GitHub configuration

See **[github-deploy-setup.md](github-deploy-setup.md)** for step-by-step setup on `binarygeek119.duckdns.org` (root SSH, password or key auth).

In the repo: **Settings → Secrets and variables → Actions**.

### Repository variable

| Name | Value |
|------|--------|
| `ENABLE_PRODUCTION_DEPLOY` | `true` |

Leave unset (or not `true`) until secrets below are configured.

### Secrets

| Secret | Description |
|--------|-------------|
| `DEPLOY_SSH_KEY` | Private SSH key (PEM) for SSH/rsync — **or** use `DEPLOY_SSH_PASSWORD` instead |
| `DEPLOY_SSH_PASSWORD` | Root/login SSH password (used with `sshpass` when no key is configured) |
| `DEPLOY_USER` | SSH username for rsync (default in workflow: `root`) |
| `DEPLOY_HOST` | Server hostname or IP (used when host-specific secrets are omitted) |
| `DEPLOY_WWW_PATH` | Absolute path for static game files, e.g. `/var/www/rava` |
| `DEPLOY_API_PATH` | Absolute path for API publish output, e.g. `/var/www/publish` |
| `DEPLOY_WWW_HOST` | Optional; game host if different from `DEPLOY_HOST` |
| `DEPLOY_API_HOST` | Optional; API host if different from `DEPLOY_HOST` |
| `DEPLOY_SSH_PORT` | Optional; default `22` |
| `DEPLOY_API_SERVICE` | Optional; systemd unit to restart after API deploy, e.g. `rava-api` |
| `DEPLOY_STATUS_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `rava-status` |

Add the matching **public** key to `~/.ssh/authorized_keys` on the server.

## What deploy does

1. **html** — `rsync` from the repo to `DEPLOY_WWW_PATH` (mirrors deletes; game host only).
2. **API** — `rsync` publish artifact to `DEPLOY_API_PATH`, excluding `appsettings*.json` and `html/images/profile/*`. The bundle includes an `html/` folder (game UI + avatar uploads path).
3. **Restart** — runs `sudo systemctl restart <DEPLOY_API_SERVICE>` and optionally `<DEPLOY_STATUS_SERVICE>` when those secrets are set.

Deploy runs only on pushes to `main` (not pull requests), after build and test pass.

## Manual deploy on the server

```bash
# Static game site (from a git checkout)
rsync -av --delete /path/to/rava/server/Rava.Api/html/ /var/www/rava/

# API + status dashboard (from extracted GitHub release zip)
rsync -av --exclude 'appsettings*.json' --exclude 'html/images/profile/*' \
  ./publish/ /var/www/publish/
sudo systemctl restart rava-api
sudo systemctl restart rava-status
```
