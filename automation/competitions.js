// Competition auto-entry. Designed as a "best effort, defensive" bot:
// - Only submits against VERY simple forms (email + maybe name/postcode).
// - Bails on captchas, social logins, multi-page forms, DOB pickers, file uploads.
// - Returns a reason code so the caller can log WHY it didn't enter.
//
// Safety rationale: AU trade promotion lotteries require a natural person per
// entry. We're filling on behalf of ONE real person (the user's own details).
// We rate-limit at the caller level — not here.

import { chromium } from 'playwright-extra'
import stealth from 'puppeteer-extra-plugin-stealth'

chromium.use(stealth())

const PROFILE_DIR = process.env.PROFILE_DIR || '/data/browser-profile'

function sleep(ms) { return new Promise(r => setTimeout(r, ms)) }

/** Heuristic red flags — presence of any → skip. */
const CAPTCHA_MARKERS = [
  'iframe[src*="recaptcha"]',
  'iframe[src*="hcaptcha"]',
  '.g-recaptcha',
  '.h-captcha',
  '.cf-turnstile',
  'iframe[src*="turnstile"]',
  'iframe[title*="captcha" i]',
]

const COMPLEX_FIELD_MARKERS = [
  'input[type="file"]',
  'input[type="date"]',            // most comps just want email; a date picker = DOB complexity
  'select[name*="year" i]',        // DOB year dropdown
  'select[name*="birth" i]',
  'textarea[required]',            // open-text required = "tell us why you want to win" — can't bot
]

const SOCIAL_LOGIN_MARKERS = [
  'a[href*="facebook.com/v"]',
  'button[class*="facebook" i]',
  'a[href*="accounts.google.com"]',
  'button[class*="google-signin" i]',
]

async function detectBlockers(page) {
  for (const sel of CAPTCHA_MARKERS) {
    if (await page.$(sel)) return 'captcha_detected'
  }
  for (const sel of COMPLEX_FIELD_MARKERS) {
    if (await page.$(sel)) return 'complex_form'
  }
  for (const sel of SOCIAL_LOGIN_MARKERS) {
    if (await page.$(sel)) return 'social_login_required'
  }
  // Look for obvious signals in visible text
  const text = (await page.evaluate(() => document.body.innerText || '')).toLowerCase()
  if (text.includes('upload receipt') || text.includes('proof of purchase')) {
    return 'requires_receipt'
  }
  if (text.includes('invite a friend') || text.includes('tag a friend') || text.includes('follow us on')) {
    return 'social_engagement_required'
  }
  return null
}

/** Find the first visible email input. Returns the locator selector or null. */
async function findEmailField(page) {
  const candidates = [
    'input[type="email"]',
    'input[name*="email" i]:not([type="hidden"])',
    'input[id*="email" i]:not([type="hidden"])',
    'input[placeholder*="email" i]',
  ]
  for (const sel of candidates) {
    const el = await page.$(sel)
    if (el && await el.isVisible()) return sel
  }
  return null
}

async function findOptionalField(page, names) {
  const selectors = names.flatMap(n => [
    `input[name*="${n}" i]:not([type="hidden"])`,
    `input[id*="${n}" i]:not([type="hidden"])`,
    `input[placeholder*="${n}" i]`,
  ])
  for (const sel of selectors) {
    const el = await page.$(sel)
    if (el && await el.isVisible()) return sel
  }
  return null
}

async function tickConsentCheckboxes(page) {
  // Required checkboxes ("I agree to T&Cs", "Subscribe to newsletter") need ticking,
  // but we only tick visible unchecked ones and never tick anything that looks like
  // a marketing opt-IN for third parties (we're already implying marketing consent).
  const boxes = await page.$$('input[type="checkbox"]:not(:checked)')
  for (const box of boxes) {
    try {
      if (!await box.isVisible()) continue
      const label = await box.evaluate((el) => {
        const id = el.id
        const lbl = id ? document.querySelector(`label[for="${id}"]`) : null
        return (lbl?.innerText || el.closest('label')?.innerText || '').toLowerCase()
      })
      // Tick "agree/accept/confirm/subscribe/newsletter" — skip obvious third-party sharing
      const isConsent = /agree|accept|confirm|subscrib|newsletter|over 18|australian resident/.test(label)
      const isThirdParty = /third.?part|share.*partners|marketing from other/.test(label)
      if (isConsent && !isThirdParty) {
        await box.check({ timeout: 2000 }).catch(() => {})
      }
    } catch { /* keep going */ }
  }
}

