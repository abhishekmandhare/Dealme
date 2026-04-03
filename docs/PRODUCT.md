# DealMe - Product Document

> **Tagline:** Your autonomous Australian money-making and savings engine.
>
> **Version:** 0.1.0 (Draft)
> **Last Updated:** 2026-04-03
> **Status:** Pre-Development

---

## Table of Contents

1. [Vision & Objectives](#1-vision--objectives)
2. [Target Audience](#2-target-audience)
3. [Product Principles](#3-product-principles)
4. [Use Cases](#4-use-cases)
5. [Australian Service Integrations](#5-australian-service-integrations)
6. [Architecture Overview](#6-architecture-overview)
7. [Technology Stack](#7-technology-stack)
8. [Open Source Dependencies](#8-open-source-dependencies)
9. [MVP Definition](#9-mvp-definition)
10. [Roadmap](#10-roadmap)
11. [Risk & Compliance](#11-risk--compliance)
12. [Appendix: Rejected Approaches](#appendix-a-rejected-approaches)

---

## 1. Vision & Objectives

### Vision

A unified, self-hosted platform that autonomously discovers, tracks, and optimises every legitimate avenue for Australians to earn and save money -- from cashback and deals to government rebates and passive income.

### Primary Objective

**Maximise guaranteed, low-risk earnings and savings** for Australian users through automation. The system should work autonomously once configured, requiring minimal ongoing user effort.

### Success Metrics

| Metric | Target |
|---|---|
| Annual value generated per active user | $2,000 - $8,000+ (combined earnings + savings) |
| Daily active effort required from user | < 5 minutes |
| Integrations available at launch (MVP) | 5-8 core services |
| Integrations available at v1.0 | 25+ services |
| System uptime (self-hosted) | 99%+ |
| False positive rate on deal alerts | < 5% |

---

## 2. Target Audience

### Primary Persona: "The Savvy Aussie"

- Australian resident (any state/territory)
- Comfortable with technology (can run Docker, configure apps)
- Wants to maximise earnings without a second job
- Values time -- wants automation over manual effort
- Already uses some combination of OzBargain, Cashrewards, Flybuys, etc.
- Frustrated by managing 15+ apps/sites separately

### Secondary Persona: "The Set-and-Forget Saver"

- Less technical, guided setup
- Primarily interested in deal alerts and cashback reminders
- Wants push notifications, not dashboards
- "Tell me when to act, and what to do"

---

## 3. Product Principles

### P1: Guaranteed Money Only

No speculation, no gambling, no crypto yield farming. Every channel must produce reliable, repeatable returns. If it has a risk of loss, it's out of scope.

### P2: Australian-First

Every feature, integration, and data source is designed for the Australian market. Currency is AUD. Services must be available in Australia. State/territory-specific rules (energy rebates, stamp duty, concessions) are first-class citizens.

### P3: Autonomous Operation

After initial setup, the system runs unattended. Scheduled jobs scrape deals, monitor prices, check for new bonuses, and send alerts. The user acts only when there's money to be made.

### P4: Decoupled Integrations

Each external service integration is an independent adapter. Adding, removing, or updating an integration must not affect the core system or other integrations. Adapters communicate through a standard interface.

### P5: Zero Tolerance for Errors

We are dealing with money. All financial tracking is idempotent and auditable. No auto-purchasing. No auto-spending. The system recommends actions; the user executes them. Every state change is logged.

### P6: Start Small, Scale Later

Single Docker Compose deployment on day one. Architecture supports horizontal scaling if needed later. SQLite for MVP, PostgreSQL for production. Monolith-first, microservices-ready.

### P7: Stand on Giants' Shoulders

Use proven open-source tools wherever possible. Only build what doesn't exist: the aggregation logic, the scoring engine, and the unified dashboard.

---

## 4. Use Cases

### UC1: Deal Discovery & Alerts

> *"I set my interests and get notified instantly when a matching deal appears."*

**Actor:** User
**Trigger:** New deal posted on monitored source
**Flow:**
1. System monitors OzBargain (RSS), Cheapies (RSS), Frugal Feeds (RSS) on a schedule
2. New deal is parsed and normalised into standard format
3. Matching engine compares deal against user's interest keywords and categories
4. If match found with positive score, alert is sent via configured notification channel
5. Deal is stored in database with metadata (source, price, votes, timestamp)

**Sources:** OzBargain, Cheapies, Frugal Feeds, TopBargains
**Integration Method:** RSS feeds (OzBargain has excellent per-category RSS)
**Notification:** Apprise (Discord, Telegram, email, SMS, 90+ channels)

**Acceptance Criteria:**
- Deals appear in system within 5 minutes of posting
- User can set keyword filters, category filters, minimum vote threshold
- Duplicate deals across sources are deduplicated
- Notifications include: title, price, source link, cashback route if applicable

---

### UC2: Cashback Optimiser

> *"Before I buy something, the app tells me the best cashback route and whether there's a coupon to stack."*

**Actor:** User
**Trigger:** User searches for a retailer/product, or a deal from UC1 triggers optimisation
**Flow:**
1. User enters retailer name or product
2. System checks current cashback rates on Cashrewards and ShopBack
3. System checks for active coupon codes (RetailMeNot AU, OzBargain coupons)
4. System checks user's credit card portfolio for category bonuses
5. System presents optimal stacking route with total savings percentage

**Example Output:**
```
Buy via ShopBack (8% cashback) 
+ Use code SAVE10 (10% off)
+ Pay with AmEx Platinum Edge (3x points on online shopping)
= Effective savings: ~18%
```

**Sources:** Cashrewards, ShopBack, RetailMeNot AU, OzBargain coupon section
**Integration Method:** Web scraping (rates change frequently)

**Acceptance Criteria:**
- Cashback rates are refreshed at least every 6 hours
- Coupon codes include community-reported validity status
- Credit card optimisation requires user to input their cards once during setup
- Shows comparison table: "Via Cashrewards: X% | Via ShopBack: Y% | Direct: 0%"

---

### UC3: Passive Earnings Dashboard

> *"I see a single dashboard showing all my passive earnings across every platform."*

**Actor:** User
**Trigger:** User opens dashboard or views daily/weekly digest
**Flow:**
1. System periodically checks configured passive income sources
2. Earnings data is aggregated into unified format (AUD)
3. Dashboard displays: today's earnings, this week, this month, all-time
4. Breakdown by source with trend graphs

**Tracked Sources:**
| Source | Data Point | Collection Method |
|---|---|---|
| Microsoft Rewards | Points balance & daily points | Playwright automation |
| Google Opinion Rewards | Credits earned | Manual input (no API) |
| Honeygain | Earnings (USD→AUD) | API/scrape dashboard |
| EarnApp | Earnings (USD→AUD) | API/scrape dashboard |
| Pawns.app | Earnings (USD→AUD) | API/scrape dashboard |
| Cashrewards | Pending cashback | Scrape account page |
| ShopBack | Pending cashback | Scrape account page |
| Survey earnings | Per-platform totals | Manual input with quick-entry |

**Acceptance Criteria:**
- Single unified view in AUD (auto-convert foreign currencies)
- Historical data stored for trend analysis
- Monthly summary email/notification: "You earned $X this month"
- No credentials stored for manual-input sources

---

### UC4: Bank & Card Bonus Tracker

> *"I see all available bank sign-up bonuses and credit card welcome offers, track requirements, and know when I'm eligible again."*

**Actor:** User
**Trigger:** New bonus detected, or user checks tracker
**Flow:**
1. System monitors Point Hacks, Finder, and bank pages for current offers
2. Offers are normalised: bonus amount, requirements, deadline, cooling-off period
3. User marks offers as: interested → applied → in progress → completed → bonus received
4. System tracks requirement progress (e.g., "3 of 5 card purchases made")
5. System calculates cooldown ("Eligible for ING bonus again: March 2027")

**Sources:**
| Source | Type | Integration |
|---|---|---|
| Point Hacks (pointhacks.com.au) | Card bonuses | RSS + scraping |
| Finder (finder.com.au) | Bank + card bonuses | Scraping |
| ING, Macquarie, ANZ, CBA, etc. | Direct bank offers | changedetection.io monitoring |

**Bank Bonuses Currently Tracked:**
- ING Orange Everyday: ~$100-150 (deposit $1K/mo + 5 purchases)
- Macquarie Transaction: ~$100 (periodic)
- Up Bank referral: $5-10 per referral
- Bankwest: ~$200 (periodic)
- ANZ Plus: competitive savings rate offers

**Credit Card Bonuses Currently Tracked:**
- AmEx Qantas Business Rewards: 150,000-200,000 QF points
- ANZ Frequent Flyer Platinum: 75,000-120,000 QF points
- AmEx Velocity Business: 100,000-150,000 Velocity points
- HSBC Platinum: $200-300 cashback
- Various others via Point Hacks feed

**Acceptance Criteria:**
- Offers updated daily
- Cooldown tracking per bank/issuer
- Requirements checklist per active application
- Estimated value in AUD (points converted at standard redemption rates)
- Alert when new high-value offer appears (threshold configurable)

---

### UC5: Government Rebate Checker

> *"I enter my state and basic details, and the app tells me which rebates I'm missing."*

**Actor:** User (one-time setup, periodic re-check)
**Trigger:** Initial setup, or annual review reminder
**Flow:**
1. User selects state/territory and answers basic eligibility questions
2. System matches against known rebates and concessions database
3. Presents list of applicable rebates with: value, how to claim, direct link
4. Tracks which ones user has claimed

**Rebates Database (by State):**

| Rebate | States | Value | Frequency |
|---|---|---|---|
| Energy Bill Relief Fund | All | $300+ | Annual (federal budget) |
| Power Saving Bonus | VIC | $250 | One-time (per period) |
| Electricity Rebate | QLD | $550+ | Annual |
| Low Income Household Rebate | NSW | $285+ | Annual |
| Cost of Living Concession | SA | $200+ | Annual |
| Household Electricity Credit | WA | Varies | Periodic |
| Private Health Insurance Rebate | All | 8-33% of premiums | Ongoing |
| Low Income Tax Offset (LITO) | All | Up to $700 | Annual (tax return) |
| Seniors Health Card benefits | All | Varies | Ongoing |
| Telecom concessions (Telstra/Optus) | All | ~$30/mo savings | Ongoing |
| Solar rebates (STCs) | All | Varies | One-time |
| First Home Owner Grant | All (varies) | $10,000-30,000 | One-time |
| Stamp duty concessions | All (varies) | Varies | One-time |

**Acceptance Criteria:**
- State-specific filtering
- Direct links to claim pages
- Annual reminder to re-check eligibility
- Data sourced from government websites (energy.gov.au, servicesaustralia.gov.au, state portals)

---

### UC6: Price Watch

> *"I add products I want and set target prices. The app alerts me when the price drops."*

**Actor:** User
**Trigger:** Price drops below user-defined target
**Flow:**
1. User adds product URL(s) or search term + target price
2. changedetection.io monitors the product page(s) on a schedule
3. When price drops below target, system triggers alert
4. Alert includes: current price, target price, savings amount, cashback route (UC2)
5. Price history is stored and graphed

**Monitored Retailers:**
- Amazon Australia (amazon.com.au)
- eBay Australia (ebay.com.au)
- JB Hi-Fi (jbhifi.com.au)
- The Good Guys (thegoodguys.com.au)
- Kogan (kogan.com)
- Harvey Norman (harveynorman.com.au)
- Officeworks (officeworks.com.au)
- Bunnings (bunnings.com.au)
- Chemist Warehouse (chemistwarehouse.com.au)

**Acceptance Criteria:**
- Price checks at configurable intervals (default: every 4 hours)
- Price history graph per product
- Alert combines with cashback data: "JB Hi-Fi dropped to $399 + 5% via Cashrewards = effective $379"
- Support for adding products via URL paste
- Powered by changedetection.io (self-hosted Docker container)

---

### UC7: Subscription Audit

> *"I see all my recurring subscriptions and get suggestions for cheaper alternatives."*

**Actor:** User
**Trigger:** User opens subscription view, or monthly audit reminder
**Flow:**
1. User enters subscriptions manually (or imports via bank transaction data if Open Banking connected)
2. System categorises subscriptions (streaming, telco, energy, insurance, software)
3. System compares against current market rates for each category
4. Presents: total monthly spend, savings opportunities, unused subscription flags
5. Alerts before free trial expirations

**Comparison Sources:**
- Telco/broadband: WhistleOut, Finder
- Energy: Energy Made Easy, Victorian Energy Compare
- Insurance: Compare the Market, iSelect
- Streaming: Manual comparison database

**Integration:** Wallos (self-hosted subscription tracker, Docker)

**Acceptance Criteria:**
- Total monthly/annual subscription cost visible at a glance
- "You could save $X/month by switching to [alternative]" suggestions
- Free trial expiry alerts (configurable: 3 days, 7 days before)
- Categories with colour-coded spending breakdown

---

### UC8: Referral Manager

> *"I manage all my referral links and get alerted when high-value referral promos launch."*

**Actor:** User
**Trigger:** New referral promo detected, or user shares a referral link
**Flow:**
1. System maintains database of Australian referral programs and current bonus amounts
2. User stores their personal referral links/codes
3. When a high-value referral promo launches, user is alerted
4. User tracks: link shared → person signed up → bonus received
5. System shows total referral earnings

**Active Referral Programs (Australian):**
| Service | Referral Bonus | Category |
|---|---|---|
| Up Bank | $5-10 per referral | Banking |
| ING | $100 (periodic) | Banking |
| Macquarie | $50-100 (periodic) | Banking |
| Belong | $25 credit | Telco |
| Aussie Broadband | $50 credit | Broadband |
| Circles.Life | $20-30 | Telco |
| Powershop | $75 credit | Energy |
| Energy Australia | $50-75 (periodic) | Energy |
| Stake | Free stock (up to $150) | Brokerage |
| SelfWealth | $10-20 credit | Brokerage |
| Raiz | $5 | Investing |
| Cashrewards | $10-20 | Cashback |
| ShopBack | $5-10 | Cashback |
| Uber/Uber Eats | $10-20 credit | Transport/Food |
| DoorDash | $10-20 | Food delivery |
| HelloFresh | $50-100 off first box | Meal kits |

**Acceptance Criteria:**
- Searchable database of current AU referral programs
- Personal referral link storage (encrypted)
- Track referral status: shared → pending → received
- Alert on new/increased referral bonuses
- Monthly referral earnings summary

---

### UC9: Class Action Monitor

> *"I get notified when class action registrations open that I might be eligible for."*

**Actor:** User
**Trigger:** New class action registration opens
**Flow:**
1. System monitors major Australian class action law firm websites
2. New actions are parsed: description, eligibility criteria, registration deadline, law firm
3. System matches against user profile (banks used, products owned, employers, shares held)
4. If potential match, user is alerted
5. User tracks: notified → registered → pending → payout received

**Monitored Sources:**
- Maurice Blackburn (mauriceblackburn.com.au/class-actions)
- Slater and Gordon (slatergordon.com.au/class-actions)
- Shine Lawyers (shine.com.au/service/class-actions)
- Phi Finney McDonald (phifinneymcdonald.com)
- Federal Court of Australia class actions registry

**Common AU Class Action Types:**
- Bank fee overcharging
- Superannuation underperformance
- Data breach compensation
- Product liability
- Employment underpayment / wage theft
- Shareholder class actions

**Integration Method:** changedetection.io monitoring of law firm class action pages

**Acceptance Criteria:**
- New class actions detected within 24 hours of posting
- Basic eligibility matching against user profile
- Registration deadline alerts
- Direct link to registration page
- Track status through to payout

---

### UC10: Smart Grocery Savings

> *"I see this week's best deals at Coles and Woolworths for items on my shopping list."*

**Actor:** User
**Trigger:** Weekly catalogue release (typically Wednesday), or user checks before shopping
**Flow:**
1. User maintains a shopping list of regular items
2. System ingests weekly Coles and Woolworths specials
3. Matches specials against shopping list
4. Highlights: half-price items, Flybuys/Everyday Rewards bonus offers, price-per-unit comparisons
5. Suggests optimal store split: "Buy X at Coles (half price), Y at Woolworths (10x points)"
6. Checks Too Good To Go for nearby surplus food bags

**Sources:**
- Coles/Woolworths digital catalogues (via scraping or Frugl data)
- Cheapies.com.au (everyday low-price deals)
- Too Good To Go app (surplus food, ~1/3 price)
- Frugal Feeds (fast food and restaurant deals)

**Acceptance Criteria:**
- Updated when new catalogues drop (Wednesday)
- Shopping list with price history per item
- "Best buy this week" recommendations
- Price per unit/kg comparison between stores
- Loyalty points optimisation: "This shop earns 3x Flybuys points at Coles"

---

## 5. Australian Service Integrations

### Integration Priority Matrix

#### Tier 1 - MVP (Launch)

| # | Service | Category | Integration Method | Est. Value/Year | Complexity |
|---|---|---|---|---|---|
| 1 | OzBargain | Deals | RSS feeds | High (savings) | Low |
| 2 | Cheapies | Deals | RSS feeds | Medium (savings) | Low |
| 3 | Cashrewards | Cashback | Web scraping | $300-1,500 | Medium |
| 4 | ShopBack | Cashback | Web scraping | $300-1,500 | Medium |
| 5 | Point Hacks | Card bonuses | RSS + scraping | $1,000-3,000 | Medium |
| 6 | Microsoft Rewards | Rewards | Playwright | $50-80 | Medium |
| 7 | changedetection.io | Price watch | Docker (self-hosted) | High (savings) | Low |
| 8 | Apprise | Notifications | API call | N/A (delivery) | Low |

#### Tier 2 - v1.1

| # | Service | Category | Integration Method | Est. Value/Year | Complexity |
|---|---|---|---|---|---|
| 9 | Flybuys | Loyalty | Scraping / manual | $200-600 | Medium |
| 10 | Everyday Rewards | Loyalty | Scraping / manual | $200-600 | Medium |
| 11 | Finder.com.au | Comparison | Scraping | N/A (data) | Medium |
| 12 | RetailMeNot AU | Coupons | Scraping | Medium (savings) | Medium |
| 13 | Frugal Feeds | Food deals | RSS feeds | Medium (savings) | Low |
| 14 | Wallos | Subscriptions | Docker (self-hosted) | $200-1,200 | Low |
| 15 | Honeygain | Passive income | API/scrape | $20-60 | Medium |
| 16 | EarnApp | Passive income | API/scrape | $20-60 | Medium |

#### Tier 3 - v1.2+

| # | Service | Category | Integration Method | Est. Value/Year | Complexity |
|---|---|---|---|---|---|
| 17 | Prolific | Surveys | Alert scraping | $200-600 | Medium |
| 18 | Pureprofile | Surveys | Alert scraping | $50-200 | Medium |
| 19 | Maurice Blackburn | Class actions | changedetection.io | Sporadic $5-1000+ | Low |
| 20 | Slater & Gordon | Class actions | changedetection.io | Sporadic $5-1000+ | Low |
| 21 | Too Good To Go | Food savings | Scraping | $200-500 | High |
| 22 | WhistleOut | Telco comparison | Scraping | $100-500 | Medium |
| 23 | Energy Made Easy | Energy comparison | Scraping | $200-800 | Medium |
| 24 | Prezzee | Gift card discounts | API | $100-500 | Medium |
| 25 | Up Bank | Banking | REST API (public!) | N/A (data) | Low |
| 26 | Open Banking (CDR) | Bank data | Standardised API | N/A (data) | High |
| 27 | Coles/Woolworths catalogues | Grocery savings | Scraping | $200-500 | High |
| 28 | Stake / Moomoo / Tiger | Brokerage bonuses | changedetection.io | $50-200 each | Low |

### Integration Interface Contract

Every integration adapter must implement:

```
IIntegrationAdapter
├── Id: string                    // Unique adapter identifier
├── Name: string                  // Human-readable name
├── Category: enum                // Deals, Cashback, Rewards, Banking, etc.
├── Schedule: CronExpression      // How often to poll
├── FetchAsync()                  // Pull new data from source
├── NormaliseAsync()              // Convert to standard DealMe format
├── HealthCheckAsync()            // Verify integration is working
└── GetStatusAsync()              // Current state (last run, errors, items found)
```

Standard output format:

```
Opportunity
├── Id: guid
├── Source: string               // "ozbargain", "cashrewards", etc.
├── Type: enum                   // Deal, Cashback, Bonus, Rebate, Earning
├── Title: string
├── Description: string
├── Value: decimal (AUD)         // Estimated value or savings
├── Url: string                  // Direct link to act
├── ExpiresAt: datetime?         // Deadline if applicable
├── Confidence: enum             // High, Medium, Low
├── Tags: string[]               // For matching against user interests
├── Metadata: Dictionary         // Source-specific extra data
└── CreatedAt: datetime
```

---

## 6. Architecture Overview

### System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER INTERFACES                          │
│                                                                 │
│  ┌──────────────────┐  ┌──────────────────────────────────────┐ │
│  │  Web Dashboard   │  │  Notifications (via Apprise)         │ │
│  │  (React/Blazor)  │  │  Discord, Telegram, Email, SMS,     │ │
│  │                  │  │  Slack, Push, 90+ channels           │ │
│  └────────┬─────────┘  └──────────────────┬───────────────────┘ │
└───────────┼────────────────────────────────┼────────────────────┘
            │                                │
            ▼                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                        CORE API (ASP.NET Core)                  │
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────────┐    │
│  │ Matching    │  │ Scoring     │  │ Notification         │    │
│  │ Engine      │  │ Engine      │  │ Service              │    │
│  │             │  │ (ROI/value) │  │ (Apprise client)     │    │
│  └─────────────┘  └─────────────┘  └──────────────────────┘    │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────────┐    │
│  │ Dedup       │  │ Audit       │  │ User Preferences     │    │
│  │ Service     │  │ Logger      │  │ Service              │    │
│  └─────────────┘  └─────────────┘  └──────────────────────┘    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ Job Scheduler (Hangfire)                                │    │
│  │ Manages all adapter polling schedules                   │    │
│  └─────────────────────────────────────────────────────────┘    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     ADAPTER LAYER                               │
│                                                                 │
│  Each adapter implements IIntegrationAdapter                    │
│  Each runs independently on its own schedule                    │
│  Each can be enabled/disabled without affecting others          │
│                                                                 │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────────┐   │
│  │ OzBargain │ │ Cashrew.  │ │ ShopBack  │ │ MS Rewards    │   │
│  │ (RSS)     │ │ (Scrape)  │ │ (Scrape)  │ │ (Playwright)  │   │
│  └───────────┘ └───────────┘ └───────────┘ └───────────────┘   │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────────┐   │
│  │ PointHacks│ │ Cheapies  │ │ Honeygain │ │ Govt Rebates  │   │
│  │ (Scrape)  │ │ (RSS)     │ │ (API)     │ │ (Static+Mon.) │   │
│  └───────────┘ └───────────┘ └───────────┘ └───────────────┘   │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────────┐   │
│  │ Finder    │ │ Wallos    │ │ Class Act.│ │ Up Bank       │   │
│  │ (Scrape)  │ │ (Docker)  │ │ (CDet.io) │ │ (REST API)    │   │
│  └───────────┘ └───────────┘ └───────────┘ └───────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     EXTERNAL TOOLS (Docker)                     │
│                                                                 │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │ changedetection.io   │  │ n8n (optional)                   │ │
│  │ Price monitoring     │  │ Complex workflow automation       │ │
│  │ Bank page monitoring │  │ Visual flow editor               │ │
│  │ Class action monitor │  │                                  │ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │ Wallos               │  │ Apprise                          │ │
│  │ Subscription tracker │  │ Notification gateway             │ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     DATA LAYER                                  │
│                                                                 │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │ SQLite (MVP)         │  │ PostgreSQL (Production)          │ │
│  │                      │  │                                  │ │
│  │ Tables:              │  │ Same schema, just swap provider  │ │
│  │ - Opportunities      │  │                                  │ │
│  │ - UserPreferences    │  │                                  │ │
│  │ - Earnings           │  │                                  │ │
│  │ - PriceHistory       │  │                                  │ │
│  │ - Referrals          │  │                                  │ │
│  │ - Subscriptions      │  │                                  │ │
│  │ - AuditLog           │  │                                  │ │
│  │ - BonusTracking      │  │                                  │ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
External Source → Adapter (fetch + normalise) → Core Engine (match + score + dedup)
    → Database (store) → Notification Service (alert user) → Dashboard (display)
```

### Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Monolith vs microservices | Monolith-first | Simpler to develop, deploy, debug. Split later if needed |
| Database | SQLite → PostgreSQL | SQLite for zero-config MVP; EF Core makes switching trivial |
| Frontend | React (or Blazor) | TBD based on team preference |
| Job scheduling | Hangfire | Native .NET, built-in dashboard, retry/queue support |
| Notifications | Apprise | 90+ channels via one API; no need to build notification infra |
| Price monitoring | changedetection.io | 31K stars, battle-tested, Docker-ready |
| Workflow automation | n8n (optional) | Only needed for complex multi-step flows |
| Browser automation | Playwright for .NET | Microsoft-maintained, sandboxed, no shared cookies |

---

## 7. Technology Stack

### Core Application

| Component | Technology | Purpose |
|---|---|---|
| Backend API | ASP.NET Core 9 | REST API, business logic, job scheduling |
| Frontend | React + TypeScript (or Blazor) | Dashboard UI |
| ORM | Entity Framework Core | Database access |
| Job Scheduler | Hangfire | Background job scheduling and management |
| Browser Automation | Playwright for .NET | Scraping JS-heavy sites, rewards automation |
| HTTP Scraping | AngleSharp / HtmlAgilityPack | Parsing HTML from scraped pages |
| RSS Parsing | System.ServiceModel.Syndication | OzBargain, Cheapies, etc. |
| Notifications | Apprise (via HTTP API) | Multi-channel notification delivery |

### Infrastructure (Docker)

| Service | Image | Purpose |
|---|---|---|
| changedetection.io | dgtlmoon/changedetection.io | Price monitoring, page change detection |
| Wallos | bellamy/wallos | Subscription tracking |
| Apprise | caronc/apprise | Notification gateway |
| n8n (optional) | n8nio/n8n | Complex workflow automation |
| PostgreSQL | postgres:17 | Production database |

### Development Tools

| Tool | Purpose |
|---|---|
| Docker Compose | Local development and deployment |
| GitHub Actions | CI/CD |
| xUnit | Unit and integration testing |
| Playwright Test | E2E testing of scraping adapters |

---

## 8. Open Source Dependencies

### Direct Dependencies (Self-Hosted)

| Project | Stars | License | Role in DealMe |
|---|---|---|---|
| [changedetection.io](https://github.com/dgtlmoon/changedetection.io) | ~31K | Apache 2.0 | Price monitoring, page change alerts |
| [Wallos](https://github.com/ellite/Wallos) | ~7.6K | GPL-3.0 | Subscription tracking |
| [Apprise](https://github.com/caronc/apprise) | ~12K+ | BSD-2 | Notification delivery (90+ channels) |
| [n8n](https://github.com/n8n-io/n8n) | ~182K | Fair-code | Optional: complex workflow automation |

### Libraries / Frameworks

| Project | Role |
|---|---|
| [Playwright for .NET](https://github.com/microsoft/playwright-dotnet) | Browser automation for JS-heavy scraping |
| [AngleSharp](https://github.com/AngleSharp/AngleSharp) | HTML parsing |
| [Hangfire](https://github.com/HangfireIO/Hangfire) | Background job scheduling |
| [Entity Framework Core](https://github.com/dotnet/efcore) | ORM / database access |

### Reference Projects (Architecture Inspiration)

| Project | Stars | What We Learn From It |
|---|---|---|
| [Scrapy](https://github.com/scrapy/scrapy) | ~61K | Scraping pipeline patterns (fetch → parse → filter → store) |
| [Huginn](https://github.com/huginn/huginn) | ~49K | Agent-based automation architecture |
| [Maybe Finance](https://github.com/maybe-finance/maybe) | ~54K | Personal finance dashboard UX |
| [Actual Budget](https://github.com/actualbudget/actual) | ~26K | Local-first finance app patterns |
| [MS-Rewards-Script](https://github.com/TheNetsky/Microsoft-Rewards-Script) | ~735 | Microsoft Rewards automation approach |

---

## 9. MVP Definition

### MVP Scope (v0.1)

**Goal:** A working system that finds deals, monitors prices, and sends notifications autonomously.

**Included Use Cases:**
- UC1: Deal Discovery (OzBargain + Cheapies RSS)
- UC6: Price Watch (changedetection.io)
- Notifications via Apprise (at least Discord + email)
- Basic web dashboard (deal feed, price watches, settings)

**Included Integrations:**
1. OzBargain (RSS)
2. Cheapies (RSS)
3. changedetection.io (Docker, webhooks)
4. Apprise (notifications)

**Included Infrastructure:**
- Docker Compose with all services
- SQLite database
- Hangfire job dashboard
- Basic React dashboard

**Explicitly NOT in MVP:**
- Cashback scraping (UC2)
- Passive earnings tracking (UC3)
- Bank/card bonus tracking (UC4)
- Government rebates (UC5)
- Subscription audit (UC7)
- Referral manager (UC8)
- Class action monitor (UC9)
- Grocery savings (UC10)
- User authentication (single-user, self-hosted)
- Mobile app

### MVP Success Criteria

- [ ] Docker Compose starts all services with one command
- [ ] OzBargain deals appear in dashboard within 5 minutes of posting
- [ ] User can set keyword filters for deal matching
- [ ] User can add price watch URLs via dashboard
- [ ] Price drop alerts delivered via Discord/Telegram/email
- [ ] Deal match alerts delivered via Discord/Telegram/email
- [ ] Deals are deduplicated across sources
- [ ] System runs 24/7 unattended after initial setup

---

## 10. Roadmap

### Phase 1: Foundation (MVP) -- v0.1

- Project scaffolding (ASP.NET Core, React, Docker Compose)
- Adapter interface and plugin architecture
- OzBargain + Cheapies RSS adapters
- changedetection.io integration
- Apprise notification integration
- Basic dashboard (deal feed, price watch, settings)
- Hangfire job scheduling

### Phase 2: Cashback & Bonuses -- v0.2

- Cashrewards adapter (scraping)
- ShopBack adapter (scraping)
- Cashback optimiser (UC2)
- Point Hacks adapter (RSS + scraping)
- Bank/card bonus tracker (UC4)
- Coupon integration (RetailMeNot AU)

### Phase 3: Passive Income & Tracking -- v0.3

- Microsoft Rewards automation adapter
- Passive earnings dashboard (UC3)
- Bandwidth sharing tracking (Honeygain, EarnApp)
- Survey platform alerts (Prolific, Pureprofile)
- Earnings history and trend graphs

### Phase 4: Savings Optimisation -- v0.4

- Government rebate checker (UC5)
- Subscription audit with Wallos integration (UC7)
- Referral manager (UC8)
- Energy/telco/insurance comparison links

### Phase 5: Advanced Features -- v1.0

- Class action monitor (UC9)
- Smart grocery savings (UC10)
- Up Bank API integration
- Open Banking (CDR) integration (requires accreditation)
- n8n integration for custom user workflows
- Mobile-responsive dashboard
- Multi-user support (optional)

### Phase 6: Future -- v1.x+

- Mobile app (React Native or MAUI)
- AI-powered deal scoring ("Is this actually a good deal?")
- Community features (share deals, referral circles)
- Automated tax reporting for earnings
- Kubernetes deployment option

---

## 11. Risk & Compliance

### Legal Compliance

| Area | Approach |
|---|---|
| Web scraping | Respect robots.txt, rate limit all scrapers, cache aggressively, no login-wall bypassing |
| Terms of Service | Flag integrations that may conflict with ToS (e.g., MS Rewards automation). User acknowledges risk |
| Financial advice | DealMe does NOT provide financial advice. It presents information. All actions taken by the user |
| Privacy (Australian Privacy Act) | Self-hosted = user's data stays on their machine. No data leaves to our servers (there are none) |
| Tax obligations | Remind users that earnings (cashback, referrals, bandwidth) may be assessable income. Not tax advice |
| Consumer Data Right (CDR) | Open Banking integration requires formal accreditation as a data recipient. Deferred to Phase 5+ |

### Technical Risks

| Risk | Mitigation |
|---|---|
| Site layout changes break scrapers | Health checks per adapter; alert user when adapter fails; adapters are independently deployable |
| Rate limiting / IP blocking | Respectful scraping intervals; configurable delays; optional proxy support |
| changedetection.io goes down | System degrades gracefully; deal discovery still works via RSS adapters |
| Stale data leading to wrong decisions | All opportunities include timestamp and confidence level; expired items auto-archived |
| Data loss | SQLite WAL mode for crash safety; configurable backup schedule |

### Security

| Concern | Approach |
|---|---|
| Credentials for external services | Encrypted at rest using ASP.NET Core Data Protection API; never logged; never sent to any LLM |
| Browser automation (Playwright) | Always runs in isolated browser context; no shared cookies/sessions; no access to user's real browser |
| Self-hosted exposure | Binds to localhost by default; reverse proxy guide provided for remote access |
| Notification tokens (Discord, Telegram) | Stored in separate encrypted config; rotatable |

### ToS Risk Matrix

| Integration | ToS Risk | Notes |
|---|---|---|
| OzBargain RSS | **None** | RSS is intended for consumption |
| Cashrewards scraping | **Low** | Reading public cashback rates |
| ShopBack scraping | **Low** | Reading public cashback rates |
| Microsoft Rewards automation | **High** | Automation explicitly against ToS; account ban risk. User opt-in with warning |
| Honeygain/EarnApp | **None** | Legitimate passive income; just tracking earnings |
| Bank page monitoring | **None** | Reading publicly available offers |
| Class action monitoring | **None** | Reading publicly available legal information |

---

## Appendix A: Rejected Approaches

### OpenClaw (AI Agent Platform)

**Evaluated:** April 2026
**Decision:** Rejected as core infrastructure

**Reasons:**
1. **156 security advisories** including CVSS 9.9 critical vulnerabilities
2. **Supply chain attack** (ClawHavoc): 2,400 malicious skills uploaded to marketplace
3. **Plaintext credential storage** by default -- unacceptable for financial use case
4. **Sandbox escape bugs** -- child processes bypass restrictions
5. **Prompt injection risk** -- AI agent browsing financial sites creates unacceptable attack surface
6. **One-click RCE** (CVE-2026-25253) via WebSocket hijacking, with 21,000+ instances exposed

**What We Took From It:**
- Plugin/skill architecture pattern (our adapter interface is inspired by it)
- Multi-channel notification routing concept
- Cron + webhook scheduling approach

### Huginn / n8n as Primary Orchestrator

**Evaluated:** April 2026
**Decision:** n8n retained as optional tool, not primary orchestrator

**Reasons:**
- Adds unnecessary complexity for most use cases
- Hangfire handles scheduling natively in .NET
- n8n useful only for complex multi-step workflows that justify visual editor
- Huginn is Ruby-based, doesn't fit .NET stack

---

*This is a living document. Updated as product decisions are made.*
