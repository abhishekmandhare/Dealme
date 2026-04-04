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

- 3 adapters: OzBargain RSS, Cheapies RSS, changedetection.io webhooks
- 5 database tables: DomainEvents, Opportunities, PriceWatches, UserPreferences, NotificationChannels
- Notifications via Apprise (Discord primary)
- No bank integration, no auto-learning, no cashback optimiser at MVP

## Deployment

- **Target**: TrueNAS server via Docker Compose
- **CI/CD**: GitHub Actions → GHCR → Watchtower auto-pulls on TrueNAS
- **SSH**: turenas_admin@192.169.1.179

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
- Playwright-based adapters (MS Rewards, etc.)
- CDR Open Banking integration
