# Production auto-deploy

When a build on `main` succeeds, GitHub Actions can deploy automatically:

| Target | Source | Typical path |
|--------|--------|--------------|
| Game site (port 80) + API + status + admin + moderator + docs | `dotnet publish` output (game UI under `html/`) | `/var/www/publish` |

**Recommended:** one folder — set both `DEPLOY_WWW_PATH` and `DEPLOY_API_PATH` to `/var/www/publish`. Point nginx/Apache for the game site at `/var/www/publish/html`. Do **not** set either path to `/var/www` alone (that drops `html/`, `wwwroot/`, and `.aspnet/` beside `publish/`).

## One-time server setup

1. Ensure SSH access for GitHub Actions or manual deploy (often your normal login user or `root` — this is **not** required to be the same account that runs the API).
2. Create directories and permissions:

```bash
sudo mkdir -p /var/www/publish
sudo mkdir -p /var/www/publish/html/images/profile
sudo mkdir -p /var/www/publish/html/images/profile-backgrounds
sudo mkdir -p /var/www/publish/.aspnet
# Use the same user as User= in your systemd units (www-data is typical with Apache):
sudo chown -R www-data:www-data /var/www/publish
```

   **Helper scripts in `/usr/local/bin`** — install once from a repo clone (or copy `scripts/` to the server), then use short commands from anywhere:

```bash
sudo bash scripts/install-bin-scripts.sh
```

   This copies scripts to `/usr/local/lib/rava/scripts/` and adds symlinks:

   | Command | Purpose |
   |---------|---------|
   | `sudo restart-rava` | Stop/start all five services, free ports |
   | `sudo diagnose-rava-api` | API startup checks |
   | `sudo diagnose-rava-portals` | Admin/moderator/docs checks |
   | `sudo install-rava-systemd` | Install all five systemd units |
   | `sudo install-rava-portals` | Install admin + moderator units only |
   | `sudo install-rava-scripts` | Re-run installer after `git pull` |

   Optional override: `RAVA_LIB_DIR=/custom/path sudo install-rava-scripts /path/to/scripts`

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
   | `Rava.Admin.dll` + `wwwroot/` | Same publish bundle |
   | `Rava.Moderator.dll` + `wwwroot/` | Same publish bundle |
   | `Rava.Docs.dll` + `content/` + `wwwroot/` | Same publish bundle |
   | `credits.csv` | Included in publish output |
   | `appsettings.json` | Copy from `appsettings.json.example`, then edit |

   Add **`StatusMonitor`**, **`AdminPortal`**, **`ModeratorPortal`**, and **`DocsPortal`** sections to the same `appsettings.json` for the status dashboard (port 6000), admin portal (port 7000), moderator portal (port 7050), and game docs (port 9000). Do **not** add a top-level `"Urls"` key — each systemd service sets its own port via `ASPNETCORE_URLS`.

   Set **`Hosting:ServeGameUi`** to **`false`** (default in `appsettings.json.example`) so the API subdomain shows a status page at `/` instead of the game UI. The game is served from `/var/www/publish/html` (nginx/Apache on port 80), not from the API.

   **Connection string:** use the same PostgreSQL host, database name, username, and password that work from your dev machine. If Postgres runs on another machine (e.g. `192.168.1.2`), do **not** use `Host=localhost` unless Postgres is installed on the API server itself. The API server must be able to reach the DB host on port 5432.

   **Upload folder permissions** — the API creates `html/images/profile/` and `html/images/profile-backgrounds/` at startup. The systemd service user must own (or be able to write to) `/var/www/publish`:

```bash
sudo mkdir -p /var/www/publish/html/images/profile \
               /var/www/publish/html/images/profile-backgrounds
sudo chown -R www-data:www-data /var/www/publish
```

   Use the same username as `User=` in your systemd units (`www-data`, your login user, or omit `User=` to run as root — not recommended for production).

   The API resolves paths from the folder containing `Rava.Api.dll`. With `WorkingDirectory=/var/www/publish`, uploads are written to **`/var/www/publish/html/images/profile/`** (avatars) and **`/var/www/publish/html/images/profile-backgrounds/`** (banner backgrounds). URL paths are `/images/profile/...` and `/images/profile-backgrounds/...`.

