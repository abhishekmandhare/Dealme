# DealMe - Architecture (C4 Model)

> Rendered natively in VS Code (Ctrl+Shift+V) and on GitHub.

---

## C4 Level 1: System Context

```mermaid
graph TB
    User["👤 DealMe User<br/><i>Australian resident wanting to<br/>automatically earn & save money</i>"]

    DealMe["🟦 DealMe Platform<br/><i>Self-hosted platform that discovers<br/>deals, tracks prices, and sends alerts</i>"]

    Deals["⬜ Deal Sources<br/><i>OzBargain, Cheapies,<br/>Frugal Feeds, TopBargains</i>"]
    Cashback["⬜ Cashback Platforms<br/><i>TopCashback, ShopBack</i>"]
    FinInfo["⬜ Financial Info<br/><i>Point Hacks, Finder,<br/>Bank offer pages</i>"]
    Notif["⬜ Notification Channels<br/><i>Discord, Telegram, Email<br/>— 90+ via Apprise</i>"]

    User -->|"Views dashboard,<br/>receives alerts"| DealMe

    DealMe -->|"Polls deals [RSS]"| Deals
    DealMe -->|"Scrapes rates [HTTP]"| Cashback
    DealMe -->|"Monitors changes [HTTP]"| FinInfo
    DealMe -->|"Sends alerts [HTTP API]"| Notif

    style DealMe fill:#1168BD,stroke:#0E5AA7,color:#fff
    style User fill:#08427B,stroke:#073B6F,color:#fff
    style Deals fill:#999,stroke:#888,color:#fff
    style Cashback fill:#999,stroke:#888,color:#fff
    style FinInfo fill:#999,stroke:#888,color:#fff
    style Notif fill:#999,stroke:#888,color:#fff
```

**Legend:** 🟦 Blue = DealMe system | ⬜ Grey = External system

**Future external systems** (Phase 2+): Banking APIs (Up Bank, CDR), Rewards & Passive Income (MS Rewards, Honeygain, EarnApp), Government Portals, Class Action Sources, Grocery & Food.

---

## C4 Level 2: Container Diagram

```mermaid
graph TB
    User["DealMe User"]

    Dashboard["Web Dashboard<br/>React + TypeScript<br/><br/>Deal feed, price watches,<br/>settings"]
    API["Core API<br/>ASP.NET Core 9<br/><br/>Pipeline, adapters,<br/>Hangfire scheduler"]
    DB[("SQLite<br/>MVP Database")]
    CDIO["changedetection.io<br/>Port 5000<br/><br/>Price & page monitoring"]
    Apprise["Apprise<br/>Port 8000<br/><br/>Notifications<br/>90+ channels"]

    External["External Sources<br/>RSS, Scrape targets"]
    Devices["User Devices<br/>Discord, Telegram, Email"]

    User -->|HTTPS| Dashboard
    User -.->|Alerts| Devices
    Dashboard -->|REST JSON| API
    API -->|EF Core| DB
    API -->|HTTP| Apprise
    API -->|HTTP| CDIO
    CDIO -->|Webhooks| API
    API -->|Poll / Scrape| External
    CDIO -->|Monitor| External
    Apprise -->|Deliver| Devices

    style User fill:#08427B,stroke:#073B6F,color:#fff
    style Dashboard fill:#438DD5,stroke:#3C7FC0,color:#fff
    style API fill:#438DD5,stroke:#3C7FC0,color:#fff
    style DB fill:#438DD5,stroke:#3C7FC0,color:#fff
    style CDIO fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Apprise fill:#438DD5,stroke:#3C7FC0,color:#fff
    style External fill:#999,stroke:#888,color:#fff
    style Devices fill:#999,stroke:#888,color:#fff
```

| Container | Port | Role |
|---|---|---|
| dealme-api | 5001 | ASP.NET Core 9 + Hangfire |
| dealme-web | 80/443 | React SPA + Nginx |
| changedetection.io | 5000 | Price/page monitoring |
| Apprise | 8000 | Notification gateway |

---

## C4 Level 3: Core API Components

