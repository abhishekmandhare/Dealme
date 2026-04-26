# DealMe - Claude Code Guide

## Project Overview

DealMe is a self-hosted platform that autonomously discovers Australian deals, tracks prices, and sends notifications. Single-user app deployed via Docker Compose on TrueNAS.

## Architecture

- **Backend**: ASP.NET Core 9 (C#), EF Core 9, Hangfire scheduler
- **Frontend**: React + TypeScript, served via Nginx
- **Database**: SQLite (MVP) / PostgreSQL (production)
- **Containers**: 4 Docker services — API, Web, changedetection.io, Apprise
- **Pattern**: Event-inspired hybrid — append-only `DomainEvents` table + mutable state tables
- **Adapters**: Plugin system via `IIntegrationAdapter` interface (FetchAsync, NormaliseAsync, HealthCheckAsync, GetStatusAsync)

## Key Documents

- `docs/PRODUCT.md` — product spec, use cases, principles
- `docs/ARCHITECTURE.md` — C4 diagrams, pipeline flow, deployment
- `docs/DATABASE.md` — schema, enums, indexes, ER diagram

## Design Principles

- **Zero hardcoding** — all config via environment variables and `.env` files
- **Minimise toil (P8)** — automate everything; user effort < 5 min/day
- **Start simple** — MVP is OzBargain RSS → store → Discord notification
- **Add complexity only when needed** — Phase 2+ features (banking, passive income, auto-learning) are documented but not built until required

## Pipeline Flow

Fetch → Normalise → Store (dedup + filter + score) → Notify

## MVP Scope

- **Deal adapters** (`src/DealMe.Infrastructure/Adapters/`): OzBargainAdapter, CheapiesAdapter, FrugalFeedsAdapter, PointHacksAdapter (all RSS via `RssDealAdapter` base), ChangeDetectionAdapter (webhook)
- **Automation stack** (`automation/`, Node + Playwright): Microsoft Rewards — desktop searches, mobile searches, daily activities, $5 Amazon redemption. Edge browser (`channel: 'msedge'`) inside xvfb + `playwright-extra` + stealth plugin
- **Notifications**: Apprise → Discord
- **5 Docker services**: `dealme-api`, `dealme-web` (nginx + React), `dealme-automation`, `changedetection`, `apprise`

## Deployment

- **Target**: TrueNAS server via Docker Compose
- **CI/CD**: GitHub Actions → GHCR → Watchtower auto-pulls on TrueNAS
- **SSH**: truenas_admin@192.168.1.179
- **Local dev**: `deploy/docker-compose.override.yml` builds images from source instead of pulling from GHCR

## Dev Commands

Run these from the repo root unless stated.

```bash
# Build + run the local stack (from deploy/ dir)
cd deploy && docker compose build && docker compose up -d

# Rebuild + restart a single service after editing
docker compose build dealme-api && docker compose up -d dealme-api
# Services: dealme-api | dealme-web | automation | changedetection | apprise

# Run the test suite (36 tests on pure logic)
dotnet test tests/DealMe.Tests/DealMe.Tests.csproj --nologo

# Build any C# project quickly
dotnet build src/DealMe.Api/DealMe.Api.csproj --nologo -v quiet

# Tail automation logs (the Playwright runs live here)
docker logs dealme-automation --tail 50

# Trigger a task directly against the automation service (bypasses API + DB)
curl -s -X POST http://localhost:3100/run/bing-searches -H "Content-Type: application/json" -d '{"searchCount":2}'

# Trigger via the API (records to DB — prefer this for real testing)
curl -s -X POST http://localhost:5001/api/automation/providers/microsoft-rewards/run/bing-searches

# SQLite quick-peek (no sqlite3 in container — copy out + python)
docker cp dealme-api:/data/dealme.db /tmp/d.db && python3 -c "import sqlite3;c=sqlite3.connect('/tmp/d.db');print(c.execute('SELECT COUNT(*) FROM AutomationRuns').fetchone())"
```

## Gotchas (Things That Bit Us)

- **Nginx caches upstream IPs**: recreating the `dealme-api` container → 502 from `dealme-web` until `docker compose restart dealme-web`. Not worth a full resolver fix for single-user setup.
- **Chromium profile Singleton locks**: `/data/browser-profile/Singleton*` orphaned after SIGKILL. `automation/entrypoint.sh` clears them on startup.
- **`networkidle` doesn't settle on Bing**: Bing holds telemetry sockets open. Use `domcontentloaded` + explicit `sleep()` for JS hydration on rewards.bing.com.
- **Direct `/search?q=X` URLs trigger bot detection**: Bing credits zero points. Must land on bing.com, type into `#sb_form_q`, press Enter (see `rewards.js`).
- **Stealth plugin + headless alone ISN'T ENOUGH**: need real Edge (`channel: 'msedge'`) + xvfb headful. Stealth handles `navigator.webdriver`; xvfb handles WebGL/rendering signals.
- **Hangfire uses in-memory storage**: schedules are wiped on every `dealme-api` restart. `Program.cs` re-registers them at boot. `AutomationController.PutConfig` also updates them when the user edits the schedule.
- **EF Core migrations need Designer files**: creating a migration by hand? Write BOTH `xxx.cs` AND `xxx.Designer.cs` with the `[Migration("ID")]` attribute — otherwise EF won't discover it.
- **`TaskCanceledException` during long runs**: long-running controller actions must use `CancellationToken.None` for the downstream HTTP call — otherwise a browser tab close kills the run. See `AutomationController.RunTask`.
- **nginx proxy timeout**: defaulted to 60s, which dies mid-run; `web/nginx.conf` bumps it to 20m for `/api/`.

## Testable pure logic lives in

- `src/DealMe.Infrastructure/Services/AutomationMath.cs` — cron offset + stats delta math
- `src/DealMe.Infrastructure/Adapters/PriceParser.cs` — price extraction from titles
- `src/DealMe.Infrastructure/Services/QueryNormalizer.cs` — product query rewrites

Tests live in `tests/DealMe.Tests/` (xUnit). Internals are exposed via `InternalsVisibleTo` in `DealMe.Infrastructure.csproj`.

## Code Conventions

- C# naming: PascalCase for public members, camelCase for locals
- Enums backed by `byte` for DB storage efficiency
- JSON columns stored as `text` in SQLite, `jsonb` in PostgreSQL
- All timestamps as `datetimeoffset` (UTC)
- Unique constraint `Source + SourceId` for opportunity dedup — no separate dedup table

## What NOT to Build Yet

These are Phase 2+ and should not be scaffolded until explicitly requested:
- Wallos / n8n containers
- Cashback Optimiser, User Profiler, auto-learning feedback loop
- InterestWeights, Earnings, BankConnections, AdapterConfigs tables
- CDR Open Banking integration
- Facebook Marketplace scraper (explicitly out — ToS + account-ban risk; use Gmail-email adapter instead)
