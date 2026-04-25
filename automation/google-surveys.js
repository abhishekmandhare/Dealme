import { execFile } from 'child_process'
import { promisify } from 'util'

const execFileAsync = promisify(execFile)

const ANDROID_HOST  = process.env.ANDROID_HOST  || 'android'
const ANDROID_PORT  = process.env.ANDROID_PORT  || '5555'
const OLLAMA_URL    = process.env.OLLAMA_URL    || 'http://192.169.1.179:30068'
const OLLAMA_MODEL  = process.env.OLLAMA_MODEL  || 'qwen2.5-coder:7b'
const SURVEY_PKG    = 'com.google.android.apps.paidtasks'
const DEVICE_ID     = `${ANDROID_HOST}:${ANDROID_PORT}`

function sleep(ms) { return new Promise(r => setTimeout(r, ms)) }

// Run an adb command on the emulator, returns stdout
async function adb(...args) {
  const { stdout } = await execFileAsync('adb', ['-s', DEVICE_ID, ...args], { timeout: 30000 })
  return stdout.trim()
}

// Connect to the emulator; retries for up to 90s so it has time to boot
async function connectDevice(push) {
  for (let attempt = 1; attempt <= 9; attempt++) {
    try {
      await execFileAsync('adb', ['connect', DEVICE_ID], { timeout: 10000 })
      const devices = await execFileAsync('adb', ['devices'])
      if (devices.stdout.includes(DEVICE_ID)) {
        push(`Connected to Android device (attempt ${attempt})`)
        return true
      }
    } catch { /* ignore — device not ready yet */ }
    push(`Waiting for Android device... (${attempt * 10}s)`)
    await sleep(10000)
  }
  push('ERROR: Android device did not become available within 90s')
  return false
}

// Dump current UI to XML and return it as a string
async function dumpUI() {
  await adb('shell', 'uiautomator', 'dump', '--compressed', '/sdcard/uidump.xml')
  await sleep(500)
  return adb('shell', 'cat', '/sdcard/uidump.xml')
}

// Parse UIAutomator XML into a flat array of node objects
function parseNodes(xml) {
  const nodes = []
  const re = /<node[^>]+>/g
  let m
  while ((m = re.exec(xml)) !== null) {
    const s    = m[0]
    const text = (s.match(/\btext="([^"]*)"/)         || [])[1] || ''
    const desc = (s.match(/content-desc="([^"]*)"/)   || [])[1] || ''
    const cls  = (s.match(/class="([^"]*)"/)           || [])[1] || ''
    const rid  = (s.match(/resource-id="([^"]*)"/)     || [])[1] || ''
    const b    = s.match(/bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/)
    if (!b) continue
    nodes.push({
      text, desc, cls, rid,
      cx: Math.floor((+b[1] + +b[3]) / 2),
      cy: Math.floor((+b[2] + +b[4]) / 2),
    })
  }
  return nodes
}

// Find the first node whose text or desc contains the given substring (case-insensitive)
function findByText(nodes, needle) {
  const lo = needle.toLowerCase()
  return nodes.find(n =>
    n.text.toLowerCase().includes(lo) || n.desc.toLowerCase().includes(lo)
  )
}

// Tap a screen coordinate
async function tap(cx, cy) {
  await adb('shell', 'input', 'tap', String(cx), String(cy))
  await sleep(1200)
}

// Read the current credit balance shown on the app's home screen
// Returns cents (integer) or null if not found
function parseBalance(nodes) {
  for (const n of nodes) {
    // Balance usually looks like "$1.20" or "AUD 1.20"
    const m = (n.text || n.desc).match(/\$\s*([\d]+\.[\d]{2})/)
    if (m) return Math.round(parseFloat(m[1]) * 100)
  }
  return null
}