```mermaid
graph TB
    Dashboard["Web Dashboard"]
    CDIO["changedetection.io"]
    Apprise["Apprise"]
    DB[("SQLite")]
    External["External Sources<br/><i>RSS, HTTP</i>"]

    subgraph API["Core API (ASP.NET Core 9)"]
        REST["REST Controllers<br/><i>GET /opportunities<br/>GET /earnings<br/>POST /pricewatches<br/>GET /settings</i>"]
        Webhook["Webhook Controller<br/><i>Receives webhooks from<br/>changedetection.io</i>"]
        Pipeline["Pipeline Service<br/><i>Normalise → Dedup →<br/>Filter → Score → Notify</i>"]
        AdapterHost["Adapter Host<br/><i>Loads IIntegrationAdapter,<br/>routes data into pipeline</i>"]
        Scheduler["Hangfire Scheduler<br/><i>Cron-based adapter polling</i>"]
        Notifier["Notification Service<br/><i>Formats alerts,<br/>quiet hours</i>"]
        EventLog["Event Logger<br/><i>Append-only DomainEvent</i>"]
    end

    Dashboard -->|REST / JSON| REST
    CDIO -->|Webhooks| Webhook

    Webhook --> Pipeline
    Scheduler -->|Triggers on cron| AdapterHost
    AdapterHost -->|Fetch data| External
    AdapterHost --> Pipeline
    Pipeline --> Notifier
    Notifier -->|Send alerts| Apprise

    Pipeline --> EventLog
    Pipeline --> DB
    REST --> DB
    EventLog --> DB

    style Dashboard fill:#438DD5,stroke:#3C7FC0,color:#fff
    style CDIO fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Apprise fill:#438DD5,stroke:#3C7FC0,color:#fff
    style DB fill:#438DD5,stroke:#3C7FC0,color:#fff
    style External fill:#999,stroke:#888,color:#fff
    style REST fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Webhook fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Pipeline fill:#438DD5,stroke:#3C7FC0,color:#fff
    style AdapterHost fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Scheduler fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Notifier fill:#438DD5,stroke:#3C7FC0,color:#fff
    style EventLog fill:#438DD5,stroke:#3C7FC0,color:#fff
```

**Pipeline Service** handles the full flow in a single service:
1. **Normalise** — raw data → `Opportunity` model (AUD, timestamps, confidence)
2. **Dedup** — check `Source + SourceId` unique constraint; skip if exists
3. **Filter** — match against user's enabled categories and min value threshold
4. **Score** — simple value + confidence ranking
5. **Notify** — send to Apprise if it passes the filter

---

## Adapter Layer

MVP adapters implementing `IIntegrationAdapter`:

```mermaid
graph LR
    Host["Adapter Host"]
    Interface["IIntegrationAdapter<br/><i>FetchAsync()<br/>NormaliseAsync()<br/>HealthCheckAsync()<br/>GetStatusAsync()</i>"]

    Host --> Interface

    subgraph MVP["MVP Adapters"]
        OzBargain["OzBargain<br/><i>RSS — 5 min</i>"]
        Cheapies["Cheapies<br/><i>RSS — 5 min</i>"]
        CDIOa["changedetection.io<br/><i>Webhook — event</i>"]
    end

    OzBargain --> Interface
    Cheapies --> Interface
    CDIOa --> Interface

    style Host fill:#85BBF0,stroke:#78A8D8,color:#000
    style Interface fill:#85BBF0,stroke:#78A8D8,color:#000
    style MVP fill:#E3F2FD,stroke:#90CAF9,color:#000
```

### Future Adapters (Phase 2+)

| Tier | Adapters | Method |
|---|---|---|
| Cashback | TopCashback, ShopBack, RetailMeNot AU | AngleSharp scraping |
| Financial | Point Hacks, Finder | RSS + scraping |
| Passive Income | MS Rewards, Honeygain, EarnApp | Playwright / API |
| Banking | Up Bank, CDR | REST API |
| Government | Rebates, Class Actions | Webhooks |
| Grocery | Coles, Woolworths | AngleSharp scraping |

### Standard Output: Opportunity

Every adapter normalises its data into this model:

```
Opportunity
├── Id: guid
├── Source: string              ("ozbargain", "cheapies", etc.)
├── SourceId: string            (original ID from source)
├── Type: enum                  (Deal, Cashback, Bonus, Rebate, PriceAlert)
├── Status: enum                (New, Seen, Saved, ActedOn, Dismissed, Expired)
├── Title: string
├── Description: string
├── Value: decimal? (AUD)
├── Url: string
├── ExpiresAt: datetime?
├── Confidence: enum            (High, Medium, Low)
├── Tags: string[]
├── Metadata: Dictionary
├── CreatedAt: datetime
└── UpdatedAt: datetime
```

---

## Data Flow

