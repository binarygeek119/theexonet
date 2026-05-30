# Rava — Phase 1 Core Mining Game

2D point-and-click space mining game with ASP.NET Core backend, browser client, and optional Unity client.

## Prerequisites

- .NET 10 SDK
- PostgreSQL 16+ (local Docker or remote server)
- Unity 6 (6000.x) — optional, for the Unity client

## Quick Start (Web App)

### 1. Configure PostgreSQL

Copy the example config and set your connection string:

```bash
cp server/Rava.Api/appsettings.Development.json.example server/Rava.Api/appsettings.Development.json
```

Edit `server/Rava.Api/appsettings.Development.json`:

```json
"DefaultConnection": "Host=192.168.1.2;Port=5432;Database=rava;Username=postgres;Password=YOUR_PASSWORD"
```

Create the `rava` database on your server if it does not exist yet. On first run, the API creates tables automatically via EF Core (`EnsureCreated`).

**Optional local Docker Postgres** (instead of a remote server):

```bash
docker compose up -d
```

Use `Host=localhost;Port=5432;Database=rava;Username=rava;Password=rava_dev` in that case.

### 2. Start the server

```bash
cd server
dotnet run --project Rava.Api
```

Open **http://localhost:5000** in your browser.

Register an account, assign workers to mine zones, buy supplies, sell ore, and click **End Day** to advance.

## Unity Client (Optional)

Open the project in Unity 6 and press Play — the game bootstraps automatically against the same API at `http://localhost:5000`.

## Architecture

```
rava/
├── Assets/Scripts/     Unity client (optional)
├── server/             ASP.NET Core API + simulation
│   ├── Rava.Api/
│   │   └── wwwroot/    Browser client (HTML/CSS/JS)
│   ├── Rava.Core/
│   └── Rava.Infrastructure/
└── docker-compose.yml  PostgreSQL for local dev
```

## Phase 1 Features

- Register / login with JWT auth
- Starter asteroid mine (8×8 grid, point-and-click zones)
- Worker assignment to mining zones
- Daily game tick: ore extraction, payroll, supply consumption
- Mock US market prices for 4 supply types
- NPC ore sales + emergency 50% buyback (anti-softlock)
- Finance dashboard with runway estimate

## Phase 2+ (Reserved)

Friends, player market, multi-mine, mine groups, real US market API, account nuke.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Create account + starter mine (email required) |
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/forgot-password` | Email password reset link |
| POST | `/api/auth/reset-password` | Set new password with reset token |
| GET | `/api/mines/{id}` | Mine state |
| POST | `/api/mines/{id}/assign-worker` | Assign worker |
| POST | `/api/mines/{id}/buy-supply` | Buy supplies |
| POST | `/api/mines/{id}/sell-ore` | Sell ore |
| POST | `/api/game/advance-day` | Advance game day |
| GET | `/api/market/today` | Current supply prices |
| GET | `/api/player/finances` | Finance summary |

## Tests

```bash
cd server
dotnet test
```
