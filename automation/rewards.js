import { chromium } from 'playwright'
import { getRandomQueries } from './words.js'
import path from 'path'

const PROFILE_DIR = process.env.PROFILE_DIR || '/data/browser-profile'
const SEARCH_COUNT = parseInt(process.env.SEARCH_COUNT || '33', 10)
const MIN_DELAY = parseInt(process.env.MIN_DELAY || '8000', 10)   // ms between searches
const MAX_DELAY = parseInt(process.env.MAX_DELAY || '20000', 10)

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms))
}

function randomDelay() {
  return MIN_DELAY + Math.random() * (MAX_DELAY - MIN_DELAY)
}

export async function runBingSearches() {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push(`Starting Bing searches (count: ${SEARCH_COUNT})`)

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: true,
    args: ['--disable-blink-features=AutomationControlled'],
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0',
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })

  const page = context.pages()[0] || await context.newPage()

  // Check if logged in by visiting Bing
  try {
    await page.goto('https://www.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(2000)

    // Check for logged-in user — look for the visible name element
    const nameEl = await page.$('#id_n')
    const nameText = nameEl ? (await nameEl.textContent()).trim() : ''
    const nameVisible = nameEl ? await nameEl.isVisible() : false

    if (!nameText || !nameVisible) {
      push('ERROR: Not logged in. Run the login flow first.')
      await context.close()
      return { success: false, searches: 0, log }
    }

    push(`Logged in as: ${nameText}`)
  } catch (e) {
    push(`Warning: Could not check login status: ${e.message}`)
  }

  const queries = getRandomQueries(SEARCH_COUNT)
  let completed = 0

  for (const query of queries) {
    try {
      await page.goto(`https://www.bing.com/search?q=${encodeURIComponent(query)}`, {
        waitUntil: 'domcontentloaded',
        timeout: 15000,
      })
      completed++
      push(`[${completed}/${SEARCH_COUNT}] Searched: "${query}"`)

      // Random delay between searches
      const delay = randomDelay()
      await sleep(delay)
    } catch (e) {
      push(`[${completed + 1}/${SEARCH_COUNT}] Failed: "${query}" — ${e.message}`)
    }
  }

  // Check points after via rewards dashboard
  try {
    await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 15000 })
    await sleep(3000)
    const content = await page.content()
    // Look for points balance in page content
    const pointsMatch = content.match(/availablePoints["\s:]+(\d[\d,]*)/i)
      || content.match(/(\d[\d,]+)\s*(?:available\s*)?points/i)
    if (pointsMatch) push(`Points balance: ${pointsMatch[1]}`)
    else push('Points balance: could not read (check rewards dashboard manually)')
  } catch (e) {
    push(`Warning: Could not check final points: ${e.message}`)
  }

  await context.close()
  push(`Done. Completed ${completed}/${SEARCH_COUNT} searches.`)

  return { success: true, searches: completed, log }
}