async function clickSubmit(page) {
  const candidates = [
    'button[type="submit"]:not([disabled])',
    'input[type="submit"]:not([disabled])',
    'button:has-text("Enter")',
    'button:has-text("Submit")',
    'button:has-text("Sign up")',
    'button:has-text("Subscribe")',
  ]
  for (const sel of candidates) {
    const el = await page.$(sel)
    if (el && await el.isVisible()) {
      await el.click({ timeout: 3000 }).catch(() => {})
      return sel
    }
  }
  return null
}

/**
 * Attempt to auto-enter a single competition URL.
 * Returns { status, reason?, log }.
 *   status: 'entered' | 'skipped' | 'failed'
 */
export async function runCompetitionEntry({ url, email, firstName, lastName, postcode }) {
  const log = []
  const push = (m) => { console.log(m); log.push(m) }

  if (!url || !email) {
    return { status: 'failed', reason: 'missing_inputs', log: ['ERROR: url + email required'] }
  }

  push(`Competition entry: ${url}`)
  push(`Using email: ${email}`)

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: false,
    channel: 'msedge',
    args: ['--disable-blink-features=AutomationControlled', '--no-sandbox', '--disable-dev-shm-usage'],
    viewport: { width: 1366, height: 768 },
    locale: 'en-AU',
    timezoneId: 'Australia/Sydney',
  })
  const page = await context.newPage()

  try {
    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 25000 })
    await sleep(3000)

    const blocker = await detectBlockers(page)
    if (blocker) {
      push(`Skipping: ${blocker}`)
      await context.close()
      return { status: 'skipped', reason: blocker, log }
    }

    const emailSel = await findEmailField(page)
    if (!emailSel) {
      push('Skipping: no email field found')
      await context.close()
      return { status: 'skipped', reason: 'no_email_field', log }
    }

    await page.fill(emailSel, email)
    push('Filled email')

    // Optional fields — best effort, never fail if missing.
    const firstSel = await findOptionalField(page, ['first', 'given', 'fname'])
    if (firstSel && firstName) { await page.fill(firstSel, firstName); push('Filled first name') }

    const lastSel = await findOptionalField(page, ['last', 'surname', 'lname'])
    if (lastSel && lastName) { await page.fill(lastSel, lastName); push('Filled last name') }

    const pcSel = await findOptionalField(page, ['postcode', 'zip', 'postal'])
    if (pcSel && postcode) { await page.fill(pcSel, postcode); push('Filled postcode') }

    await tickConsentCheckboxes(page)
    push('Ticked consent checkboxes where present')

    // Pre-submit sanity: re-check blockers in case the form revealed new ones after focus.
    const lateBlocker = await detectBlockers(page)
    if (lateBlocker) {
      push(`Skipping after fill: ${lateBlocker}`)
      await context.close()
      return { status: 'skipped', reason: lateBlocker, log }
    }

    await sleep(800 + Math.random() * 600)
    const submitted = await clickSubmit(page)
    if (!submitted) {
      push('Skipping: no submit button found')
      await context.close()
      return { status: 'skipped', reason: 'no_submit_button', log }
    }
    push(`Clicked submit (${submitted})`)

    // Wait for post-submit page/state.
    await sleep(4000)
    const postText = (await page.evaluate(() => document.body.innerText || '')).toLowerCase()

    // Error indicators that mean we DIDN'T actually enter.
    const looksErrored = /error|invalid|required|failed|please try again/.test(postText)
    const looksSuccess = /thank you|success|confirmed|entered|registered|check your (email|inbox)/.test(postText)

    await context.close()

    if (looksErrored && !looksSuccess) {
      push('Post-submit page looks like validation error')
      return { status: 'failed', reason: 'post_submit_error', log }
    }
    push('Entry looks successful')
    return { status: 'entered', log }
  } catch (e) {
    push(`ERROR: ${e.message}`)
    await context.close().catch(() => {})
    return { status: 'failed', reason: e.message, log }
  }
}
