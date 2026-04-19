import express from 'express'
import { runBingSearches, runBingMobileSearches, runDailyActivities } from './rewards.js'

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

app.get('/status', (_req, res) => {
  res.json({ running, lastRun })
})

const server = app.listen(PORT, '0.0.0.0', () => {
  console.log(`Automation service listening on port ${PORT}`)
})

process.on('SIGTERM', () => {
  server.close(() => process.exit(0))
})
