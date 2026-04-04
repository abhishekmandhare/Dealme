# DealMe - Product Document

> **Tagline:** Your autonomous Australian money-making and savings engine.
>
> **Version:** 0.1.0 (Draft)
> **Last Updated:** 2026-04-04
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
11. [Bank Connection & Data Access](#11-bank-connection--data-access)
12. [Risk & Compliance](#12-risk--compliance)
13. [Appendix: Rejected Approaches](#appendix-a-rejected-approaches)

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
- Already uses some combination of OzBargain, TopCashback, Flybuys, etc.
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

### P8: Minimise Toil

Every feature is designed to reduce user effort. If the system can do it automatically, the user should never have to. Features are built in three tiers:

| Tier | User Effort | Description | Example |
|---|---|---|---|
| **Tier 1: Fully Autonomous** | Zero clicks after setup | System runs entirely on its own | RSS deal scanning, price monitoring, class action alerts |
| **Tier 2: One-Click Action** | System does 95%, user confirms | Pre-computed recommendation, user just approves | "Cashback available — click to activate" |
| **Tier 3: Guided Setup** | One-time effort, then autonomous | User configures once, system takes over | Connect bank API, set interests, link reward accounts |

**Design rules:**
- Smart defaults over configuration — start with zero config, progressively profile
- Auto-detect interests from user behaviour rather than asking
- Never alert without a clear action — every notification answers "what should I do?"
- Measure every feature by: **dollars earned or saved per minute of user effort**

---

## 4. Use Cases

### UC1: Deal Discovery & Alerts

> *"Deals that matter to me just show up — I never browse deal sites manually."*

**Automation Tier:** Tier 1 (Fully Autonomous) → learns into Tier 2 (One-Click Action)

**Actor:** System (autonomous), User (acts on alerts)
**Trigger:** New deal posted on monitored source

**Flow:**
1. System monitors OzBargain (RSS), Cheapies (RSS), Frugal Feeds (RSS) on a schedule
2. New deal is parsed and normalised into standard format
3. Matching engine compares deal against user's interest profile
4. Scoring engine ranks by value, cashback availability, and historical engagement
5. High-confidence matches trigger immediate notification with actionable link
6. Notification includes pre-computed cashback route (UC2) — user clicks one link to buy optimally
7. Deal is stored in database; user's click/ignore behaviour refines future matching

**Smart Defaults (zero config start):**
- Initially sends top-voted deals across all categories (OzBargain 25+ votes)
- Learns interests from which notifications the user clicks vs ignores
- After ~2 weeks of passive learning, auto-narrows to relevant categories
- User can accelerate by picking categories during optional onboarding

**Sources:** OzBargain, Cheapies, Frugal Feeds, TopBargains
**Integration Method:** RSS feeds (OzBargain has excellent per-category RSS)
**Notification:** Apprise (Discord, Telegram, email, SMS, 90+ channels)

**Acceptance Criteria:**
- Deals appear in system within 5 minutes of posting
- Works immediately with zero configuration (smart defaults)
- Interest profile auto-refines from notification engagement (click = interested, ignore = not)
- Duplicate deals across sources are deduplicated
- Notifications include: title, price, source link, **best cashback route pre-computed**
- User never needs to visit OzBargain/Cheapies directly

---

### UC2: Cashback Optimiser

> *"Every deal alert already tells me the best cashback route — I just click one link."*

**Automation Tier:** Tier 1 (auto-computed) + Tier 2 (one-click activation)

**Actor:** System (computes routes automatically), User (clicks optimised link)
**Trigger:** Any opportunity from UC1/UC6/UC10 involving a purchase, OR user manually searches a retailer

**Flow:**
1. System continuously scrapes cashback rates from TopCashback and ShopBack (background job)
2. System maintains a coupon database from RetailMeNot AU and OzBargain coupon section
3. When any deal/price alert fires, the cashback route is **pre-computed and embedded in the notification**
4. If user has entered their credit cards during setup (Tier 3, optional), card bonus is factored in
5. Notification includes a single optimised link — user clicks it and buys
6. System tracks whether cashback was likely earned (via bank transaction matching if connected)

**Example Notification:**
```
🔔 JB Hi-Fi: Sony WH-1000XM5 — $299 (was $449)
   Best route: ShopBack (8% = $23.92 back)
   Coupon: JBSAVE5 ($5 off, verified 2d ago)
   Card bonus: AmEx 3x points on online
   → Effective price: $270.08  [Buy Now →]
```

**Sources:** TopCashback, ShopBack, RetailMeNot AU, OzBargain coupon section
**Integration Method:** Web scraping (rates change frequently)

**Acceptance Criteria:**
- Cashback rates refreshed at least every 6 hours (background, no user action)
- Cashback route **automatically bundled** into every deal/price notification — user never computes this manually
- One-click buy link in every notification
- Credit card optimisation optional (Tier 3 setup: enter cards once)
- Coupon codes include community-reported validity status and age
- Fallback: if no cashback route exists, notification says so explicitly

---

### UC3: Passive Earnings Dashboard

> *"I never check individual platforms — a weekly digest tells me exactly what I've earned everywhere."*

**Automation Tier:** Tier 1 (auto-collection) + Tier 3 (one-time account linking)

**Actor:** System (collects earnings automatically), User (reads digest)
**Trigger:** Scheduled collection jobs (background), weekly/monthly digest notification

**Flow:**
1. System periodically scrapes/polls all configured passive income sources (no user action)
2. Earnings data is auto-aggregated into unified AUD format (foreign currencies auto-converted)
3. Weekly digest notification sent: total earned, breakdown by source, trend vs last week
4. Dashboard available for deep-dive but **not required** — the digest is the primary interface
5. System auto-detects stalled sources (e.g., Honeygain offline for 3 days) and alerts user

**Tracked Sources:**
| Source | Data Point | Collection Method | User Effort |
|---|---|---|---|
| Microsoft Rewards | Points balance & daily tasks | Playwright auto-completes daily tasks | Tier 1: zero (auto-earns) |
| Honeygain | Earnings (USD→AUD) | API/scrape dashboard | Tier 3: install once, then auto |
| EarnApp | Earnings (USD→AUD) | API/scrape dashboard | Tier 3: install once, then auto |
| Pawns.app | Earnings (USD→AUD) | API/scrape dashboard | Tier 3: install once, then auto |
| TopCashback | Pending cashback | Scrape account page | Tier 3: link once, then auto |
| ShopBack | Pending cashback | Scrape account page | Tier 3: link once, then auto |
| Google Opinion Rewards | Credits earned | Manual input (no API available) | Quick-entry prompt in digest |
| Survey earnings | Per-platform totals | Manual input with quick-entry | Quick-entry prompt in digest |

**Toil Reduction:**
- Microsoft Rewards daily tasks are **auto-completed by Playwright** — user earns points without lifting a finger
- Only 2 sources (Google Opinion Rewards, surveys) require manual input — digest includes a quick-entry prompt: "Earned anything on surveys this week? Reply with amount"
- Stale source detection: if a passive source stops reporting, system alerts user to check it

**Acceptance Criteria:**
- Single unified view in AUD (auto-convert foreign currencies)
- Weekly digest notification is the primary interface — dashboard is optional
- Historical data stored for trend analysis
- Auto-detection of stalled/broken sources with fix suggestions
- Monthly summary: "You earned $X this month — up/down Y% vs last month"
- Manual-input sources limited to absolute minimum; prompt for input via notification

---

### UC4: Bank & Card Bonus Tracker

> *"The system finds bonuses, tracks my progress automatically, and warns me before I miss a deadline."*

**Automation Tier:** Tier 1 (discovery + monitoring) + Tier 2 (progress tracking with bank connection)

**Actor:** System (discovers and monitors), User (decides which bonuses to pursue)
**Trigger:** New bonus detected, or active bonus deadline approaching

**Flow:**
1. System monitors Point Hacks, Finder, and bank pages for current offers (Tier 1, autonomous)
2. Offers are normalised: bonus amount, requirements, deadline, cooling-off period
3. High-value new offers trigger alert: "New ING bonus: $150 for $1K deposit + 5 purchases in 60 days — [Start tracking →]"
4. User clicks to track (Tier 2, one-click) — system creates requirement checklist automatically
5. If bank API connected (Up Bank / CDR), system **auto-monitors progress** against requirements
6. Proactive nudges: "ING bonus: 3/5 purchases done, 12 days remaining — you're on track" or "WARNING: 2/5 purchases, only 3 days left"
7. System auto-calculates cooldown periods: "Eligible for ING bonus again: March 2027"
8. When cooldown expires, system auto-alerts: "You're eligible for ING bonus again — new offer available"

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

**Toil Reduction:**
- User never browses Point Hacks or Finder manually — system surfaces relevant bonuses
- Progress tracking is automatic when bank API connected; manual fallback is a simple checklist
- Proactive warnings prevent missed deadlines (the #1 way people lose bonus value)
- Cooldown re-eligibility alerts mean users never forget to re-apply

**Acceptance Criteria:**
- Offers updated daily (autonomous)
- One-click to start tracking a bonus
- Auto-progress monitoring via bank API (or manual checklist fallback)
- Proactive deadline warnings at 75%, 50%, 25% time remaining
- Cooldown tracking per bank/issuer with re-eligibility alerts
- Estimated value in AUD (points converted at standard redemption rates)

---

### UC5: Government Rebate Checker

> *"I told the system my state once — it found $1,200 in rebates I didn't know about and reminds me when new ones open."*

**Automation Tier:** Tier 3 (one-time profile setup) → Tier 1 (autonomous monitoring)

**Actor:** System (monitors rebate changes), User (claims rebates via provided links)
**Trigger:** Initial profile setup, new rebate detected, annual re-check, or budget announcement

**Flow:**
1. User selects state/territory and answers basic eligibility questions once (Tier 3 setup, ~2 minutes)
2. System immediately matches against known rebates database and presents actionable list
3. Each rebate shows: value, one-line eligibility summary, **direct claim link**, deadline
4. System monitors government websites via changedetection.io for new/changed rebates (Tier 1, autonomous)
5. When federal/state budget announced, system auto-scans for new rebates matching user profile
6. Annual reminder to re-check eligibility (circumstances may change)
7. System tracks claimed vs unclaimed — shows "unclaimed money on the table: $X"

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

**Toil Reduction:**
- 2-minute setup, then fully autonomous
- User never needs to search government websites
- Budget announcements auto-scanned — user is first to know about new rebates
- "Unclaimed money" counter creates urgency without requiring the user to remember

**Acceptance Criteria:**
- State-specific filtering from one-time profile
- Direct claim links (user clicks, lands on government claim page)
- Autonomous monitoring for new/changed rebates via changedetection.io
- Annual re-check reminder with updated eligibility
- "Unclaimed value" summary: total $ user is leaving on the table
- Data sourced from government websites (energy.gov.au, servicesaustralia.gov.au, state portals)

---

### UC6: Price Watch

> *"I paste a URL once. The system watches the price, and when it drops, the alert already includes the cheapest way to buy it."*

**Automation Tier:** Tier 3 (paste URL once) → Tier 1 (autonomous monitoring) → Tier 2 (one-click buy)

**Actor:** System (monitors prices, computes optimal purchase route), User (pastes URL, acts on alerts)
**Trigger:** Price drops below user-defined target (or auto-suggested target based on price history)

**Flow:**
1. User pastes product URL — system auto-detects retailer, product name, and current price
2. System suggests a target price based on historical lows (if available) — user can adjust or accept
3. changedetection.io monitors the product page on a schedule (Tier 1, autonomous)
4. When price drops below target, alert fires with **pre-computed optimal purchase route**:
   - Best cashback platform and rate (UC2)
   - Active coupon codes
   - Card bonus if applicable
   - Effective final price after all savings
5. Alert includes one-click buy link through best cashback route
6. Price history stored and graphed; system learns seasonal patterns over time

**Auto-Watch (future):** When bank API connected, system can auto-detect repeat purchases and suggest price watches for frequently bought items.

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

**Toil Reduction:**
- Paste a URL — that's it. No manual price entry, no retailer selection, no target guessing
- Target price auto-suggested from history (user can override)
- Alert is actionable: one-click buy link with cashback pre-applied
- User never needs to check the price themselves

**Acceptance Criteria:**
- Add product via URL paste — auto-detect retailer, name, price
- Auto-suggest target price from historical data
- Price checks at configurable intervals (default: every 4 hours)
- Price history graph per product
- Alert combines price drop + cashback + coupon into single actionable notification
- One-click buy link through optimal cashback route
- Powered by changedetection.io (self-hosted Docker container)

---

### UC7: Subscription Audit

> *"The system found 3 subscriptions I forgot about and a phone plan $20/month cheaper — I didn't even ask."*

**Automation Tier:** Tier 1 (auto-discovery with bank API) or Tier 3 (manual entry once) → Tier 1 (autonomous monitoring)

**Actor:** System (discovers, monitors, compares), User (acts on switch recommendations)
**Trigger:** New subscription detected, cheaper alternative found, or free trial expiring

**Flow:**
1. **With bank API (ideal):** System auto-discovers recurring charges from transaction history — no manual entry
2. **Without bank API:** User enters subscriptions via Wallos, or imports a bank statement CSV — one-time effort
3. System auto-categorises subscriptions (streaming, telco, energy, insurance, software)
4. System periodically compares each subscription against current market rates (autonomous)
5. When cheaper alternative found: "Your Telstra plan is $65/mo — Belong offers same data for $45/mo. [Switch →]"
6. Auto-detects subscriptions user may have forgotten (charged but never used/logged in)
7. Free trial expiry alerts fire automatically — "Netflix trial ends in 3 days. Keep or cancel? [Keep] [Cancel link →]"

**Comparison Sources:**
- Telco/broadband: WhistleOut, Finder
- Energy: Energy Made Easy, Victorian Energy Compare
- Insurance: Compare the Market, iSelect
- Streaming: Manual comparison database

**Integration:** Wallos (self-hosted subscription tracker, Docker)

**Toil Reduction:**
- With bank API: zero manual entry — subscriptions auto-discovered from transactions
- Without bank API: enter once in Wallos, system handles everything after
- User never needs to comparison-shop — system does it and presents the switch recommendation
- "Forgotten subscription" detection catches money leaks the user didn't know about
- Trial expiry alerts include a direct cancel link — one click to stop unwanted charges

**Acceptance Criteria:**
- Auto-discovery of subscriptions from bank transactions (when connected)
- Total monthly/annual subscription cost visible at a glance
- Proactive "switch and save" recommendations with direct links
- Forgotten/unused subscription detection
- Free trial expiry alerts with one-click cancel link (3 and 7 days before)
- Categories with colour-coded spending breakdown

---

### UC8: Referral Manager

> *"When a service I use launches a referral bonus, I get alerted with my link ready to share — zero effort."*

**Automation Tier:** Tier 3 (enter links once) → Tier 1 (autonomous monitoring + auto-embed)

**Actor:** System (monitors programs, embeds links), User (shares when alerted)
**Trigger:** New/increased referral bonus detected for a service the user has a link for

**Flow:**
1. System maintains and auto-updates database of Australian referral programs and current bonus amounts (Tier 1)
2. User enters their personal referral links/codes once per service (Tier 3 setup)
3. When a high-value referral promo launches for a linked service, user is alerted with **their link pre-loaded**: "ING referral bonus increased to $100 — share your link: [Copy →]"
4. System auto-embeds user's referral links into relevant deal notifications — e.g., when recommending Up Bank in UC4, user's Up referral link is included
5. If bank API connected, system auto-detects when a referral bonus has been received
6. System tracks: link shared → person signed up → bonus received → total earnings

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
| TopCashback | $10-20 | Cashback |
| ShopBack | $5-10 | Cashback |
| Uber/Uber Eats | $10-20 credit | Transport/Food |
| DoorDash | $10-20 | Food delivery |
| HelloFresh | $50-100 off first box | Meal kits |

**Toil Reduction:**
- Enter referral links once — system handles the rest
- Referral links auto-embedded into relevant notifications across other use cases
- User never needs to check if a referral promo is running — system monitors and alerts
- Bonus receipt auto-detected via bank API when connected

**Acceptance Criteria:**
- Auto-updated database of current AU referral programs
- Personal referral link storage (encrypted at rest)
- Referral links auto-embedded into cross-UC notifications where relevant
- Alert on new/increased bonuses with user's link pre-loaded and ready to share
- Auto-detect bonus receipt via bank API (or manual confirmation fallback)
- Monthly referral earnings summary

---

### UC9: Class Action Monitor

> *"I got $340 from a bank fee class action I didn't even know existed — the system found it, matched it, and told me to register."*

**Automation Tier:** Tier 1 (autonomous discovery + matching) → Tier 2 (one-click registration)

**Actor:** System (discovers and matches), User (registers via provided link)
**Trigger:** New class action detected that matches user profile

**Flow:**
1. System monitors major Australian class action law firm websites via changedetection.io (Tier 1, autonomous)
2. New actions auto-parsed: description, eligibility criteria, registration deadline, law firm
3. System auto-matches against user profile — banks used, products owned, employers, super fund, shares held
4. If potential match: "Class action against [Bank] for fee overcharging — you bank with them. Register by [date]. [Register →]"
5. Notification includes: eligibility likelihood (high/medium/low), estimated payout range, direct registration link
6. System tracks: notified → registered → pending → payout received
7. Deadline reminders sent if user hasn't registered yet: "Registration closes in 7 days"

**Profile Matching (auto-enriched):**
- Banks/cards: auto-populated from UC4 and bank API connections
- Super fund: entered once during setup
- Employers: entered once (for wage theft / underpayment actions)
- Products/services: auto-built from subscription data (UC7) and purchase history
- Shares: entered once (for shareholder class actions)

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

**Toil Reduction:**
- User never needs to check law firm websites — system monitors all of them
- Profile auto-enriched from other use cases (banks from UC4, subscriptions from UC7)
- Eligibility matching is automatic — user only sees actions relevant to them
- Deadline reminders prevent missed registrations (free money left on the table)

**Acceptance Criteria:**
- New class actions detected within 24 hours of posting
- Auto-matching against user profile with confidence level
- Direct registration link in every alert
- Deadline reminder if user hasn't registered (7 days and 1 day before)
- Track status through to payout
- Profile auto-enriched from other UC data — minimal manual entry

---

### UC10: Smart Grocery Savings

> *"Every Wednesday I get a message: 'Your regular items are on special this week — buy coffee at Coles (half price), buy nappies at Woolies (10x points).'"*

**Automation Tier:** Tier 3 (build shopping list once) → Tier 1 (autonomous weekly alerts)

**Actor:** System (monitors catalogues, matches, optimises), User (shops with optimised list)
**Trigger:** Weekly catalogue release (typically Wednesday) — fully automatic

**Flow:**
1. User builds a shopping list of regular items once (or system auto-builds from bank transaction history if connected)
2. System auto-ingests weekly Coles and Woolworths specials every Wednesday (Tier 1, autonomous)
3. Auto-matches specials against shopping list
4. Wednesday evening notification: optimised shopping plan for the week
   - Which items are on special and where
   - Optimal store split: "Buy X at Coles (half price), Y at Woolworths (10x points)"
   - Loyalty points optimisation: which store gives bonus points this week
   - Cashback route if available for online grocery orders
5. System learns purchase patterns over time — auto-adds frequently bought items to list
6. Checks Too Good To Go for nearby surplus food bags and alerts if good value

**Auto-List Building (with bank API):**
- System analyses grocery transaction history to identify regular purchase categories
- Suggests items to add to the watch list: "You spend ~$15/week on coffee — want to track specials?"
- Over time, the shopping list is fully auto-maintained

**Sources:**
- Coles/Woolworths digital catalogues (via scraping or Frugl data)
- Cheapies.com.au (everyday low-price deals)
- Too Good To Go app (surplus food, ~1/3 price)
- Frugal Feeds (fast food and restaurant deals)

**Toil Reduction:**
- Set up list once (or let bank API auto-build it) — weekly alerts are fully automatic
- User never browses catalogues — system does it and presents only relevant specials
- Store-split optimisation done for you — no mental math about where to shop
- System learns and auto-adds frequent purchases over time

**Acceptance Criteria:**
- Auto-ingest catalogues every Wednesday (no user action)
- Shopping list auto-builds from purchase history (or manual setup)
- Weekly notification with optimised shopping plan
- "Best buy this week" recommendations with store-split
- Price per unit/kg comparison between stores
- Loyalty points optimisation included in recommendations
- System learns purchase patterns and auto-suggests list additions

---

## 5. Australian Service Integrations

### Integration Priority Matrix

#### Tier 1 - MVP (Launch)

| # | Service | Category | Integration Method | Est. Value/Year | Complexity |
|---|---|---|---|---|---|
| 1 | OzBargain | Deals | RSS feeds | High (savings) | Low |
| 2 | Cheapies | Deals | RSS feeds | Medium (savings) | Low |
| 3 | TopCashback | Cashback | Web scraping | $300-1,500 | Medium |
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
├── Source: string               // "ozbargain", "topcashback", etc.
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

- TopCashback adapter (scraping)
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

## 11. Bank Connection & Data Access

### Core Principle

**DealMe never stores bank credentials and never initiates transactions.** Bank access is strictly read-only observation to enhance automation. The app works fully without any bank connection — connecting a bank is an optional upgrade that unlocks deeper automation.

### Access Tiers

| Tier | Method | Access Level | User Effort | What It Unlocks |
|---|---|---|---|---|
| **Tier A: No Bank (default)** | None | No bank data | Zero | Everything works — manual input where needed |
| **Tier B: CSV Import** | User uploads bank statement | Snapshot of past transactions | Per-import (manual) | Subscription auto-discovery (UC7), spending insights, grocery list suggestions (UC10) |
| **Tier C: Up Bank API** | User generates personal API token | Read-only live transactions | One-time token setup | Auto-progress tracking for bonuses (UC4), cashback verification (UC2), subscription auto-discovery (UC7), referral payout detection (UC8) |
| **Tier D: Open Banking (CDR)** | Australia's Consumer Data Right | Read-only, consented, regulated | One-time consent flow | Same as Tier C but for any participating AU bank. Requires formal accreditation (Phase 5+) |

### What Each Tier Enables

```
Tier A (No Bank):
  UC1  Deal Discovery .............. ✅ Full functionality
  UC2  Cashback Optimiser .......... ✅ Full (cashback rates from scraping)
  UC3  Passive Earnings ............ ⚠️ Manual input for some sources
  UC4  Bank Bonus Tracker .......... ⚠️ Manual progress checklist
  UC5  Government Rebates .......... ✅ Full functionality
  UC6  Price Watch ................. ✅ Full functionality
  UC7  Subscription Audit .......... ⚠️ Manual entry in Wallos
  UC8  Referral Manager ............ ⚠️ Manual payout confirmation
  UC9  Class Action Monitor ........ ✅ Full functionality
  UC10 Grocery Savings ............. ⚠️ Manual shopping list

Tier B (CSV Import) — adds:
  UC7  Subscription Audit .......... ✅ Auto-discovered from transactions
  UC10 Grocery Savings ............. ✅ Auto-suggested shopping list

Tier C (Up Bank API) — adds:
  UC3  Passive Earnings ............ ✅ Auto-detect cashback payouts
  UC4  Bank Bonus Tracker .......... ✅ Auto-progress monitoring
  UC7  Subscription Audit .......... ✅ Live subscription detection
  UC8  Referral Manager ............ ✅ Auto-detect referral payouts
  UC10 Grocery Savings ............. ✅ Auto-maintained shopping list

Tier D (CDR / Open Banking) — same as Tier C for any participating bank
```

### Security Guarantees

| Guarantee | Implementation |
|---|---|
| **No bank credentials stored** | Up Bank uses a user-generated API token (not username/password). CDR uses OAuth2 consent flow. Neither requires bank login credentials |
| **Read-only access only** | Up Bank API is read-only by design. CDR consent scoped to transaction read only |
| **No transactions initiated** | The system NEVER moves money, makes payments, or initiates transfers. P5 (Zero Tolerance) — system recommends, user executes |
| **Token encrypted at rest** | Up Bank API token encrypted via ASP.NET Core Data Protection API |
| **User-revocable at any time** | Up Bank token can be revoked instantly from the Up app. CDR consent revocable via bank |
| **Data stays local** | Self-hosted — bank transaction data never leaves the user's machine |
| **No screen scraping of bank logins** | Playwright is used for public retail sites only, NEVER for bank login pages |

### Up Bank API Details

Up Bank is one of the few Australian banks with a public REST API:
- **Base URL:** `https://api.up.com.au/api/v1`
- **Auth:** Bearer token (user generates in Up app under Settings → API)
- **Endpoints used:**
  - `GET /accounts` — list accounts and balances
  - `GET /transactions` — list transactions with filtering (date, category, status)
  - `GET /categories` — transaction categories
- **Rate limit:** Reasonable (undocumented, but generous for personal use)
- **Data:** Transaction descriptions, amounts, dates, categories, merchant info
- **Limitations:** Up Bank customers only (~500K users as of 2026)

### Consumer Data Right (CDR) — Phase 5+

CDR is Australia's government-mandated Open Banking framework:
- **Requires formal accreditation** as an Accredited Data Recipient (ADR) — significant compliance overhead
- **Covers:** ANZ, CBA, NAB, Westpac, Macquarie, ING, and 100+ other participants
- **Access:** OAuth2 consent flow, read-only, time-limited consent
- **Why deferred:** Accreditation requires legal entity, compliance documentation, security audit. Not viable for MVP/early phases. Explore via CDR sandbox first
- **Alternative:** Unrestricted CDR access is possible via a CDR Representative model (partnering with an existing ADR)

---

## 12. Risk & Compliance

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
| TopCashback scraping | **Low** | Reading public cashback rates |
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
