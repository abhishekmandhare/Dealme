# DealMe - Architecture Diagrams (C4 Model)

> Rendered natively in VS Code (Ctrl+Shift+V) and on GitHub.

---

## C4 Level 1: System Context

Who uses DealMe and what external systems does it talk to?

```mermaid
graph TB
    User["👤 DealMe User<br/><i>Australian resident wanting to<br/>automatically earn & save money</i>"]

    DealMe["🟦 DealMe Platform<br/><i>Self-hosted platform that autonomously<br/>discovers, tracks, and optimises<br/>earnings and savings</i>"]

    Deals["⬜ Deal Sources<br/><i>OzBargain, Cheapies,<br/>Frugal Feeds, TopBargains</i>"]
    Cashback["⬜ Cashback Platforms<br/><i>Cashrewards, ShopBack,<br/>RetailMeNot AU</i>"]
    Rewards["⬜ Rewards & Passive Income<br/><i>Microsoft Rewards,<br/>Honeygain, EarnApp</i>"]
    Banking["⬜ Banking APIs<br/><i>Up Bank REST API,<br/>Consumer Data Right</i>"]
    FinInfo["⬜ Financial Info Sites<br/><i>Point Hacks, Finder,<br/>Bank offer pages</i>"]
    Govt["⬜ Government Portals<br/><i>energy.gov.au,<br/>state portals</i>"]
    Legal["⬜ Class Action Sources<br/><i>Maurice Blackburn, Slater & Gordon,<br/>Shine, Federal Court</i>"]
    Grocery["⬜ Grocery & Food<br/><i>Coles, Woolworths,<br/>Too Good To Go</i>"]
    Notif["⬜ Notification Channels<br/><i>Discord, Telegram, Email,<br/>SMS, Slack — 90+ via Apprise</i>"]

    User -->|"Views dashboard,<br/>receives alerts,<br/>acts on recommendations"| DealMe

    DealMe -->|"Polls deals [RSS]"| Deals
    DealMe -->|"Scrapes rates [HTTP]"| Cashback
    DealMe -->|"Automates tasks,<br/>reads earnings [Playwright/API]"| Rewards
    DealMe -->|"Reads transactions<br/>read-only [REST API]"| Banking
    DealMe -->|"Scrapes offers,<br/>monitors changes [HTTP]"| FinInfo
    DealMe -->|"Monitors rebate pages<br/>[Webhooks]"| Govt
    DealMe -->|"Monitors class actions<br/>[Webhooks]"| Legal
    DealMe -->|"Scrapes weekly specials<br/>[HTTP]"| Grocery
    DealMe -->|"Sends alerts<br/>[HTTP API]"| Notif

    style DealMe fill:#1168BD,stroke:#0E5AA7,color:#fff
    style User fill:#08427B,stroke:#073B6F,color:#fff
    style Deals fill:#999,stroke:#888,color:#fff
    style Cashback fill:#999,stroke:#888,color:#fff
    style Rewards fill:#999,stroke:#888,color:#fff
    style Banking fill:#999,stroke:#888,color:#fff
    style FinInfo fill:#999,stroke:#888,color:#fff
    style Govt fill:#999,stroke:#888,color:#fff
    style Legal fill:#999,stroke:#888,color:#fff
    style Grocery fill:#999,stroke:#888,color:#fff
    style Notif fill:#999,stroke:#888,color:#fff
```
```

**Legend:** 🟦 Blue = DealMe system | ⬜ Grey = External system

---

## C4 Level 2: Container Diagram

What Docker containers make up DealMe and how do they communicate?

```mermaid
graph TB
    User["👤 DealMe User"]

    subgraph DealMe["DealMe Platform (Docker Compose)"]
        Dashboard["🟦 Web Dashboard<br/><i>React + TypeScript</i><br/><br/>Deal feed, price watches,<br/>earnings, settings"]
        API["🟦 Core API<br/><i>ASP.NET Core 9</i><br/><br/>Business logic, matching,<br/>scoring, dedup, Hangfire,<br/>Playwright runtime"]
        DB[("🟦 Database<br/><i>SQLite (MVP) / PostgreSQL</i><br/><br/>Opportunities, Preferences,<br/>Earnings, PriceHistory,<br/>AuditLog")]
        CDIO["🟦 changedetection.io<br/><i>Docker :5000</i><br/><br/>Price & page monitoring"]
        Apprise["🟦 Apprise<br/><i>Docker :8000</i><br/><br/>Notification gateway<br/>90+ channels"]
        Wallos["🟦 Wallos<br/><i>Docker :8282</i><br/><br/>Subscription tracking"]
        N8N["🟦 n8n (optional)<br/><i>Docker :5678</i><br/><br/>Workflow automation"]
    end

    RSS["⬜ RSS Deal Sources<br/><i>OzBargain, Cheapies,<br/>Frugal Feeds</i>"]
    Scrape["⬜ Scrape Targets<br/><i>Cashrewards, ShopBack,<br/>Point Hacks, Finder</i>"]
    APIs["⬜ API Sources<br/><i>Up Bank, Honeygain,<br/>EarnApp</i>"]
    Monitored["⬜ Monitored Sites<br/><i>Bank pages, govt portals,<br/>law firms, retailers</i>"]
    RewardSites["⬜ Rewards Sites<br/><i>Microsoft Rewards</i>"]
    Devices["⬜ User Devices<br/><i>Discord, Telegram,<br/>Email, SMS</i>"]

    User -->|"HTTPS"| Dashboard
    User -.->|"Receives alerts"| Devices
    Dashboard -->|"REST / JSON"| API
    API -->|"EF Core"| DB
    API -->|"HTTP :8000"| Apprise
    API -->|"HTTP :5000"| CDIO
    CDIO -->|"Webhooks"| API
    API -->|"HTTP :8282"| Wallos
    API -->|"HTTP :5678"| N8N

    API -->|"Poll RSS<br/>every 5 min"| RSS
    API -->|"Scrape<br/>every 4-6 hrs"| Scrape
    API -->|"REST API"| APIs
    API -->|"Playwright"| RewardSites
    CDIO -->|"Monitor pages"| Monitored
    Apprise -->|"Deliver alerts"| Devices

    style User fill:#08427B,stroke:#073B6F,color:#fff
    style Dashboard fill:#438DD5,stroke:#3C7FC0,color:#fff
    style API fill:#438DD5,stroke:#3C7FC0,color:#fff
    style DB fill:#438DD5,stroke:#3C7FC0,color:#fff
    style CDIO fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Apprise fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Wallos fill:#438DD5,stroke:#3C7FC0,color:#fff
    style N8N fill:#438DD5,stroke:#3C7FC0,color:#fff
    style RSS fill:#999,stroke:#888,color:#fff
    style Scrape fill:#999,stroke:#888,color:#fff
    style APIs fill:#999,stroke:#888,color:#fff
    style Monitored fill:#999,stroke:#888,color:#fff
    style RewardSites fill:#999,stroke:#888,color:#fff
    style Devices fill:#999,stroke:#888,color:#fff
