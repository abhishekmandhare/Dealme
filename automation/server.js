import express from 'express'
import { runBingSearches } from './rewards.js'

const app = express()
const PORT = parseInt(process.env.PORT || '3100', 10)

let lastRun = null
let running = false

app.get('/health', (_req, res) => {
  res.json({ status: 'ok', lastRun })
})

app.post('/run/bing-searches', async (_req, res) => {
  if (running) {
    return res.status(409).json({ error: 'Already running' })
  }

  running = true
  try {
    const result = await runBingSearches()
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

app.get('/status', (_req, res) => {
  res.json({ running, lastRun })
})

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Automation service listening on port ${PORT}`)
})
