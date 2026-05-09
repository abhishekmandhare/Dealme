import { chromium } from 'playwright-extra'
import stealth from 'puppeteer-extra-plugin-stealth'
import { getRandomQueries } from './words.js'
import fs from 'fs'

chromium.use(stealth())

const PROFILE_DIR = process.env.PROFILE_DIR || '/data/browser-profile'
const SEARCH_COUNT = parseInt(process.env.SEARCH_COUNT || '33', 10)
const MOBILE_SEARCH_COUNT = parseInt(process.env.MOBILE_SEARCH_COUNT || '20', 10)
const MIN_DELAY = parseInt(process.env.MIN_DELAY || '8000', 10)
const MAX_DELAY = parseInt(process.env.MAX_DELAY || '20000', 10)

const MOBILE_UA = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 EdgiOS/131.0.0.0 Mobile/15E148 Safari/604.1'

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms))
}

const COOKIES_FILE = '/data/ms-cookies.json'

async function injectSavedCookies(context) {
  if (!fs.existsSync(COOKIES_FILE)) return
  try {
    const cookies = JSON.parse(fs.readFileSync(COOKIES_FILE, 'utf8'))
    await context.addCookies(cookies)
  } catch (e) {
    console.log(`injectSavedCookies failed: ${e.message}`)
  }
}

async function readPointsBalance(page) {
  try {
    // networkidle is unreliable on rewards.bing.com — Bing keeps long-lived
    // telemetry connections open, so the event often never fires. Use
    // domcontentloaded and give the SPA time to hydrate the balance widget.
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 30000 })
    await sleep(6000)
    const result = await page.evaluate(() => {
      const mainText = document.querySelector('main')?.innerText || document.body.innerText
      // The SPA widget can render the number before or after the label depending
      // on layout version: "Available points\n\n1,794" or "1,794\n\nAvailable points".
      const after  = mainText.match(/Available points[\s\S]{0,20}?([\d,]{1,10})\s*(?:points)?/i)
      const before = mainText.match(/([\d,]{1,10})\s*(?:points)?\s*[\s\S]{0,20}?Available points/i)
      const raw = (after || before)?.[1]
      return { value: raw ? parseInt(raw.replace(/,/g, ''), 10) : null, snippet: mainText.slice(0, 400) }
    })
    if (result.value == null) console.log('readPointsBalance no match, page snippet:', result.snippet)
    return result.value
  } catch (e) {
    console.log(`readPointsBalance failed: ${e.message}`)
    return null
  }
}