```

**Legend:** 🟦 Blue = DealMe container | ⬜ Grey = External system

---

## C4 Level 3: Core API Components

What services live inside the Core API?

```mermaid
graph TB
    Dashboard["Web Dashboard"]
    CDIO["changedetection.io"]
    Apprise["Apprise"]
    Wallos["Wallos"]
    DB[("Database")]
    External["External Sources<br/><i>RSS, Scrape, API</i>"]

    subgraph API["Core API (ASP.NET Core 9)"]
        REST["REST API Controllers<br/><i>GET /opportunities<br/>GET /earnings<br/>POST /pricewatches<br/>GET /settings</i>"]
        Webhook["Webhook Controller<br/><i>Receives webhooks from<br/>changedetection.io, n8n</i>"]
        Matching["Matching Engine<br/><i>Keyword, category, and<br/>behavioural matching</i>"]
        Scoring["Scoring Engine<br/><i>ROI ranking, value,<br/>confidence, engagement</i>"]
        Dedup["Dedup Service<br/><i>Title similarity, URL norm,<br/>product fingerprinting</i>"]
        CashbackOpt["Cashback Optimiser<br/><i>Best route: cashback +<br/>coupon + card bonus</i>"]
        Profiler["User Profiler<br/><i>Auto-learns interests<br/>from engagement</i>"]
        Notifier["Notification Service<br/><i>Formats alerts, quiet hours,<br/>channel preferences</i>"]
        Scheduler["Adapter Scheduler<br/><i>Hangfire — cron polling,<br/>retry, queue</i>"]
        AdapterHost["Adapter Host<br/><i>Loads IIntegrationAdapter,<br/>routes data into pipeline</i>"]
        Audit["Audit Logger<br/><i>Immutable log of every<br/>state change</i>"]
        Prefs["User Preferences<br/><i>Interests, thresholds,<br/>channels, cards, state</i>"]
        Health["Health Monitor<br/><i>Adapter health, stale data,<br/>failure alerts</i>"]
        BankConn["Bank Connector<br/><i>Up Bank API / CDR<br/>Read-only, encrypted</i>"]
    end

    Dashboard -->|"REST / JSON"| REST
    CDIO -->|"Webhooks"| Webhook

    REST --> Prefs
    REST --> Matching
    REST --> Scoring
    Webhook --> AdapterHost

    Scheduler -->|"Triggers on cron"| AdapterHost
    AdapterHost --> Dedup
    Dedup --> Matching
    Matching --> Scoring
    Scoring --> CashbackOpt
    CashbackOpt --> Notifier

    Scoring -.->|"Engagement feedback"| Profiler
    Profiler -.->|"Updated weights"| Matching

    AdapterHost -->|"Fetch data"| External
    Notifier -->|"Send alerts"| Apprise
    AdapterHost --> Wallos
    BankConn --> AdapterHost

    AdapterHost --> DB
    Matching --> DB
    Scoring --> DB
    Audit --> DB
    Prefs --> DB
    Profiler --> DB
    BankConn --> DB

    Health -.->|"Alert on failure"| Notifier

    style Dashboard fill:#438DD5,stroke:#3C7FC0,color:#fff
    style CDIO fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Apprise fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Wallos fill:#438DD5,stroke:#3C7FC0,color:#fff
    style DB fill:#438DD5,stroke:#3C7FC0,color:#fff
    style External fill:#999,stroke:#888,color:#fff
    style REST fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Webhook fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Matching fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Scoring fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Dedup fill:#438DD5,stroke:#3C7FC0,color:#fff
    style CashbackOpt fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Profiler fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Notifier fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Scheduler fill:#438DD5,stroke:#3C7FC0,color:#fff
    style AdapterHost fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Audit fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Prefs fill:#438DD5,stroke:#3C7FC0,color:#fff
    style Health fill:#438DD5,stroke:#3C7FC0,color:#fff
    style BankConn fill:#438DD5,stroke:#3C7FC0,color:#fff
