<p align="center">
  <img src="logo.svg" alt="theexonet logo" width="280">
</p>

# theexonet

[![License: MPL-2.0](https://img.shields.io/badge/License-MPL--2.0-blue.svg)](License)

A browser-based sci-fi asteroid mining game. Register an account, run mines on the belt, manage workers and supplies, sell ore, and advance game days to keep your operation alive. Beyond the mine grid, **Exonet** is the in-game frontier network—trade data, player profiles, AI-generated news, and lore that updates from the live server.



**Stack:** ASP.NET Core 10 · PostgreSQL · HTML/CSS/JS · optional Unity client

---

## About

theexonet is a point-and-click mining sim set in a hard-science frontier. Players build credit runway, buy supplies, assign workers to zones on an asteroid grid, and end each day to extract ore and pay payroll. The economy includes public trade listings, company names, miner profiles, and a friends directory.

Staff-facing portals handle moderation, admin operations, and server health. Exonet sites add daily world-building content—some generated with OpenAI at UTC midnight.

---

## Features

### Gameplay

- Account registration and JWT login (email, password reset)
- Starter mine with point-and-click zone assignment
- Daily game tick: extraction, payroll, supply consumption
- Finance dashboard and runway estimates
- Trade market, supply store, shipping/refinery flows
- Miner profiles (avatars, backgrounds, social links, rankings)
- Friends and social directory

### Exonet (in-game browser)

- **Trade Market** — public market data
- **Supply Store** — catalog and listings
- **Offworld News** — daily AI frontier headlines
- **Lunar Weather** — space-weather relay bulletins
- **Foreverfall Penitentiary** — galactic lifetime-sentence inmate registry
- **theexonet Archives** — official player documentation

### Operations

- **Status dashboard** — API/database health, OpenAI usage
- **Admin portal** — players, bans, events, credits, Exonet tuning
- **Moderator portal** — messages, appeals, flagged content

The game exposes a REST API under `/api`. See source controllers in `server/Theexonet.Api/Controllers/` for routes.

---

## Repository layout

```
theexonet/
├── Assets/Scripts/          Unity client (optional)
├── server/
│   ├── Theexonet.Api/            Game API + browser client (html/)
│   ├── Theexonet.Core/           Domain logic, DTOs, simulation
│   ├── Theexonet.Infrastructure/ EF Core, services
│   ├── Theexonet.Core.Tests/
│   ├── Theexonet.Status/         Status dashboard (port 6000)
│   ├── Theexonet.Admin/          Admin portal (port 7000)
│   ├── Theexonet.Moderator/      Moderator portal (port 7050)
│   └── Theexonet.Docs/           Game docs host (port 9000)
├── docs/                    Deploy, translation, versioning
├── scripts/                 Server helpers and publish scripts
└── docker-compose.yml       Local PostgreSQL (optional)
```

---

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 16+ (local Docker via `docker compose up -d` or your own server)

### Run locally

```bash
cp server/Theexonet.Api/appsettings.json.example server/Theexonet.Api/appsettings.json
# Edit DefaultConnection and secrets in appsettings.json

cd server
dotnet run --project Theexonet.Api
```

Open **http://localhost:5000**, register an account, and start mining.

Optional local Postgres connection string:

```
Host=localhost;Port=5432;Database=theexonet;Username=theexonet;Password=theexonet_dev
```

### Optional services

```bash
cd server
dotnet run --project Theexonet.Status      # http://localhost:6000
dotnet run --project Theexonet.Admin       # http://localhost:7000
dotnet run --project Theexonet.Moderator   # http://localhost:7050
dotnet run --project Theexonet.Docs        # http://localhost:9000
```

### Tests

```bash
cd server
dotnet test
```

---

## Services (production layout)

| Service | Role | Port |
|---------|------|------|
| Game UI | Browser client (`html/`) | 80 (HTTPS via reverse proxy) |
| Theexonet.Api | Game API | 5000 |
| Theexonet.Status | Health and monitoring | 6000 |
| Theexonet.Admin | Admin operations | 7000 |
| Theexonet.Moderator | Moderation | 7050 |
| Theexonet.Docs | Player documentation | 9000 |

Production deploys all backends from one publish folder. Set `"Hosting": { "ServeGameUi": false }` on the API host so the game UI is served separately. See **[docs/deploy.md](docs/deploy.md)** and **[docs/github-deploy-setup.md](docs/github-deploy-setup.md)** for systemd, SSH deploy, and GitHub Actions.

---

## Unity client (optional)

Open the project in **Unity 6** and press Play. The client talks to the same API at `http://localhost:5000`.

---

## CI and security

GitHub Actions on `main`:

- **theexonet CI** — build, test, publish artifact, optional production deploy
- **Security** — NuGet vulnerability audit, CodeQL for C#

Dependabot opens weekly update PRs for NuGet and GitHub Actions.

### Security and code audit

**theexonet is built primarily with AI-assisted development.** Automated checks (CodeQL, dependency audits, CI tests) help, but they are not a substitute for human review.

I am looking for **code auditors and security reviewers** who can help assess the codebase end to end—authentication, authorization, input validation, secrets handling, deployment posture, and player-data safety. If you have experience in application security, penetration testing, or secure code review and want to audit an MPL-2.0 game stack (ASP.NET Core, PostgreSQL, browser client), please get in touch.

Reasonable reports, pull requests, and coordinated disclosure are welcome.

---

## Get involved

theexonet is an open project and I would like more people to help shape it. You do not need to commit to a huge roadmap—a focused PR, design note, or audit is valuable.

| Area | Examples |
|------|----------|
| **Coding** | Bug fixes, refactors, tests, tooling, scripts |
| **Ideas** | Gameplay, economy, Exonet lore, moderation workflows |
| **Art** | UI visuals, icons, mine/asteroid presentation, branding |
| **Backend** | API, simulation, data, schedulers, PostgreSQL |
| **Frontend** | Game UI (`html/`), JavaScript, CSS, accessibility |
| **UI / UX** | Layout, flows, mobile-friendly patterns, portal design |

**Ways to contribute**

- Open a [GitHub issue](https://github.com/binarygeek119/theexonet/issues) to discuss an idea before coding
- Submit a pull request against `main` (see [docs/branch-protection.md](docs/branch-protection.md))
- For security findings, prefer a private report if the issue is sensitive

If you are unsure where to start, say what interests you (gameplay, ops, art, security) and we can point you at a good first task.

---

## Documentation

| Doc | Description |
|-----|-------------|
| [docs/deploy.md](docs/deploy.md) | Production setup, systemd, permissions |
| [docs/github-deploy-setup.md](docs/github-deploy-setup.md) | GitHub Actions auto-deploy |
| [docs/branch-protection.md](docs/branch-protection.md) | Branch protection rules for `main` |
| [docs/TRANSLATION.md](docs/TRANSLATION.md) | i18n and Weblate (currently English-only) |
| [docs/VERSIONING.md](docs/VERSIONING.md) | Semantic versioning policy |
| [docs/create-account.md](docs/create-account.md) | Manual account creation |
| [docs/game/](docs/game/) | Player guide source (also served via Theexonet.Docs) |

UI strings live in `server/Theexonet.Api/html/locales/`. Exonet AI content is English-only and excluded from translation.

---

## License

This project is licensed under the **Mozilla Public License 2.0** with additional project-specific terms. See **[License](License)** for the full text.

Game version **V1.0.0** is defined in `server/Theexonet.Core/Constants/GameVersion.cs`. See [docs/VERSIONING.md](docs/VERSIONING.md) for release policy.
