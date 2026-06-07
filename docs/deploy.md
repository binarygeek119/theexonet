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

   This copies scripts to `/usr/local/lib/theexonet/scripts/` and adds symlinks:

   | Command | Purpose |
   |---------|---------|
   | `sudo restart-theexonet` | Stop/start all six services, free ports |
   | `sudo diagnose-theexonet-api` | API startup checks |
   | `sudo diagnose-theexonet-portals` | Admin/moderator/docs checks |
   | `sudo install-theexonet-systemd` | Install all six systemd units (includes permissions watcher) |
   | `sudo install-theexonet-portals` | Install admin + moderator units only |
   | `sudo install-theexonet-scripts` | Re-run installer after `git pull` |
   | `sudo fix-theexonet-permissions` | One-shot fix for `/var/www/data` and publish paths |
   | `sudo audit-theexonet-permissions` | Check ownership/writability (exit 1 if broken) |
   | `sudo install-theexonet-permissions-service` | Install/enable `theexonet-permissions` watcher only (run `install-theexonet-scripts` first if the command is missing) |

   Optional override: `THEEXONET_LIB_DIR=/custom/path sudo install-theexonet-scripts /path/to/scripts`

   Install the **ASP.NET Core 10 runtime** (the published API targets `net10.0`; .NET 8 is not enough):