```

**Legend:** Light blue = Component | Blue = Container | Grey = External

---

## C4 Level 3: Adapter Layer

All 18 adapters implementing `IIntegrationAdapter`, grouped by deployment tier.

```mermaid
graph LR
    Host["Adapter Host<br/><i>Orchestrates all adapters</i>"]
    Interface["IIntegrationAdapter<br/><i>FetchAsync()<br/>NormaliseAsync()<br/>HealthCheckAsync()<br/>GetStatusAsync()</i>"]

    Host --> Interface

    subgraph Tier1["Tier 1: MVP"]
        OzBargain["OzBargain<br/><i>RSS — 5 min</i>"]
        Cheapies["Cheapies<br/><i>RSS — 5 min</i>"]
        FrugalFeeds["Frugal Feeds<br/><i>RSS — 15 min</i>"]
        CDIOa["changedetection.io<br/><i>Webhook — event</i>"]
        Apprisea["Apprise<br/><i>HTTP — on demand</i>"]
    end

    subgraph Tier2["Tier 2: Cashback & Bonuses"]
        Cashrewards["Cashrewards<br/><i>AngleSharp — 6 hrs</i>"]
        ShopBack["ShopBack<br/><i>AngleSharp — 6 hrs</i>"]
        PointHacks["Point Hacks<br/><i>RSS+Scrape — 12 hrs</i>"]
        Finder["Finder<br/><i>AngleSharp — 12 hrs</i>"]
        RetailMeNot["RetailMeNot AU<br/><i>AngleSharp — 12 hrs</i>"]
    end

    subgraph Tier3["Tier 3: Passive Income & Savings"]
        MSRewards["MS Rewards<br/><i>Playwright — daily 6am</i>"]
        Honeygain["Honeygain<br/><i>API — 24 hrs</i>"]
        EarnApp["EarnApp<br/><i>API — 24 hrs</i>"]
        UpBank["Up Bank<br/><i>REST API — 1 hr</i>"]
        GovRebate["Govt Rebates<br/><i>Static+Webhook — event</i>"]
        ClassAction["Class Actions<br/><i>Webhook — event</i>"]
        Grocery["Grocery<br/><i>AngleSharp — Wed 6pm</i>"]
        WallosA["Wallos<br/><i>Docker API — 24 hrs</i>"]
    end

    OzBargain --> Interface
    Cheapies --> Interface
    FrugalFeeds --> Interface
    CDIOa --> Interface
    Apprisea --> Interface

    Cashrewards --> Interface
    ShopBack --> Interface
    PointHacks --> Interface
    Finder --> Interface
    RetailMeNot --> Interface

    MSRewards --> Interface
    Honeygain --> Interface
    EarnApp --> Interface
    UpBank --> Interface
    GovRebate --> Interface
    ClassAction --> Interface
    Grocery --> Interface
    WallosA --> Interface

    style Host fill:#85BBF0,stroke:#78A8D8,color:#000
    style Interface fill:#85BBF0,stroke:#78A8D8,color:#000
    style Tier1 fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Tier2 fill:#FFFDE7,stroke:#FFF59D,color:#000
    style Tier3 fill:#FCE4EC,stroke:#F8BBD0,color:#000
