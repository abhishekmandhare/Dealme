import { chromium } from 'playwright-extra'
import stealth from 'puppeteer-extra-plugin-stealth'
import { getRandomQueries } from './words.js'

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

async function readPointsBalance(page) {
  try {
    // networkidle is unreliable on rewards.bing.com — Bing keeps long-lived
    // telemetry connections open, so the event often never fires. Use
    // domcontentloaded and give the SPA time to hydrate the balance widget.
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 30000 })
    await sleep(6000)
    return await page.evaluate(() => {
      const mainText = document.querySelector('main')?.innerText || document.body.innerText
      // "Available points\n\n1,794" — anchored so we don't match redemption tiles.
      const m = mainText.match(/Available points\s*[\n\s]+([\d,]+)/i)
      return m ? parseInt(m[1].replace(/,/g, ''), 10) : null
    })
  } catch (e) {
    console.log(`readPointsBalance failed: ${e.message}`)
    return null
  }
}

async function runSearches({ count, minD, maxD, userAgent, viewport, label, channel }) {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Bing ${label} searches (count: ${count}${channel ? `, channel: ${channel}` : ''})`)

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    // Headful inside xvfb — Bing's bot detection checks real rendering signals.
    headless: false,
    // channel='msedge' launches the real Edge binary, which unlocks the Edge
    // browser bonus on Microsoft Rewards. Omit userAgent in that case so Edge
    // sends its genuine UA (and matching Sec-CH-UA client hints).
    channel,
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-dev-shm-usage',
    ],
    userAgent,
    viewport,
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  const page = context.pages()[0] || await context.newPage()

  try {
    await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(2000)

    // Desktop shows #id_n (username); mobile shows #id_s (account icon) or hides #id_a (sign-in).
    // We're logged OUT if the sign-in link (#id_a) is visible.
    const signInEl = await page.$('#id_a')
    const signInVisible = signInEl ? await signInEl.isVisible() : false

    if (signInVisible) {
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      return { success: false, searches: 0, log }
    }

    // Try to read the display name (desktop only — fine to skip on mobile)
    const nameEl = await page.$('#id_n')
    const nameText = nameEl ? (await nameEl.textContent()).trim() : ''
    push(nameText ? `Logged in as: ${nameText}` : 'Logged in (mobile — name not shown)')
  } catch (e) {
    push(`Warning: Could not check login status: ${e.message}`)
  }

  // Capture balance before we start so the API can record an accurate pointsBefore.
  const pointsBefore = await readPointsBalance(page)
  if (pointsBefore != null) push(`Points before: ${pointsBefore.toLocaleString()}`)

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

  await context.close()
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
  await sleep(2500 + Math.random() * 1500)

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

  for (let round = 0; round < 15; round++) {
    const clicked = await tryClickFirst(page, answerSelectors)
    if (!clicked) break
    await sleep(1500 + Math.random() * 1200)
    await tryClickFirst(page, nextSelectors)
    await sleep(1200 + Math.random() * 800)
  }

  // Article-style tiles: just scroll to simulate reading.
  await page.mouse.wheel(0, 500).catch(() => {})
  await sleep(1500)
  await page.mouse.wheel(0, 500).catch(() => {})
  await sleep(1200)
}

async function collectActivityUrls(page) {
  return await page.evaluate(() => {
    // Activity tiles on the dashboard link to either bing.com/search?...
    // (quiz/poll triggered via search) or bing.com/rewardsapp/ pages.
    // Completed tiles typically have a checkmark — we filter those out.
    const result = new Set()
    for (const a of document.querySelectorAll('a[href]')) {
      const href = a.href
      if (!href) continue
      if (!/bing\.com\/(search|rewardsapp)/i.test(href)) continue
      // Skip anchors that clearly look like completed or static nav links.
      const wrapper = a.closest('[aria-label], [class]')
      const aria = (wrapper?.getAttribute('aria-label') || '').toLowerCase()
      if (aria.includes('complete')) continue
      const hasCheckmark = !!a.querySelector('svg[class*="check" i], [class*="complete" i]')
      if (hasCheckmark) continue
      result.add(href)
    }
    return [...result].slice(0, 15)
  })
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

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: false,
    channel: 'msedge',
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-dev-shm-usage',
    ],
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  const page = context.pages()[0] || await context.newPage()

  try {
    // Login check
    await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(1500)
    const signInEl = await page.$('#id_a')
    if (signInEl && await signInEl.isVisible()) {
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      return { success: false, error: 'not_logged_in', log }
    }

    // Balance gate
    const pointsBefore = await readPointsBalance(page)
    push(`Balance: ${pointsBefore ?? 'unknown'} pts (need ≥ ${expectedCost})`)
    if (pointsBefore == null) {
      await context.close()
      return { success: false, error: 'could_not_read_balance', log }
    }
    if (pointsBefore < expectedCost) {
      push(`ABORT: insufficient balance (${pointsBefore} < ${expectedCost})`)
      await context.close()
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
    return { success: false, error: e.message, log }
  }
}

export async function runDailyActivities({ maxActivities } = {}) {
  const limit = maxActivities ?? parseInt(process.env.DAILY_ACTIVITIES_LIMIT || '10', 10)
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Microsoft Rewards daily activities (max: ${limit})`)

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: false,
    channel: 'msedge',
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-dev-shm-usage',
    ],
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  const page = context.pages()[0] || await context.newPage()
  let completed = 0
  let pointsBefore = null

  try {
    await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(1500)

    const signInEl = await page.$('#id_a')
    const signInVisible = signInEl ? await signInEl.isVisible() : false
    if (signInVisible) {
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      return { success: false, completed: 0, log }
    }

    const nameEl = await page.$('#id_n')
    const nameText = nameEl ? (await nameEl.textContent()).trim() : ''
    push(nameText ? `Logged in as: ${nameText}` : 'Logged in')

    pointsBefore = await readPointsBalance(page)
    if (pointsBefore != null) push(`Points before: ${pointsBefore.toLocaleString()}`)

    const urls = await collectActivityUrls(page)
    push(`Found ${urls.length} candidate activity tiles`)

    for (const url of urls.slice(0, limit)) {
      try {
        const tab = await context.newPage()
        await tab.goto(url, { waitUntil: 'domcontentloaded', timeout: 20000 })
        await handleActivityTab(tab)
        push(`[${completed + 1}] Processed: ${url.slice(0, 100)}`)
        await tab.close()
        completed++
        await sleep(2000 + Math.random() * 2500)
      } catch (e) {
        push(`Activity skipped (${url.slice(0, 60)}): ${e.message}`)
      }
    }
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
