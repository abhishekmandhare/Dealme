import express from 'express'
import { runBingSearches, runBingMobileSearches, runDailyActivities, runRedemption, openLoginBrowser } from './rewards.js'
import { runCompetitionEntry } from './competitions.js'

const app = express()
const PORT = parseInt(process.env.PORT || '3100', 10)

app.use(express.json())

let lastRun = null
let running = false

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
    const result = await runBingSearches({ searchCount, minDelay, maxDelay })
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
    const result = await runBingMobileSearches({ searchCount, minDelay, maxDelay })
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
    const result = await runDailyActivities({ maxActivities })
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
    const result = await runRedemption({ brand, denomination, dryRun })
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
    const result = await runCompetitionEntry({ url, email, firstName, lastName, postcode })
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

// Opens Edge on the Microsoft login page and waits up to 10 min for sign-in.
// Connect via VNC (port 5900) to complete the login interactively.
app.post('/run/login', async (req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }
  running = true
  try {
    const result = await openLoginBrowser()
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
