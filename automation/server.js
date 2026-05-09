import express from 'express'
import { runBingSearches, runBingMobileSearches, runDailyActivities, runRedemption, openLoginBrowser } from './rewards.js'
import { runCompetitionEntry } from './competitions.js'
import { runGoogleSurveys } from './google-surveys.js'

const app = express()
const PORT = parseInt(process.env.PORT || '3100', 10)

app.use(express.json())

let lastRun = null
let running = false

// Wall-clock cap on a task. If it doesn't resolve in time, we reject so the
// outer try/catch fires and `finally` clears the `running` flag — otherwise
// a hung Playwright call locks the service indefinitely (#stuck-running).
// Any browser processes left behind get cleaned up on next container restart.
function withTimeout(promise, ms, label) {
  let timer
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`${label} timed out after ${Math.round(ms / 1000)}s`)), ms)
  })
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer))
}

const MIN = 60 * 1000

app.get('/health', (_req, res) => {
  res.json({ status: 'ok', lastRun })
})

app.post('/run/bing-searches', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  const { searchCount, minDelay, maxDelay } = req.body || {}

  running = true
  try {
    const result = await withTimeout(runBingSearches({ searchCount, minDelay, maxDelay }), 20 * MIN, 'bing-searches')
    lastRun = {
      task: 'bing-searches',
      success: result.success,
      searches: result.searches,
      pointsBefore: result.pointsBefore ?? null,
      pointsAfter: result.pointsAfter ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'bing-searches',
      success: false,
      error: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

app.post('/run/bing-searches-mobile', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  const { searchCount, minDelay, maxDelay } = req.body || {}

  running = true
  try {
    const result = await withTimeout(runBingMobileSearches({ searchCount, minDelay, maxDelay }), 15 * MIN, 'bing-searches-mobile')
    lastRun = {
      task: 'bing-searches-mobile',
      success: result.success,
      searches: result.searches,
      pointsBefore: result.pointsBefore ?? null,
      pointsAfter: result.pointsAfter ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'bing-searches-mobile',
      success: false,
      error: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

app.post('/run/daily-activities', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  const { maxActivities } = req.body || {}

  running = true
  try {
    const result = await withTimeout(runDailyActivities({ maxActivities }), 15 * MIN, 'daily-activities')
    lastRun = {
      task: 'daily-activities',
      success: result.success,
      completed: result.completed,
      pointsBefore: result.pointsBefore ?? null,
      pointsAfter: result.pointsAfter ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'daily-activities',
      success: false,
      error: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

app.post('/run/redeem', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  const { brand, denomination, dryRun } = req.body || {}

  running = true
  try {
    const result = await withTimeout(runRedemption({ brand, denomination, dryRun }), 5 * MIN, 'redeem')
    lastRun = {
      task: 'redeem',
      success: result.success,
      brand: result.brand,
      denomination: result.denomination,
      pointsBefore: result.pointsBefore ?? null,
      pointsAfter: result.pointsAfter ?? null,
      pointsSpent: result.pointsSpent ?? null,
      error: result.error ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'redeem',
      success: false,
      error: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

app.post('/run/enter-competition', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  const { url, email, firstName, lastName, postcode } = req.body || {}

  running = true
  try {
    const result = await withTimeout(runCompetitionEntry({ url, email, firstName, lastName, postcode }), 5 * MIN, 'enter-competition')
    lastRun = {
      task: 'enter-competition',
      url,
      status: result.status,
      reason: result.reason ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'enter-competition',
      url,
      status: 'failed',
      reason: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

app.post('/run/google-surveys', async (req, res) => {
  if (running) return res.status(409).json({ error: 'Already running' })
  running = true
  try {
    const result = await withTimeout(runGoogleSurveys(), 10 * MIN, 'google-surveys')
    lastRun = {
      task: 'google-surveys',
      success: result.success,
      completed: result.completed,
      creditsBefore: result.creditsBefore ?? null,
      creditsAfter: result.creditsAfter ?? null,
      timestamp: new Date().toISOString(),
      log: result.log,
    }
    res.json(lastRun)
  } catch (e) {
    lastRun = {
      task: 'google-surveys',
      success: false,
      error: e.message,
      timestamp: new Date().toISOString(),
    }
    res.status(500).json(lastRun)
  } finally {
    running = false
  }
})

// Opens Edge on the Microsoft login page and waits up to 10 min for sign-in.
// Connect via VNC (port 5900) to complete the login interactively.
app.post('/run/login', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }
  running = true
  try {
    const result = await withTimeout(openLoginBrowser(), 12 * MIN, 'login')
    res.json({ success: result.success, message: result.message, log: result.log })
  } catch (e) {
    res.status(500).json({ success: false, error: e.message })
  } finally {
    running = false
  }
})

app.get('/status', (_req, res) => {
  res.json({ running, lastRun })
})

const server = app.listen(PORT, '0.0.0.0', () => {
  console.log(`Automation service listening on port ${PORT}`)
})

process.on('SIGTERM', () => {
  server.close(() => process.exit(0))
})

// Playwright-extra stealth plugin can throw unhandled rejections when a
// browser crashes mid-run. Log them but keep the server alive.
process.on('unhandledRejection', (reason) => {
  console.error('Unhandled rejection (ignored):', reason?.message ?? reason)
})
