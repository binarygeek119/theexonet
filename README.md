# Rava — Phase 1 Core Mining Game

2D point-and-click space mining game with ASP.NET Core backend and mock US-market-linked supply prices.

## Prerequisites

- Unity 6 (6000.x)
- .NET 10 SDK
- Docker (for PostgreSQL)

## Quick Start

### 1. Start PostgreSQL

```bash
docker compose up -d
```

### 2. Start the API

```bash
cd server
dotnet run --project Rava.Api
```

API runs at `http://localhost:5000`

### 3. Open Unity Project

Open the project in Unity 6. Press Play — the game bootstraps automatically.

Register an account, assign workers to mine zones, buy supplies, sell ore, and click **End Day** to advance.

## Architecture

```
rava/
├── Assets/Scripts/     Unity client (Rava.Core, Networking, Mining, UI, Game)
├── server/             ASP.NET Core API + simulation
│   ├── Rava.Api/
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
| POST | `/api/auth/register` | Create account + starter mine |
| POST | `/api/auth/login` | Login |
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