4. Optional: systemd unit for the API — copy from `scripts/systemd/rava-api.service` or create `/etc/systemd/system/rava-api.service`:

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
  "GameUrl": "https://rava.binarygeek119.duckdns.org/",
  "ApiPublicUrl": "https://ravaapi.binarygeek119.duckdns.org/",
  "StatusPublicUrl": "https://ravastatus.binarygeek119.duckdns.org/",
  "DocsInternalUrl": "http://127.0.0.1:9000",
  "DocsPublicUrl": "https://ravadocs.binarygeek119.duckdns.org/"
}
```

`/etc/systemd/system/rava-status.service` — see `scripts/systemd/rava-status.service`:

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

Optional admin portal on port **7000** — uses the same `/var/www/publish` folder as the API.

Add to `/var/www/publish/appsettings.json` (if not already present from `AdminPortal` above):

```json
"AdminPortal": {
  "PublicUrl": "https://ravaadmin.binarygeek119.duckdns.org/",
  "GameUrl": "https://rava.binarygeek119.duckdns.org/",
  "ApiBaseUrl": "http://127.0.0.1:5000"
}
```

`/etc/systemd/system/rava-admin.service` — see `scripts/systemd/rava-admin.service`:

```ini
[Unit]
Description=RAVA Admin Portal
After=network.target rava-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Rava.Admin.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:7000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-admin
```

Optional moderator portal on port **7050** — uses the same `/var/www/publish` folder as the API.

Add to `/var/www/publish/appsettings.json`:

```json
"ModeratorPortal": {
  "PublicUrl": "https://ravamoderator.binarygeek119.duckdns.org/",
  "GameUrl": "https://rava.binarygeek119.duckdns.org/",
  "AdminPortalUrl": "https://ravaadmin.binarygeek119.duckdns.org/",
  "ApiBaseUrl": "http://127.0.0.1:5000"
}
```

`/etc/systemd/system/rava-moderator.service` — see `scripts/systemd/rava-moderator.service`:

```ini
[Unit]
Description=RAVA Moderator Portal
After=network.target rava-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Rava.Moderator.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:7050
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-moderator
```

Game docs on port **9000** — uses the same `/var/www/publish` folder as the API. Markdown source lives in the repo under `docs/game/` and is published to `content/` in the deploy bundle.

Add to `/var/www/publish/appsettings.json`:

```json
"DocsPortal": {
  "PublicUrl": "https://ravadocs.binarygeek119.duckdns.org/",
  "GameUrl": "https://rava.binarygeek119.duckdns.org/",
  "ContentPath": "content",
  "SiteTitle": "RAVA Game Docs"
}
```

`/etc/systemd/system/rava-docs.service` — see `scripts/systemd/rava-docs.service`:

```ini
[Unit]
Description=RAVA Game Docs
After=network.target

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Rava.Docs.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:9000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now rava-docs
```

**Quick install (all five units):** copy `scripts/systemd/*.service` to the server, then:

```bash
sudo install-rava-systemd
```

Allow passwordless deploy commands for GitHub Actions (optional — use your SSH login user, not necessarily `www-data`):

```bash
echo 'YOUR_SSH_USER ALL=(ALL) NOPASSWD: /usr/local/bin/restart-rava, /usr/local/lib/rava/scripts/restart-rava.sh, /usr/local/bin/install-rava-scripts, /usr/local/lib/rava/scripts/install-bin-scripts.sh, /bin/systemctl restart rava-api, /bin/systemctl restart rava-status, /bin/systemctl restart rava-admin, /bin/systemctl restart rava-moderator, /bin/systemctl restart rava-docs' | sudo tee /etc/sudoers.d/rava-deploy
sudo chmod 440 /etc/sudoers.d/rava-deploy
```

The deploy workflow syncs `scripts/` on each run, installs helpers to `/usr/local/bin`, then runs `restart-rava`.

5. Configure your reverse proxy:

| Host | Backend |
|------|---------|
| `rava.binarygeek119.duckdns.org` | port 80 (game static site) |
| `ravaapi.binarygeek119.duckdns.org` | port 5000 (`Rava.Api`) |
| `ravastatus.binarygeek119.duckdns.org` | port 6000 (`Rava.Status`) |
| `ravaadmin.binarygeek119.duckdns.org` | port 7000 (`Rava.Admin`) |
| `ravamoderator.binarygeek119.duckdns.org` | port 7050 (`Rava.Moderator`) |
| `ravadocs.binarygeek119.duckdns.org` | port 9000 (`Rava.Docs`) |

Example nginx server block for the game docs site:

```nginx
server {
    listen 443 ssl;
    server_name ravadocs.binarygeek119.duckdns.org;

    location / {
        proxy_pass http://127.0.0.1:9000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Example nginx server block for the moderator portal:

```nginx
server {
    listen 443 ssl;
    server_name ravamoderator.binarygeek119.duckdns.org;

    location / {
        proxy_pass http://127.0.0.1:7050;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Example nginx server block for the admin portal:

```nginx
server {
    listen 443 ssl;
    server_name ravaadmin.binarygeek119.duckdns.org;

    location / {
        proxy_pass http://127.0.0.1:7000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

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

If the API returns **502**, `/api/status` shows **database offline**, or **`rava-api.service` failed (Result: core-dump)**:

1. **Run diagnostics on the server:**  
   `sudo diagnose-rava-api`
2. **Check the service log:** `sudo journalctl -u rava-api -n 80 --no-pager`
3. **Manual startup (shows the real error on stdout):**  
   `sudo -u www-data env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet /var/www/publish/Rava.Api.dll`
4. **After fixing, clear systemd rate-limit:**  
   `sudo systemctl reset-failed rava-api && sudo systemctl restart rava-api`
5. **Verify Postgres from the API server:**  
   `psql "Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD" -c "SELECT 1"`
6. **Common mistakes:**
   - `Host=localhost` when PostgreSQL is on another machine
   - Wrong database name or username (must match your real Postgres setup)
   - **`28P01: password authentication failed for user "rava"`** — production `appsettings.json` still uses `Username=rava`; use your real Postgres user (often `postgres`). Deploy never overwrites `appsettings.json`.
   - `appsettings.json` missing from the API folder (publish does not include it)
   - `credits.csv` missing from the API folder
7. **Test locally on the server:**  
   `curl http://127.0.0.1:5000/api/status` — should return JSON with `"databaseStatus":"online"`
8. **`.NET runtime missing`:** log shows `Framework: Microsoft.NETCore.App, version '10.0.0'` but only 8.x installed — install `aspnetcore-runtime-10.0` (see step 2 above).
9. **`Address already in use` / socket bind error:** another process holds port 5000, 6000, 7000, 7050, or 9000 (`sudo ss -tlnp | grep -E '5000|6000|7000|7050|9000'`). Do **not** put `"Urls"` in the shared `/var/www/publish/appsettings.json` — set ports only in each systemd unit (`ASPNETCORE_URLS=http://0.0.0.0:5000` for API, `:6000` for status, `:7000` for admin, `:7050` for moderator, `:9000` for docs).
10. **`Access to the path .../html/images/profile is denied`:** fix ownership on `/var/www/publish` (see upload folder permissions in step 3 above).
11. **`Could not parse the JSON file` / `LineNumber: 308`:** `/var/www/publish/appsettings.json` is invalid (often duplicated content from repeated edits). Back it up, replace from `appsettings.production.example.json`, set your postgres password, then validate with `python3 -m json.tool /var/www/publish/appsettings.json`.
12. **Admin / moderator / docs `core-dump` (ABRT):** run `sudo diagnose-rava-portals`. Common causes: missing `Rava.Admin.dll` / `Rava.Moderator.dll` / `Rava.Docs.dll` or `content/` (redeploy from `main`), port 7000/7050/9000 already in use, or a broken systemd unit missing `Environment=ASPNETCORE_URLS`. Reinstall units with `sudo install-rava-systemd`, then `sudo systemctl reset-failed rava-admin rava-moderator rava-docs`.
13. **Stray `/var/www/html`, `/var/www/wwwroot`, or `/var/www/.aspnet`:** `DEPLOY_WWW_PATH` or `DEPLOY_API_PATH` was set to `/var/www` instead of `/var/www/publish`. Set both GitHub variables to `/var/www/publish`, remove the stray folders after confirming nothing important lives there, and re-run deploy:

```bash
# Only after verifying these are not your live game/API files:
sudo rm -rf /var/www/html /var/www/wwwroot
sudo mv /var/www/.aspnet /var/www/publish/.aspnet 2>/dev/null || true
sudo chown -R www-data:www-data /var/www/publish
```

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
| `DEPLOY_WWW_PATH` | Absolute path for deploy root — use `/var/www/publish` (same as API path) |
| `DEPLOY_API_PATH` | Absolute path for API publish output — `/var/www/publish` |
| `DEPLOY_WWW_HOST` | Optional; game host if different from `DEPLOY_HOST` |
| `DEPLOY_API_HOST` | Optional; API host if different from `DEPLOY_HOST` |
| `DEPLOY_SSH_PORT` | Optional; default `22` |
| `DEPLOY_API_SERVICE` | Optional; systemd unit to restart after API deploy, e.g. `rava-api` |
| `DEPLOY_STATUS_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `rava-status` |
| `DEPLOY_ADMIN_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `rava-admin` |
| `DEPLOY_MODERATOR_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `rava-moderator` |
| `DEPLOY_DOCS_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `rava-docs` |

Add the matching **public** key to `~/.ssh/authorized_keys` on the server.

## What deploy does

1. **publish** — `rsync` from the `dotnet publish` artifact to `DEPLOY_API_PATH` (mirrors deletes except protected paths: `appsettings.json`, `html/images/profile/`, `html/images/profile-backgrounds/`, `.aspnet/`). The bundle includes an `html/` folder (game UI + upload paths). Deploy never overwrites production secrets in `appsettings.json`.
2. **html** — skipped when `DEPLOY_WWW_PATH` equals `DEPLOY_API_PATH` (or `${DEPLOY_API_PATH}/html`); game files ship inside `publish/html/`. A separate html rsync only runs when www and api paths differ (legacy `/var/www/rava` layout).
3. **Restart** — runs `sudo systemctl restart` for API, status, admin, moderator, and docs services when configured.

Deploy runs only on pushes to `main` (not pull requests), after build and test pass.

## Manual deploy on the server

```bash
# API + game html + status + admin + moderator + docs (from extracted GitHub release zip)
# --filter P* keeps server-only appsettings.json and .aspnet keys safe when using --delete
rsync -av --delete \
  --filter 'P appsettings.json' \
  --filter 'P appsettings.Development.json' \
  --filter 'P server-runtime.json' \
  --filter 'P status-runtime.json' \
  --filter 'P html/images/profile/' \
  --filter 'P html/images/profile/***' \
  --filter 'P html/images/profile-backgrounds/' \
  --filter 'P html/images/profile-backgrounds/***' \
  --filter 'P .aspnet/' \
  --filter 'P .aspnet/***' \
  --exclude 'appsettings.json' \
  --exclude 'appsettings.Development.json' \
  --exclude 'html/images/profile/' \
  --exclude 'html/images/profile-backgrounds/' \
  --exclude 'server-runtime.json' \
  --exclude 'status-runtime.json' \
  ./publish/ /var/www/publish/
sudo systemctl restart rava-api
sudo systemctl restart rava-status
sudo systemctl restart rava-admin
sudo systemctl restart rava-moderator
sudo systemctl restart rava-docs
```

Or use the helper (after `sudo bash scripts/install-bin-scripts.sh` once):

```bash
sudo restart-rava
```

Starts **rava-api** before **rava-status**, **rava-admin**, **rava-moderator**, and **rava-docs**, and clears stray processes on ports 5000/6000/7000/7050/9000.