```

### Standard Output: Opportunity

Every adapter normalises its data into this model:

```
Opportunity
├── Id: guid
├── Source: string              ("ozbargain", "cashrewards", etc.)
├── Type: enum                  (Deal, Cashback, Bonus, Rebate, Earning)
├── Title: string
├── Description: string
├── Value: decimal (AUD)
├── Url: string
├── ExpiresAt: datetime?
├── Confidence: enum            (High, Medium, Low)
├── Tags: string[]
├── Metadata: Dictionary
└── CreatedAt: datetime
```

---

## Data Flow: Opportunity Pipeline

The 8-stage pipeline from source to user, with a feedback loop.

```mermaid
graph LR
    subgraph Fetch["1. FETCH"]
        RSS["RSS Feeds"]
        Scrape["Web Scraping"]
        APIf["REST APIs"]
        Webhooks["Webhooks"]
        Browser["Playwright"]
    end

    Normalise["2. NORMALISE<br/><i>Raw → Opportunity model<br/>AUD, timestamps,<br/>confidence level</i>"]

    Dedup["3. DEDUPLICATE<br/><i>Title similarity,<br/>URL normalisation,<br/>product fingerprint</i>"]

    Match["4. MATCH<br/><i>User interests:<br/>keywords, categories,<br/>auto-learned weights</i>"]

    Score["5. SCORE & ENRICH<br/><i>ROI ranking +<br/>cashback route +<br/>coupon + card bonus</i>"]

    Store[("6. STORE<br/><i>Opportunities,<br/>PriceHistory,<br/>Earnings, AuditLog</i>")]

    Notify["7a. NOTIFY<br/><i>Apprise — actionable<br/>alerts with buy links</i>"]
    Dash["7b. DASHBOARD<br/><i>React — browse,<br/>filter, act</i>"]

    Learn["8. LEARN<br/><i>User Profiler:<br/>click = interested<br/>ignore = not interested</i>"]

    RSS --> Normalise
    Scrape --> Normalise
    APIf --> Normalise
    Webhooks --> Normalise
    Browser --> Normalise

    Normalise --> Dedup --> Match --> Score --> Store
    Store --> Notify
    Store --> Dash

    Notify -.->|"engagement tracking"| Learn
    Learn -.->|"updated interest weights"| Match

    style Fetch fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Normalise fill:#E8F5E9,stroke:#A5D6A7,color:#000
    style Dedup fill:#FFFDE7,stroke:#FFF59D,color:#000
    style Match fill:#FFE0B2,stroke:#FFCC80,color:#000
    style Score fill:#FFCDD2,stroke:#EF9A9A,color:#000
    style Store fill:#F3E5F5,stroke:#CE93D8,color:#000
    style Notify fill:#E0F2F1,stroke:#80DEEA,color:#000
    style Dash fill:#E0F2F1,stroke:#80DEEA,color:#000
    style Learn fill:#EDE7F6,stroke:#B39DDB,color:#000