```bash
sudo apt-get install -y software-properties-common
sudo add-apt-repository -y ppa:dotnet/backports
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
dotnet --list-runtimes
```

   On **Ubuntu 22.04 (jammy)**, Microsoft's `packages-microsoft-prod.deb` feed only ships .NET up to 9.x — use the **dotnet backports PPA** above (or the install script below).

   You should see `Microsoft.AspNetCore.App 10.0.x` in the list. If apt still fails, use the [install script](https://learn.microsoft.com/dotnet/core/install/linux-scripted-manual#scripted-install) with `--runtime aspnetcore --channel 10.0`.

   **Optional — .NET SDK 10** (only for manual `sudo deploy-theexonet-portals` / `deploy-theexonet-status` on the server; GitHub Actions deploy does not need this):

```bash
wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
export PATH="/usr/share/dotnet:$PATH"
dotnet --list-sdks
```

   Without the SDK, sync portal static files only: `sudo deploy-theexonet-portals --static-only` (run from your git checkout after `git pull`).

3. Copy `appsettings.json.example` to `appsettings.json` on the API host (e.g. `/var/www/publish/appsettings.json`) and set production secrets — deploy does **not** ship or overwrite this file.

   Required files in `/var/www/publish`:

   | File | Source |
   |------|--------|
   | `Theexonet.Api.dll` + dependencies | GitHub release / `dotnet publish` |
   | `Theexonet.Status.dll` + `wwwroot/` | Same publish bundle |
   | `Theexonet.Admin.dll` + `wwwroot/` | Same publish bundle |
   | `Theexonet.Moderator.dll` + `wwwroot/` | Same publish bundle |
   | `Theexonet.Docs.dll` + `content/` + `wwwroot/` | Same publish bundle |
   | `credits.csv`, `offworld-news-reporters.csv`, other `*.csv` | Included in publish output and CI `data/` bundle; production deploy rsyncs them to `/var/www/data` (overwrites CSVs only; `appsettings.json` stays put). `restart-theexonet` and `sync-theexonet-data` also refresh CSVs from `/var/www/publish`. The API seeds missing files from publish on startup but does not overwrite existing data CSVs. |
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

   The API resolves paths from the folder containing `Theexonet.Api.dll`. With `WorkingDirectory=/var/www/publish`, uploads are written to **`/var/www/publish/html/images/profile/`** (avatars) and **`/var/www/publish/html/images/profile-backgrounds/`** (banner backgrounds). URL paths are `/images/profile/...` and `/images/profile-backgrounds/...`.

4. Optional: systemd unit for the API — copy from `scripts/systemd/theexonet-api.service` or create `/etc/systemd/system/theexonet-api.service`:

```ini
[Unit]
Description=theexonet API
After=network.target

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Theexonet.Api.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now theexonet-api
```

Optional status dashboard on port **6000** — uses the same `/var/www/publish` folder as the API:

Add to `/var/www/publish/appsettings.json`:

```json
"StatusMonitor": {
  "ApiBaseUrl": "http://127.0.0.1:5000",
  "GameUrl": "https://theexonet.binarygeek119.duckdns.org/",
  "ApiPublicUrl": "https://theexonetapi.binarygeek119.duckdns.org/",
  "StatusPublicUrl": "https://theexonetstatus.binarygeek119.duckdns.org/",
  "DocsInternalUrl": "http://127.0.0.1:9000",
  "DocsPublicUrl": "https://theexonetdocs.binarygeek119.duckdns.org/"
}
```

`/etc/systemd/system/theexonet-status.service` — see `scripts/systemd/theexonet-status.service`:

```ini
[Unit]
Description=theexonet Status Dashboard
After=network.target theexonet-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Theexonet.Status.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:6000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now theexonet-status
```

`Theexonet.Status` serves static files from `/var/www/publish/status-wwwroot/` first, then `/var/www/publish/wwwroot/` (`index.html`, `js/status.js`, etc.). CI and `deploy-theexonet-portals` use `scripts/sync-publish-wwwroot.sh` so admin/moderator deploys do not delete status assets. If the status site returns **404** or Chrome shows `chrome-error://chromewebdata/`, the dashboard HTML was likely removed by a portal-only rsync — restore and restart: `sudo deploy-theexonet-status --static-only` (from the repo on the server), then hard-refresh the browser.

Optional admin portal on port **7000** — uses the same `/var/www/publish` folder as the API.

Add to `/var/www/publish/appsettings.json` (if not already present from `AdminPortal` above):

```json
"AdminPortal": {
  "PublicUrl": "https://theexonetadmin.binarygeek119.duckdns.org/",
  "GameUrl": "https://theexonet.binarygeek119.duckdns.org/",
  "ApiBaseUrl": "http://127.0.0.1:5000"
}
```

`/etc/systemd/system/theexonet-admin.service` — see `scripts/systemd/theexonet-admin.service`:

```ini
[Unit]
Description=theexonet Admin Portal
After=network.target theexonet-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Theexonet.Admin.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:7000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now theexonet-admin
```

Optional moderator portal on port **7050** — uses the same `/var/www/publish` folder as the API.

Add to `/var/www/publish/appsettings.json`:

```json
"ModeratorPortal": {
  "PublicUrl": "https://theexonetmoderator.binarygeek119.duckdns.org/",
  "GameUrl": "https://theexonet.binarygeek119.duckdns.org/",
  "AdminPortalUrl": "https://theexonetadmin.binarygeek119.duckdns.org/",
  "ApiBaseUrl": "http://127.0.0.1:5000"
}
```

`/etc/systemd/system/theexonet-moderator.service` — see `scripts/systemd/theexonet-moderator.service`:

```ini
[Unit]
Description=theexonet Moderator Portal
After=network.target theexonet-api.service

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Theexonet.Moderator.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:7050
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now theexonet-moderator
```

Game docs on port **9000** — uses the same `/var/www/publish` folder as the API. Markdown source lives in the repo under `docs/game/` and is published to `content/` in the deploy bundle.

Add to `/var/www/publish/appsettings.json`:

```json
"DocsPortal": {
  "PublicUrl": "https://theexonetdocs.binarygeek119.duckdns.org/",
  "GameUrl": "https://theexonet.binarygeek119.duckdns.org/",
  "ContentPath": "content",
  "SiteTitle": "theexonet Game Docs"
}
```

`/etc/systemd/system/theexonet-docs.service` — see `scripts/systemd/theexonet-docs.service`:

```ini
[Unit]
Description=theexonet Game Docs
After=network.target

[Service]
WorkingDirectory=/var/www/publish
ExecStart=/usr/bin/dotnet /var/www/publish/Theexonet.Docs.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:9000
Environment=DOTNET_ENVIRONMENT=Production
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now theexonet-docs
```

**Quick install (all five units):** copy `scripts/systemd/*.service` to the server, then:

```bash
sudo install-theexonet-systemd
```

Allow passwordless deploy commands for GitHub Actions (optional — use your SSH login user, not necessarily `www-data`):

```bash
echo 'YOUR_SSH_USER ALL=(ALL) NOPASSWD: /usr/local/bin/restart-theexonet, /usr/local/lib/theexonet/scripts/restart-theexonet.sh, /usr/local/bin/install-theexonet-scripts, /usr/local/lib/theexonet/scripts/install-bin-scripts.sh, /bin/systemctl restart theexonet-api, /bin/systemctl restart theexonet-status, /bin/systemctl restart theexonet-admin, /bin/systemctl restart theexonet-moderator, /bin/systemctl restart theexonet-docs' | sudo tee /etc/sudoers.d/theexonet-deploy
sudo chmod 440 /etc/sudoers.d/theexonet-deploy
```

The deploy workflow syncs `scripts/` on each run, installs helpers to `/usr/local/bin`, then runs `restart-theexonet`.

5. Configure your reverse proxy:

| Host | Backend |
|------|---------|
| `theexonet.binarygeek119.duckdns.org` | port 80 (game static site) |
| `theexonetapi.binarygeek119.duckdns.org` | port 5000 (`Theexonet.Api`) |
| `theexonetstatus.binarygeek119.duckdns.org` | port 6000 (`Theexonet.Status`) |
| `theexonetadmin.binarygeek119.duckdns.org` | port 7000 (`Theexonet.Admin`) |
| `theexonetmoderator.binarygeek119.duckdns.org` | port 7050 (`Theexonet.Moderator`) |
| `theexonetdocs.binarygeek119.duckdns.org` | port 9000 (`Theexonet.Docs`) |

Example nginx server block for the game docs site:

```nginx
server {
    listen 443 ssl;
    server_name theexonetdocs.binarygeek119.duckdns.org;

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
    server_name theexonetmoderator.binarygeek119.duckdns.org;

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
    server_name theexonetadmin.binarygeek119.duckdns.org;

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
    server_name theexonetstatus.binarygeek119.duckdns.org;

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

If the API returns **502**, `/api/status` shows **database offline**, or **`theexonet-api.service` failed (Result: core-dump)**:

1. **Run diagnostics on the server:**  
   `sudo diagnose-theexonet-api`
2. **Check the service log:** `sudo journalctl -u theexonet-api -n 80 --no-pager`
3. **Manual startup (shows the real error on stdout):**  
   `sudo -u www-data env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet /var/www/publish/Theexonet.Api.dll`
4. **After fixing, clear systemd rate-limit:**  
   `sudo systemctl reset-failed theexonet-api && sudo systemctl restart theexonet-api`
5. **Verify Postgres from the API server:**  
   `psql "Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD" -c "SELECT 1"`
6. **Common mistakes:**
   - `Host=localhost` when PostgreSQL is on another machine
   - Wrong database name or username (must match your real Postgres setup)
   - **`28P01: password authentication failed for user "theexonet"`** — production `appsettings.json` still uses `Username=theexonet`; use your real Postgres user (often `postgres`). Deploy never overwrites `appsettings.json`.
   - `appsettings.json` missing from the API folder (publish does not include it)
   - `credits.csv` missing from the API folder
7. **Test locally on the server:**  
   `curl http://127.0.0.1:5000/api/status` — should return JSON with `"databaseStatus":"online"`
8. **`.NET runtime missing`:** log shows `Framework: Microsoft.NETCore.App, version '10.0.0'` but only 8.x installed — install `aspnetcore-runtime-10.0` (see step 2 above).
9. **`Address already in use` / socket bind error:** another process holds port 5000, 6000, 7000, 7050, or 9000 (`sudo ss -tlnp | grep -E '5000|6000|7000|7050|9000'`). Do **not** put `"Urls"` in the shared `/var/www/publish/appsettings.json` — set ports only in each systemd unit (`ASPNETCORE_URLS=http://0.0.0.0:5000` for API, `:6000` for status, `:7000` for admin, `:7050` for moderator, `:9000` for docs).
10. **`Access to the path ... is denied` / upload or Offworld News write failures:** run `sudo fix-theexonet-permissions`, then enable the auto-watcher: `sudo install-theexonet-permissions-service` (or `sudo install-theexonet-systemd`, which installs `theexonet-permissions.service`). The watcher runs as **root**, polls `/var/www/data` every 30s, and fixes permissions when theexonet logs mention `Permission denied` or paths are not writable by `www-data`. Logs: `journalctl -u theexonet-permissions -f`.
11. **`Could not parse the JSON file` / `LineNumber: 308`:** `/var/www/publish/appsettings.json` is invalid (often duplicated content from repeated edits). Back it up, replace from `appsettings.production.example.json`, set your postgres password, then validate with `python3 -m json.tool /var/www/publish/appsettings.json`.
12. **Admin / moderator / docs `core-dump` (ABRT):** run `sudo diagnose-theexonet-portals`. Common causes: missing `Theexonet.Admin.dll` / `Theexonet.Moderator.dll` / `Theexonet.Docs.dll` or `content/` (redeploy from `main`), port 7000/7050/9000 already in use, or a broken systemd unit missing `Environment=ASPNETCORE_URLS`. Reinstall units with `sudo install-theexonet-systemd`, then `sudo systemctl reset-failed theexonet-admin theexonet-moderator theexonet-docs`.
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
| `DEPLOY_API_SERVICE` | Optional; systemd unit to restart after API deploy, e.g. `theexonet-api` |
| `DEPLOY_STATUS_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `theexonet-status` |
| `DEPLOY_ADMIN_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `theexonet-admin` |
| `DEPLOY_MODERATOR_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `theexonet-moderator` |
| `DEPLOY_DOCS_SERVICE` | Optional; systemd unit to restart after deploy, e.g. `theexonet-docs` |
| `DEPLOY_REPO_PATH` | Optional; on-server git checkout synced each deploy, default `/opt/theexonet/theexonet` |

Add the matching **public** key to `~/.ssh/authorized_keys` on the server.

## What deploy does

1. **publish** — `rsync` from the `dotnet publish` artifact to `DEPLOY_API_PATH` (mirrors deletes except protected paths: `appsettings.json`, `html/images/profile/`, `html/images/profile-backgrounds/`, `.aspnet/`). The bundle includes an `html/` folder (game UI + upload paths). Deploy never overwrites production secrets in `appsettings.json`.
2. **admin/moderator wwwroot** — after publish, CI runs `scripts/sync-portal-wwwroot.sh` and rsyncs `admin.html`, `moderator.html`, and portal JS/CSS into `${DEPLOY_API_PATH}/wwwroot/`. **Theexonet.Admin** (port 7000) and **Theexonet.Moderator** (port 7050) serve static files from that folder (`WorkingDirectory=/var/www/publish`).
3. **html** — skipped when `DEPLOY_WWW_PATH` equals `DEPLOY_API_PATH` (or `${DEPLOY_API_PATH}/html`); game files ship inside `publish/html/`. A separate html rsync only runs when www and api paths differ (legacy `/var/www/theexonet` layout).
4. **Restart** — runs `sudo systemctl restart` for API, status, admin, moderator, and docs services when configured.

Deploy runs in the **theexonet CI** workflow (`.github/workflows/build-website.yml`) on pushes to `main` (not pull requests), after build and test pass.

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
sudo systemctl restart theexonet-api
sudo systemctl restart theexonet-status
sudo systemctl restart theexonet-admin
sudo systemctl restart theexonet-moderator
sudo systemctl restart theexonet-docs
```

Or use the helper (after `sudo bash scripts/install-bin-scripts.sh` once):

```bash
sudo restart-theexonet
```

Starts **theexonet-api** before **theexonet-status**, **theexonet-admin**, **theexonet-moderator**, and **theexonet-docs**, and clears stray processes on ports 5000/6000/7000/7050/9000.