// Ask Ollama to choose the best answer option
async function askOllama(question, options) {
  const prompt =
    `You are completing a short Google Opinion Rewards survey. ` +
    `Choose the most appropriate and common answer for the question below. ` +
    `Reply with ONLY the exact text of ONE option — nothing else.\n\n` +
    `Question: ${question}\n\n` +
    `Options:\n${options.map((o, i) => `${i + 1}. ${o}`).join('\n')}\n\n` +
    `Best option:`

  const resp = await fetch(`${OLLAMA_URL}/api/generate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ model: OLLAMA_MODEL, prompt, stream: false }),
    signal: AbortSignal.timeout(30000),
  })
  if (!resp.ok) throw new Error(`Ollama HTTP ${resp.status}`)
  const data = await resp.json()
  return data.response.trim()
}

// Match Ollama's free-text answer back to the closest option
function bestMatch(answer, options) {
  const lo = answer.toLowerCase()
  const exact = options.find(o => o.toLowerCase() === lo)
  if (exact) return exact
  const partial = options.find(o =>
    lo.includes(o.toLowerCase()) || o.toLowerCase().includes(lo.split(' ')[0])
  )
  if (partial) return partial
  // Neutral fallback: middle option
  return options[Math.floor(options.length / 2)]
}

export async function runGoogleSurveys() {
  const log = []
  const push = (msg) => { console.log(msg); log.push(msg) }

  push('Starting Google Opinion Rewards survey check')

  if (!await connectDevice(push)) {
    return { success: false, completed: 0, creditsBefore: null, creditsAfter: null, log }
  }

  // Launch the app and wait for it to load
  await adb('shell', 'am', 'start', '-n', `${SURVEY_PKG}/.MainActivity`)
  await sleep(4000)

  let ui = await dumpUI()
  let nodes = parseNodes(ui)
  const creditsBefore = parseBalance(nodes)
  if (creditsBefore != null) push(`Credits before: $${(creditsBefore / 100).toFixed(2)}`)

  // Is a survey waiting?
  const startBtn =
    findByText(nodes, 'Start') ||
    findByText(nodes, 'Take survey') ||
    findByText(nodes, 'Begin') ||
    findByText(nodes, 'Open survey')

  if (!startBtn) {
    push(findByText(nodes, 'check back') || findByText(nodes, 'No surveys')
      ? 'No surveys available right now'
      : 'No survey button found — nothing to do')
    return { success: true, completed: 0, creditsBefore, creditsAfter: null, log }
  }

  push('Survey detected — tapping Start')
  await tap(startBtn.cx, startBtn.cy)
  await sleep(2500)

  let completed = 0

  for (let page = 0; page < 12; page++) {
    ui = await dumpUI()
    nodes = parseNodes(ui)

    // Finished?
    const doneBtn = findByText(nodes, 'Done') || findByText(nodes, 'Finish')
    if (doneBtn) {
      push('Survey complete — tapping Done')
      await tap(doneBtn.cx, doneBtn.cy)
      completed++
      break
    }

    // Find answer options: radio buttons, checkboxes, or similarly-typed nodes
    const optionNodes = nodes.filter(n =>
      (n.cls.includes('RadioButton') ||
       n.cls.includes('CheckBox') ||
       n.rid.includes('option') ||
       n.rid.includes('choice') ||
       n.rid.includes('answer')) &&
      n.text.length > 0
    )

    // Question text: longest visible TextView that isn't a button label
    const buttonLabels = new Set(['Next', 'Back', 'Cancel', 'Skip', 'Submit', 'Continue', 'Done'])
    const questionNode = nodes.find(n =>
      n.cls.includes('TextView') &&
      n.text.length > 10 &&
      !buttonLabels.has(n.text)
    )
    const question = questionNode?.text || ''
    const options  = [...new Set(optionNodes.map(n => n.text))].filter(Boolean)

    if (!question || options.length === 0) {
      // Can't identify question — try tapping Next and moving on
      const nextBtn = findByText(nodes, 'Next') || findByText(nodes, 'Continue')
      if (nextBtn) {
        push(`Page ${page + 1}: layout not recognised, tapping Next`)
        await tap(nextBtn.cx, nextBtn.cy)
        await sleep(1500)
      } else {
        push(`Page ${page + 1}: no recognisable UI, stopping`)
        break
      }
      continue
    }

    push(`Page ${page + 1}: "${question.slice(0, 70)}…" (${options.length} options)`)

    // Ask Ollama which option to pick
    let chosen
    try {
      const answer = await askOllama(question, options)
      chosen = bestMatch(answer, options)
    } catch (e) {
      push(`  Ollama failed (${e.message}) — using middle option`)
      chosen = options[Math.floor(options.length / 2)]
    }
    push(`  → "${chosen}"`)

    const chosenNode = optionNodes.find(n => n.text === chosen)
    if (chosenNode) await tap(chosenNode.cx, chosenNode.cy)

    const nextBtn = findByText(nodes, 'Next') || findByText(nodes, 'Submit') || findByText(nodes, 'Continue')
    if (nextBtn) {
      await tap(nextBtn.cx, nextBtn.cy)
      await sleep(2000)
    }
  }

  // Read balance again to see what we earned
  ui = await dumpUI()
  nodes = parseNodes(ui)
  const creditsAfter = parseBalance(nodes)
  if (creditsAfter != null) push(`Credits after: $${(creditsAfter / 100).toFixed(2)}`)

  const earned = (creditsAfter != null && creditsBefore != null)
    ? creditsAfter - creditsBefore
    : null
  if (earned != null) push(`Earned: $${(earned / 100).toFixed(2)}`)

  push(`Done. Completed ${completed} survey(s).`)
  return { success: true, completed, creditsBefore, creditsAfter, log }
}
