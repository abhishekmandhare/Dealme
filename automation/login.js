import { chromium } from 'playwright'

// Interactive login script — run this once to set up the browser profile.
// After logging in, the session is saved to the persistent profile directory.

const PROFILE_DIR = process.env.PROFILE_DIR || './browser-profile'

console.log('Launching browser for Microsoft login...')
console.log('Profile will be saved to:', PROFILE_DIR)
console.log('')
console.log('1. Log in to your Microsoft account in the browser window')
console.log('2. Make sure you can see the Bing Rewards dashboard')
console.log('3. Close the browser when done — the session will be saved')
console.log('')

const context = await chromium.launchPersistentContext(PROFILE_DIR, {
  headless: false,
  args: ['--disable-blink-features=AutomationControlled'],
  viewport: { width: 1366, height: 768 },
})

const page = context.pages()[0] || await context.newPage()
await page.goto('https://www.bing.com/rewards/dashboard')

// Wait for the user to close the browser
await new Promise(resolve => {
  context.on('close', resolve)
})

console.log('Browser closed. Profile saved. You can now run the automation.')