```mermaid
graph LR
    Fetch["1. FETCH<br/><i>RSS / Webhook / Scrape</i>"]
    Normalise["2. NORMALISE<br/><i>Raw → Opportunity</i>"]
    Store["3. STORE<br/><i>Dedup check,<br/>filter, score,<br/>write event + state</i>"]
    Notify["4. NOTIFY<br/><i>Apprise alert<br/>with action link</i>"]
    Dashboard["DASHBOARD<br/><i>Browse & act</i>"]

    Fetch --> Normalise --> Store --> Notify
    Store --> Dashboard

    style Fetch fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Normalise fill:#E8F5E9,stroke:#A5D6A7,color:#000
    style Store fill:#F3E5F5,stroke:#CE93D8,color:#000
    style Notify fill:#E0F2F1,stroke:#80DEEA,color:#000
    style Dashboard fill:#E0F2F1,stroke:#80DEEA,color:#000
```

---

## Event-Inspired Hybrid Pattern

Every pipeline run writes an append-only event **before** updating state. Events are never modified or deleted.

```mermaid
graph LR
    Pipeline["Pipeline Service"]
    EventLog[("DomainEvent<br/><i>Append-only</i>")]
    State[("State Tables<br/><i>Mutable</i>")]
    Dashboard["Dashboard<br/><i>Reads current state</i>"]

    Pipeline -->|"1. Write event"| EventLog
    Pipeline -->|"2. Update state"| State
    State -->|"Fast reads"| Dashboard
    EventLog -.->|"Audit / debug"| Dashboard

    style EventLog fill:#F3E5F5,stroke:#CE93D8,color:#000
    style State fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Pipeline fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Dashboard fill:#E8F5E9,stroke:#A5D6A7,color:#000
```

**Why keep events at MVP?** They're cheap (one append-only table), and give you: full audit trail, pipeline debugging via `CorrelationId`, and the option to add replay/learning later without schema changes.

---

## Deployment: Docker Compose on TrueNAS

```mermaid
graph TB
    subgraph TrueNAS["TrueNAS Server"]
        subgraph Docker["Docker Compose"]
            APIc["dealme-api<br/><i>ASP.NET Core 9<br/>+ Hangfire</i>"]
            Web["dealme-web<br/><i>React SPA + Nginx</i>"]
            CDIOc["changedetection.io<br/><i>:5000</i>"]
            Apprisec["Apprise<br/><i>:8000</i>"]
        end
        subgraph Volumes["Persistent Volumes"]
            V1["db-data<br/><i>SQLite file</i>"]
            V2["cdio-data"]
        end
    end

    GitHub["GitHub<br/><i>main branch</i>"]
    GHCR["GHCR<br/><i>Container images</i>"]
    External["External Services"]
    Devices["User Devices"]

    GitHub -->|"Push to main"| GHCR
    GHCR -->|"Watchtower pulls<br/>updated images"| Docker

    Web -->|":5001"| APIc
    APIc -->|EF Core| V1
    APIc -->|":5000"| CDIOc
    APIc -->|":8000"| Apprisec
    CDIOc -->|Webhooks| APIc
    CDIOc --> V2

    APIc --> External
    CDIOc --> External
    Apprisec --> Devices

    style TrueNAS fill:#f9f9f9,stroke:#ccc,color:#000
    style Docker fill:#f0f0f0,stroke:#bbb,color:#000
    style Volumes fill:#f5f5f5,stroke:#ccc,color:#000
    style APIc fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Web fill:#E8F5E9,stroke:#A5D6A7,color:#000
    style CDIOc fill:#FFFDE7,stroke:#FFF59D,color:#000
    style Apprisec fill:#FFCDD2,stroke:#EF9A9A,color:#000
    style GitHub fill:#999,stroke:#888,color:#fff
    style GHCR fill:#999,stroke:#888,color:#fff
    style External fill:#999,stroke:#888,color:#fff
    style Devices fill:#999,stroke:#888,color:#fff
```

### Auto-Deploy Flow

1. Push to `main` → GitHub Actions builds Docker images → pushes to GHCR
2. Watchtower (running on TrueNAS) polls GHCR for updated images
3. When new image detected → Watchtower pulls and restarts the container
4. Zero manual intervention, zero hardcoding

### Configuration

All config via `.env` file (never committed):

```env
# Database
DATABASE_PROVIDER=sqlite
CONNECTION_STRING=Data Source=/data/dealme.db

# Apprise
APPRISE_URL=http://apprise:8000

# changedetection.io
CDIO_URL=http://cdio:5000
CDIO_API_KEY=${CDIO_API_KEY}

# Notification defaults
DEFAULT_NOTIFICATION_CHANNEL=discord://webhook_id/webhook_token
MIN_DEAL_VALUE=5.00
TIMEZONE=Australia/Melbourne
```
