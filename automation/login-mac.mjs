// One-time login helper — run on Mac, copies profile to Docker volume.
// Usage: node login-mac.mjs
import { chromium } from 'playwright'
import { execSync } from 'child_process'
import fs from 'fs'
import path from 'path'
import os from 'os'

const PROFILE_DIR = path.join(os.tmpdir(), 'ms-rewards-login-profile')

console.log('Installing Playwright browsers (first run only)...')
try { execSync('npx playwright install chromium', { stdio: 'inherit' }) } catch {}

console.log('\nOpening browser — sign in to your Microsoft account.')
console.log('Close the browser window when done.\n')

const context = await chromium.launchPersistentContext(PROFILE_DIR, {
  headless: false,
  args: [
    '--disable-blink-features=AutomationControlled',
    '--use-mock-keychain',   // store cookies unencrypted so Linux container can read them
    '--password-store=basic',
  ],
  viewport: { width: 1366, height: 768 },
  locale: 'en-AU',
})

const page = context.pages()[0] || await context.newPage()
await page.goto('https://rewards.bing.com/', { waitUntil: 'domcontentloaded', timeout: 30000 })

// Wait until the user closes the browser
await context.waitForEvent('close').catch(() => {})
console.log('\nBrowser closed. Exporting cookies...')

// Export cookies as decrypted JSON — avoids Mac keychain encryption issues
const cookies = await context.cookies([
  'https://www.bing.com',
  'https://rewards.bing.com',
  'https://login.microsoftonline.com',
  'https://account.microsoft.com',
])
console.log(`Exported ${cookies.length} cookies.`)

const cookiesFile = '/tmp/ms-rewards-cookies.json'
fs.writeFileSync(cookiesFile, JSON.stringify(cookies, null, 2))

try {
  execSync(`docker cp "${cookiesFile}" dealme-automation:/data/ms-cookies.json`, { stdio: 'inherit' })
  console.log('Done! Cookies copied to container. Runs will now use your Microsoft account.')
} catch (e) {
  console.error('docker cp failed:', e.message)
  console.error(`Cookies saved at: ${cookiesFile}`)
}