async function runSearches({ count, minD, maxD, userAgent, viewport, label, channel }) {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Bing ${label} searches (count: ${count}${channel ? `, channel: ${channel}` : ''})`)

  let context, browser

  if (channel) {
    // Desktop (Edge): use persistent profile — auth comes from the login flow,
    // never expires independently of browser cookies.
    context = await chromium.launchPersistentContext(PROFILE_DIR, {
      headless: false,
      channel,
      args: [
        '--disable-blink-features=AutomationControlled',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--no-first-run',
        '--disable-sync',
        '--no-default-browser-check',
      ],
      viewport,
      locale: 'en-AU',
      timezoneId: 'Australia/Sydney',
    })
  } else {
    // Mobile (Chromium): non-persistent context with injected cookies.
    // Cookies are refreshed by openLoginBrowser() each time it runs.
    browser = await chromium.launch({
      headless: false,
      args: [
        '--disable-blink-features=AutomationControlled',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--no-first-run',
        '--disable-sync',
        '--no-default-browser-check',
      ],
    })
    context = await browser.newContext({
      userAgent,
      viewport,
      locale: 'en-AU',
      timezoneId: 'Australia/Sydney',
    })
    await injectSavedCookies(context)
  }

  const page = context.pages()[0] || await context.newPage()

  // Login check: read balance from rewards.bing.com (unambiguous; #id_a is unreliable on new Bing UI).
  const pointsBefore = await readPointsBalance(page)
  if (pointsBefore == null) {
    await page.screenshot({ path: '/tmp/bing-login-check.png' }).catch(() => {})
    push('ERROR: Not logged in. Run the login flow first.')
    await context.close()
    if (browser) await browser.close()
    return { success: false, searches: 0, log }
  }
  push(`Logged in. Points before: ${pointsBefore.toLocaleString()}`)

  const queries = getRandomQueries(count)
  let completed = 0

  for (const query of queries) {
    try {
      // Real-looking query flow: land on bing.com, type into the search box,
      // press Enter. Direct /search?q= URLs are a known bot-detection signal.
      await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
      await sleep(700 + Math.random() * 800)

      const box = page.locator('#sb_form_q')
      await box.click({ timeout: 5000 })
      await box.fill('')
      // Per-char typing with jitter — mimics a person tapping keys.
      await box.pressSequentially(query, { delay: 60 + Math.floor(Math.random() * 90) })
      await sleep(200 + Math.random() * 400)
      await page.keyboard.press('Enter')
      await page.waitForLoadState('domcontentloaded', { timeout: 15000 })

      completed++
      push(`[${completed}/${count}] Searched: "${query}"`)

      // Scroll so Bing sees engagement, not an instant bail.
      await page.mouse.wheel(0, 400 + Math.floor(Math.random() * 600)).catch(() => {})
      await sleep(800 + Math.random() * 1200)
      await page.mouse.wheel(0, 300 + Math.floor(Math.random() * 500)).catch(() => {})

      const delay = minD + Math.random() * (maxD - minD)
      await sleep(delay)
    } catch (e) {
      push(`[${completed + 1}/${count}] Failed: "${query}" — ${e.message}`)
    }
  }

  const balance = await readPointsBalance(page)
  if (balance != null) push(`Points balance: ${balance.toLocaleString()}`)
  else push('Points balance: could not read (check rewards dashboard manually)')

  // Refresh ms-cookies.json from the persistent profile so mobile searches
  // (Chromium, can't share Edge profile) keep working without manual login.
  if (channel) {
    try {
      const cookies = await context.cookies()
      fs.writeFileSync(COOKIES_FILE, JSON.stringify(cookies, null, 2))
    } catch (e) {
      push(`Warning: could not refresh ${COOKIES_FILE}: ${e.message}`)
    }
  }

  await context.close()
  if (browser) await browser.close()
  push(`Done. Completed ${completed}/${count} ${label} searches.`)

  return { success: true, searches: completed, pointsBefore, pointsAfter: balance, log }
}

// ── Daily Activities ───────────────────────────────────────────────────────
// Microsoft Rewards shows a "Daily Set" and a "More Activities" section on the
// dashboard. Each tile is a quiz / poll / article / "this or that". Completing
// them earns ~10–50 points each. Tiles have varied DOM shapes so this uses a
// defensive click-through: open the tile, try several common answer/next
// selectors, close, next.

async function tryClickFirst(page, selectors) {
  for (const sel of selectors) {
    const el = await page.$(sel)
    if (el) {
      try {
        await el.click({ timeout: 2000 })
        return sel
      } catch { /* try next */ }
    }
  }
  return null
}

async function handleActivityTab(page) {
  await sleep(3000 + Math.random() * 1500)

  const answerSelectors = [
    'div.wk_Answer',
    '.rqAnsChoice',
    '.rqOption',
    '#rqStartQuiz',              // quiz "Start" button
    'label.rqAnsSld',
    '.bt_selectOne',
    '.b_mop',
    '[role="radio"][aria-checked="false"]',
    'input[type="radio"]:not(:checked)',
  ]
  const nextSelectors = [
    '#wk_Next',
    '.wk_button:not(:disabled)',
    '.btOptions > button:not(:disabled)',
    'button.bt_next:not(:disabled)',
    'button[aria-label*="Next" i]:not(:disabled)',
    'button[type="submit"]:not(:disabled)',
  ]

  let interactions = 0
  for (let round = 0; round < 15; round++) {
    const clicked = await tryClickFirst(page, answerSelectors)
    if (!clicked) break
    interactions++
    await sleep(1800 + Math.random() * 1200)
    await tryClickFirst(page, nextSelectors)
    await sleep(1500 + Math.random() * 900)
  }

  // Article/search tiles: no interactive elements found. Simulate reading by
  // dwelling for a realistic amount of time — MS tracks dwell on tiles where
  // "visit and stay" IS the activity. 15-25s aligns with typical article scan.
  if (interactions === 0) {
    for (let i = 0; i < 4; i++) {
      await page.mouse.wheel(0, 300 + Math.random() * 400).catch(() => {})
      await sleep(3500 + Math.random() * 2500)
    }
  } else {
    // Quiz completed — short scroll to "see results" and move on.
    await page.mouse.wheel(0, 500).catch(() => {})
    await sleep(2000)
  }
  return interactions
}

/**
 * Click the tile AS A REAL USER would — from the dashboard — instead of
 * extracting the URL and navigating directly. The dashboard's JS listens
 * for tile clicks to mark activities as started; direct navigation bypasses
 * that handler and MS's crediting engine treats the visit as out-of-band.
 */
async function clickDashboardTilesSequentially(context, page, limit, push) {
  let processed = 0
  // Track hrefs we've already clicked so we skip them on the next iteration.
  // Dashboard doesn't visually update "completed" state fast enough to rely on.
  const seenHrefs = new Set()

  for (let i = 0; i < limit; i++) {
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 30000 })
    await sleep(3500)

    const tileInfo = await page.evaluate((seenList) => {
      const seen = new Set(seenList)
      const anchors = [...document.querySelectorAll('a[href*="bing.com"]')]
      for (let idx = 0; idx < anchors.length; idx++) {
        const a = anchors[idx]
        const href = a.href
        if (!href) continue
        if (!/bing\.com\/(search|rewardsapp)/i.test(href)) continue
        if (seen.has(href)) continue
        const aria = (a.closest('[aria-label]')?.getAttribute('aria-label') || '').toLowerCase()
        if (aria.includes('complete')) continue
        if (a.querySelector('[class*="check" i], [class*="complete" i]')) continue
        const rect = a.getBoundingClientRect()
        if (rect.width < 50 || rect.height < 50) continue
        return { href, idx, label: (a.innerText || aria || href).slice(0, 80) }
      }
      return null
    }, [...seenHrefs])

    if (!tileInfo) {
      push(`No more unclaimed tiles after ${processed}`)
      break
    }

    push(`Tile ${processed + 1}: ${tileInfo.label.replace(/\s+/g, ' ')}`)

    try {
      // Click the anchor in a way that fires the dashboard's JS handlers.
      // The tile usually opens in a new tab — we wait for it.
      const [newTab] = await Promise.all([
        context.waitForEvent('page', { timeout: 8000 }).catch(() => null),
        page.evaluate((idx) => {
          const a = document.querySelectorAll('a[href*="bing.com"]')[idx]
          if (a) {
            a.scrollIntoView({ block: 'center' })
            a.click()
          }
        }, tileInfo.idx),
      ])

      // Some tiles open in the same tab instead of a new one.
      const targetPage = newTab ?? page
      await targetPage.waitForLoadState('domcontentloaded', { timeout: 15000 }).catch(() => {})
      const interactions = await handleActivityTab(targetPage)
      push(`  → ${interactions} interactions`)
      if (newTab && !newTab.isClosed()) await newTab.close().catch(() => {})
      seenHrefs.add(tileInfo.href)
      processed++
      await sleep(2500 + Math.random() * 2000)
    } catch (e) {
      push(`  tile error: ${e.message}`)
      seenHrefs.add(tileInfo.href)  // don't retry the same broken tile
    }
  }

  return processed
}

// ── Redemption ─────────────────────────────────────────────────────────────
// Spend earned points on a gift card. Defensive: refuses to proceed if the
// balance is too low, logs every step, and never retries (redemption can't
// be undone).

// Point costs for AU Microsoft Rewards (approximate — Microsoft adjusts these).
// Keyed as "brand:denomination".
const REDEMPTION_COSTS = {
  'amazon:2': 2700,
  'amazon:5': 6500,
  'amazon:10': 13000,
}

export async function runRedemption({ brand = 'amazon', denomination = 2, dryRun = false } = {}) {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }
  const key = `${brand.toLowerCase()}:${denomination}`
  const expectedCost = REDEMPTION_COSTS[key]

  push(`Starting redemption: ${brand} $${denomination} (dryRun=${dryRun})`)

  if (!expectedCost) {
    push(`ERROR: Unsupported brand/denomination combo: ${key}`)
    return { success: false, error: `Unsupported: ${key}`, log }
  }

  const browser = await chromium.launch({
    headless: false,
    channel: 'msedge',
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-dev-shm-usage',
      '--no-first-run',
      '--disable-sync',
      '--no-default-browser-check',
    ],
  })
  const context = await browser.newContext({
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  await injectSavedCookies(context)

  const page = await context.newPage()

  try {
    // Login check
    await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(1500)
    const signInEl = await page.$('#id_a')
    if (signInEl && await signInEl.isVisible()) {
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      await browser.close()
      return { success: false, error: 'not_logged_in', log }
    }

    // Balance gate
    const pointsBefore = await readPointsBalance(page)
    push(`Balance: ${pointsBefore ?? 'unknown'} pts (need ≥ ${expectedCost})`)
    if (pointsBefore == null) {
      await context.close()
      await browser.close()
      return { success: false, error: 'could_not_read_balance', log }
    }
    if (pointsBefore < expectedCost) {
      push(`ABORT: insufficient balance (${pointsBefore} < ${expectedCost})`)
      await context.close()
      await browser.close()
      return { success: false, error: 'insufficient_balance', pointsBefore, expectedCost, log }
    }

    // Navigate to the redemption catalog.
    await page.goto('https://rewards.bing.com/redeem', { waitUntil: 'domcontentloaded', timeout: 30000 })
    await sleep(5000)

    // Find the tile whose visible text contains both the brand and the $X denomination.
    const tile = await page.evaluate(({ brand, denom }) => {
      const needleBrand = brand.toLowerCase()
      const needlePrice = `$${denom}`
      const candidates = [...document.querySelectorAll('a[href], [role="link"], [role="button"], button')]
      for (const el of candidates) {
        const text = (el.innerText || '').toLowerCase()
        if (text.includes(needleBrand) && text.includes(needlePrice)) {
          const rect = el.getBoundingClientRect()
          if (rect.width === 0 || rect.height === 0) continue
          el.scrollIntoView({ block: 'center', behavior: 'instant' })
          return {
            href: el.tagName === 'A' ? el.href : null,
            text: el.innerText.slice(0, 120),
            rect: { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 },
          }
        }
      }
      return null
    }, { brand, denom: denomination })

    if (!tile) {
      push(`ERROR: Could not find "${brand} $${denomination}" tile on redeem page.`)
      await context.close()
      return { success: false, error: 'tile_not_found', log }
    }
    push(`Matched tile: ${tile.text.replace(/\s+/g, ' ')}`)

    if (dryRun) {
      push('DRY RUN — stopping before confirmation click')
      await context.close()
      return { success: true, dryRun: true, matched: tile.text, pointsBefore, log }
    }

    // Open the tile's detail page
    if (tile.href) {
      await page.goto(tile.href, { waitUntil: 'domcontentloaded', timeout: 15000 })
    } else {
      await page.mouse.click(tile.rect.x, tile.rect.y)
      await page.waitForLoadState('domcontentloaded', { timeout: 15000 })
    }
    await sleep(3500)

    // Click Redeem on the detail page. Try multiple common labels.
    const redeemClicked = await tryClickFirst(page, [
      'button:has-text("Redeem")',
      'a:has-text("Redeem")',
      'button[aria-label*="Redeem" i]',
    ])
    if (!redeemClicked) {
      push('ERROR: Could not find Redeem button on detail page')
      await context.close()
      return { success: false, error: 'redeem_button_missing', log }
    }
    push('Clicked Redeem')
    await sleep(3000)

    // Optional confirmation dialog
    await tryClickFirst(page, [
      'button:has-text("Confirm")',
      'button:has-text("Yes")',
      'button:has-text("Continue")',
    ])
    await sleep(5000)

    // Balance should have dropped by ~expectedCost.
    const pointsAfter = await readPointsBalance(page)
    push(`Balance after: ${pointsAfter ?? 'unknown'}`)

    const spent = (pointsBefore != null && pointsAfter != null) ? pointsBefore - pointsAfter : null
    const looksOk = spent != null && spent >= expectedCost - 50 && spent <= expectedCost + 50

    await context.close()
    await browser.close()
    push(`Done. spent≈${spent} (expected ${expectedCost}) — ${looksOk ? 'OK' : 'UNVERIFIED'}`)

    return {
      success: looksOk,
      brand,
      denomination,
      pointsBefore,
      pointsAfter,
      pointsSpent: spent,
      log,
    }
  } catch (e) {
    push(`ERROR: ${e.message}`)
    await context.close().catch(() => {})
    await browser.close().catch(() => {})
    return { success: false, error: e.message, log }
  }
}

export async function runDailyActivities({ maxActivities } = {}) {
  const limit = maxActivities ?? parseInt(process.env.DAILY_ACTIVITIES_LIMIT || '10', 10)
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Microsoft Rewards daily activities (max: ${limit})`)

  // Use the persistent profile (same as login.js) — cookie injection alone
  // doesn't work for the rewards dashboard due to Microsoft SSO requirements.
  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: false,
    channel: 'msedge',
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-dev-shm-usage',
      '--no-first-run',
      '--disable-sync',
      '--no-default-browser-check',
    ],
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  const page = context.pages()[0] || await context.newPage()
  let completed = 0
  let pointsBefore = null

  try {
    pointsBefore = await readPointsBalance(page)
    if (pointsBefore == null) {
      await page.screenshot({ path: '/tmp/bing-login-check.png' }).catch(() => {})
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      return { success: false, completed: 0, log }
    }
    push(`Logged in. Points before: ${pointsBefore.toLocaleString()}`)

    completed = await clickDashboardTilesSequentially(context, page, limit, push)
  } catch (e) {
    push(`Error: ${e.message}`)
  }

  const balance = await readPointsBalance(page)
  if (balance != null) push(`Points balance: ${balance.toLocaleString()}`)

  await context.close()
  push(`Done. Processed ${completed} activities.`)
  return { success: true, completed, pointsBefore, pointsAfter: balance, log }
}