```

---

## Deployment: Docker Compose

```mermaid
graph TB
    subgraph Host["User's Machine / VPS"]
        subgraph Docker["Docker Compose"]
            APIc["dealme-api<br/><i>ASP.NET Core 9<br/>+ Hangfire + Playwright</i>"]
            Web["dealme-web<br/><i>React SPA + Nginx</i>"]
            CDIOc["changedetection.io<br/><i>:5000</i>"]
            Apprisec["Apprise<br/><i>:8000</i>"]
            Wallosc["Wallos<br/><i>:8282</i>"]
            N8Nc["n8n (optional)<br/><i>:5678</i>"]
            SQLite[("SQLite (MVP)")]
            Postgres[("PostgreSQL (Prod)")]
        end
        subgraph Volumes["Docker Volumes"]
            V1["db-data"]
            V2["cdio-data"]
            V3["wallos-data"]
            V4["n8n-data"]
        end
    end

    External["External Services<br/><i>RSS, Scrape targets,<br/>Up Bank API,<br/>Notification channels</i>"]

    Web -->|":5001"| APIc
    APIc --> SQLite
    APIc -.-> Postgres
    APIc -->|":5000"| CDIOc
    APIc -->|":8000"| Apprisec
    APIc -->|":8282"| Wallosc
    APIc -->|":5678"| N8Nc
    CDIOc -->|"Webhooks"| APIc

    SQLite --> V1
    CDIOc --> V2
    Wallosc --> V3
    N8Nc --> V4

    APIc --> External
    CDIOc --> External
    Apprisec --> External

    style Host fill:#f9f9f9,stroke:#ccc,color:#000
    style Docker fill:#f0f0f0,stroke:#bbb,color:#000
    style Volumes fill:#f5f5f5,stroke:#ccc,color:#000
    style APIc fill:#E3F2FD,stroke:#90CAF9,color:#000
    style Web fill:#E8F5E9,stroke:#A5D6A7,color:#000
    style CDIOc fill:#FFFDE7,stroke:#FFF59D,color:#000
    style Apprisec fill:#FFCDD2,stroke:#EF9A9A,color:#000
    style Wallosc fill:#FFE0B2,stroke:#FFCC80,color:#000
    style N8Nc fill:#EDE7F6,stroke:#B39DDB,color:#000
    style External fill:#999,stroke:#888,color:#fff
```

### Ports Summary

| Container | Port | Protocol |
|---|---|---|
| dealme-api | 5001 | HTTP (REST API) |
| dealme-web | 80/443 | HTTP/S (Nginx) |
| changedetection.io | 5000 | HTTP (API + Webhooks) |
| Apprise | 8000 | HTTP (Notification API) |
| Wallos | 8282 | HTTP (Subscription API) |
| n8n | 5678 | HTTP (Workflow API) |
