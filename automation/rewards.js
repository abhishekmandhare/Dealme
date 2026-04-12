import { chromium } from 'playwright'
import { getRandomQueries } from './words.js'

const PROFILE_DIR = process.env.PROFILE_DIR || '/data/browser-profile'
const SEARCH_COUNT = parseInt(process.env.SEARCH_COUNT || '33', 10)
const MOBILE_SEARCH_COUNT = parseInt(process.env.MOBILE_SEARCH_COUNT || '20', 10)
const MIN_DELAY = parseInt(process.env.MIN_DELAY || '8000', 10)
const MAX_DELAY = parseInt(process.env.MAX_DELAY || '20000', 10)

const DESKTOP_UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0'
const MOBILE_UA  = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 EdgiOS/131.0.0.0 Mobile/15E148 Safari/604.1'

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms))
}

async function runSearches({ count, minD, maxD, userAgent, viewport, label }) {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Bing ${label} searches (count: ${count})`)

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: true,
    args: ['--disable-blink-features=AutomationControlled'],
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

  const queries = getRandomQueries(count)
  let completed = 0

  for (const query of queries) {
    try {
      await page.goto(`https://www.bing.com/search?q=${encodeURIComponent(query)}`, {
        waitUntil: 'domcontentloaded',
        timeout: 15000,
      })
      completed++
      push(`[${completed}/${count}] Searched: "${query}"`)

      const delay = minD + Math.random() * (maxD - minD)
      await sleep(delay)
    } catch (e) {
      push(`[${completed + 1}/${count}] Failed: "${query}" — ${e.message}`)
    }
  }

  try {
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(3000)
    const content = await page.content()
    const pointsMatch = content.match(/availablePoints["\s:]+(\d[\d,]*)/i)
      || content.match(/(\d[\d,]+)\s*(?:available\s*)?points/i)
    if (pointsMatch) push(`Points balance: ${pointsMatch[1]}`)
    else push('Points balance: could not read (check rewards dashboard manually)')
  } catch (e) {
    push(`Warning: Could not check final points: ${e.message}`)
  }

  await context.close()
  push(`Done. Completed ${completed}/${count} ${label} searches.`)

  return { success: true, searches: completed, log }
}

export async function runBingSearches({ searchCount, minDelay, maxDelay } = {}) {
  return runSearches({
    count: searchCount ?? SEARCH_COUNT,
    minD: minDelay ?? MIN_DELAY,
    maxD: maxDelay ?? MAX_DELAY,
    userAgent: DESKTOP_UA,
    viewport: { width: 1366, height: 768 },
    label: 'desktop',
  })
}

export async function runBingMobileSearches({ searchCount, minDelay, maxDelay } = {}) {
  return runSearches({
    count: searchCount ?? MOBILE_SEARCH_COUNT,
    minD: minDelay ?? MIN_DELAY,
    maxD: maxDelay ?? MAX_DELAY,
    userAgent: MOBILE_UA,
    viewport: { width: 390, height: 844 },
    label: 'mobile',
  })
}