export async function runBingSearches({ searchCount, minDelay, maxDelay } = {}) {
  return runSearches({
    count: searchCount ?? SEARCH_COUNT,
    minD: minDelay ?? MIN_DELAY,
    maxD: maxDelay ?? MAX_DELAY,
    // Real Edge for desktop — grants the "Edge browser bonus" (+~20 pts/day).
    channel: 'msedge',
    viewport: { width: 1366, height: 768 },
    label: 'desktop',
  })
}

export async function runBingMobileSearches({ searchCount, minDelay, maxDelay } = {}) {
  return runSearches({
    count: searchCount ?? MOBILE_SEARCH_COUNT,
    minD: minDelay ?? MIN_DELAY,
    maxD: maxDelay ?? MAX_DELAY,
    // Keep Chromium for mobile — we need to spoof iOS Edge, which can't be
    // done with a real desktop Edge binary.
    userAgent: MOBILE_UA,
    viewport: { width: 390, height: 844 },
    label: 'mobile',
  })
}

// Opens Edge on the Microsoft login page and polls until the user completes
// sign-in (up to 10 minutes). Connect to port 5900 via VNC to interact.
export async function openLoginBrowser() {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push('Opening Edge for login — connect via VNC on port 5900 to sign in.')
  push('Waiting up to 10 minutes for sign-in to complete...')

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: false,
    channel: 'msedge',
    args: ['--disable-blink-features=AutomationControlled', '--no-sandbox', '--disable-dev-shm-usage'],
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
  })

  const page = context.pages()[0] || await context.newPage()

  try {
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 30000 })

    // Poll every 15 seconds for up to 10 minutes
    const deadline = Date.now() + 10 * 60 * 1000
    let loggedIn = false
    while (Date.now() < deadline) {
      await sleep(15000)
      try {
        await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
        await sleep(2000)
        const signInEl = await page.$('#id_a')
        const signInVisible = signInEl ? await signInEl.isVisible() : false
        if (!signInVisible) {
          loggedIn = true
          push('Sign-in detected — profile saved.')
          break
        }
        push('Still waiting for sign-in...')
      } catch (e) {
        push(`Poll error: ${e.message}`)
      }
    }

    if (loggedIn) {
      // Export cookies so mobile searches (Chromium non-persistent) can reuse the session.
      try {
        const cookies = await context.cookies()
        fs.writeFileSync(COOKIES_FILE, JSON.stringify(cookies, null, 2))
        push(`Saved ${cookies.length} cookies to ${COOKIES_FILE}`)
      } catch (e) {
        push(`Warning: could not save cookies: ${e.message}`)
      }
    }

    await context.close()
    if (loggedIn) {
      return { success: true, message: 'Logged in successfully.', log }
    } else {
      return { success: false, message: 'Timed out waiting for sign-in.', log }
    }
  } catch (e) {
    await context.close().catch(() => {})
    push(`Error: ${e.message}`)
    return { success: false, message: e.message, log }
  }
}
