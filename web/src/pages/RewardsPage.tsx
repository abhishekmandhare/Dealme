import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import type { AutomationProvider, AutomationRun, AutomationStats, AutomationConfig } from '../types/automation'

function formatTime(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleString('en-AU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
}

function formatRelative(iso: string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return `${Math.floor(diff / 86400)}d ago`
}

function formatDuration(start: string, end: string): string {
  const secs = Math.floor((new Date(end).getTime() - new Date(start).getTime()) / 1000)
  if (secs < 60) return `${secs}s`
  return `${Math.floor(secs / 60)}m ${secs % 60}s`
}

/** Convert cron "M H * * *" (UTC) → AEST time string "HH:MM" for a time input */
function cronToAestTime(cron: string): string {
  const parts = cron.split(' ')
  if (parts.length === 5 && parts[2] === '*' && parts[3] === '*' && parts[4] === '*') {
    const min = parseInt(parts[0], 10)
    const hourUtc = parseInt(parts[1], 10)
    if (!isNaN(min) && !isNaN(hourUtc)) {
      const hourAest = (hourUtc + 10) % 24
      return `${String(hourAest).padStart(2, '0')}:${String(min).padStart(2, '0')}`
    }
  }
  return ''
}

/** Convert AEST time "HH:MM" → cron string (UTC) */
function aestTimeToCron(time: string): string {
  const [h, m] = time.split(':').map(Number)
  const hourUtc = (h - 10 + 24) % 24
  return `${m} ${hourUtc} * * *`
}

function isSimpleDailyCron(cron: string): boolean {
  const parts = cron.split(' ')
  return parts.length === 5 && parts[2] === '*' && parts[3] === '*' && parts[4] === '*'
}

// ── Config edit section ────────────────────────────────────────────────────

function ConfigSection({ providerId }: { providerId: string }) {
  const { data: config, loading, error, refetch } = useFetch<AutomationConfig>(
    `/automation/providers/${providerId}/config`
  )
  const [editing, setEditing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  // Edit form state
  const [label, setLabel] = useState('')
  const [aestTime, setAestTime] = useState('07:00')
  const [searchCount, setSearchCount] = useState(33)
  const [mobileSearchCount, setMobileSearchCount] = useState(20)
  const [minDelay, setMinDelay] = useState(8000)
  const [maxDelay, setMaxDelay] = useState(20000)
  const [enabled, setEnabled] = useState(true)
  const [useCustomCron, setUseCustomCron] = useState(false)
  const [customCron, setCustomCron] = useState('')

  const openEdit = () => {
    if (!config) return
    setLabel(config.accountLabel ?? '')
    const simple = isSimpleDailyCron(config.cronSchedule)
    setUseCustomCron(!simple)
    setAestTime(simple ? cronToAestTime(config.cronSchedule) : '07:00')
    setCustomCron(config.cronSchedule)
    setSearchCount(config.searchCount)
    setMobileSearchCount(config.mobileSearchCount)
    setMinDelay(config.minDelayMs)
    setMaxDelay(config.maxDelayMs)
    setEnabled(config.enabled)
    setSaveError(null)
    setEditing(true)
  }

  const save = async () => {
    if (!config) return
    setSaving(true)
    setSaveError(null)
    try {
      const cron = useCustomCron ? customCron : aestTimeToCron(aestTime)
      await api.put<AutomationConfig>(`/automation/providers/${providerId}/config`, {
        providerId,
        accountLabel: label || null,
        cronSchedule: cron,
        searchCount,
        mobileSearchCount,
        minDelayMs: minDelay,
        maxDelayMs: maxDelay,
        enabled,
      })
      refetch?.()
      setEditing(false)
    } catch (e) {
      setSaveError(String(e))
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="rw-config-section"><p className="loading">Loading config...</p></div>
  if (error) return null

  return (
    <div className="rw-config-section">
      <div className="rw-config-header">
        <h3>Account Settings</h3>
        {!editing && (
          <button className="rw-config-edit-btn" onClick={openEdit}>Edit</button>
        )}
      </div>

      {!editing ? (
        <div className="rw-config-fields">
          <div className="rw-config-field">
            <span className="rw-config-label">Account</span>
            <span className="rw-config-value">{config?.accountLabel || <em>No label set</em>}</span>
          </div>
          <div className="rw-config-field">
            <span className="rw-config-label">Schedule</span>
            <span className="rw-config-value">
              {config && isSimpleDailyCron(config.cronSchedule)
                ? `Daily at ${cronToAestTime(config.cronSchedule)} AEST`
                : config?.cronSchedule}
              {config && !config.enabled && <span className="rw-config-disabled"> (disabled)</span>}
            </span>
          </div>
          <div className="rw-config-field">
            <span className="rw-config-label">Desktop searches</span>
            <span className="rw-config-value">{config?.searchCount}/day</span>
          </div>
          <div className="rw-config-field">
            <span className="rw-config-label">Mobile searches</span>
            <span className="rw-config-value">{config?.mobileSearchCount}/day</span>
          </div>
          <div className="rw-config-field">
            <span className="rw-config-label">Delay between searches</span>
            <span className="rw-config-value">
              {config ? `${config.minDelayMs / 1000}s – ${config.maxDelayMs / 1000}s` : '—'}
            </span>
          </div>
        </div>
      ) : (
        <div className="rw-config-form">
          <label>
            Account label
            <input
              type="text"
              value={label}
              onChange={e => setLabel(e.target.value)}
              placeholder="e.g. John's account"
            />
          </label>

          <label>
            <div className="rw-config-schedule-header">
              Daily run time (AEST)
              <button
                type="button"
                className="rw-config-cron-toggle"
                onClick={() => setUseCustomCron(v => !v)}
              >
                {useCustomCron ? 'Use time picker' : 'Use cron'}
              </button>
            </div>
            {useCustomCron ? (
              <input
                type="text"
                value={customCron}
                onChange={e => setCustomCron(e.target.value)}
                placeholder="0 21 * * *  (UTC)"
              />
            ) : (
              <input
                type="time"
                value={aestTime}
                onChange={e => setAestTime(e.target.value)}
              />
            )}
          </label>

          <div className="rw-config-delay-row">
            <label>
              Desktop searches/day
              <input
                type="number"
                min={1}
                max={33}
                value={searchCount}
                onChange={e => setSearchCount(parseInt(e.target.value, 10))}
              />
            </label>
            <label>
              Mobile searches/day
              <input
                type="number"
                min={1}
                max={20}
                value={mobileSearchCount}
                onChange={e => setMobileSearchCount(parseInt(e.target.value, 10))}
              />
            </label>
          </div>

          <div className="rw-config-delay-row">
            <label>
              Min delay (ms)
              <input
                type="number"
                min={1000}
                step={500}
                value={minDelay}
                onChange={e => setMinDelay(parseInt(e.target.value, 10))}
              />
            </label>
            <label>
              Max delay (ms)
              <input
                type="number"
                min={1000}
                step={500}
                value={maxDelay}
                onChange={e => setMaxDelay(parseInt(e.target.value, 10))}
              />
            </label>
          </div>

          <label className="rw-config-enabled-row">
            <input
              type="checkbox"
              checked={enabled}
              onChange={e => setEnabled(e.target.checked)}
            />
            Enable scheduled runs
          </label>

          {saveError && <p className="error">{saveError}</p>}

          <div className="rw-config-actions">
            <button className="primary" onClick={save} disabled={saving}>
              {saving ? 'Saving...' : 'Save'}
            </button>
            <button onClick={() => setEditing(false)} disabled={saving}>
              Cancel
            </button>
          </div>
        </div>
      )}
    </div>
  )
}

// ── Provider list ─────────────────────────────────────────────────────────

function ProviderCard({ provider, onClick }: { provider: AutomationProvider; onClick: () => void }) {
  const lastRun = provider.lastRun
  const healthy = provider.serviceHealthy

  return (
    <li className="rw-provider-card" onClick={onClick}>
      <div className="rw-provider-header">
        <div className={`adapter-dot ${healthy ? 'healthy' : 'unhealthy'}`} />
        <div className="rw-provider-info">
          <span className="rw-provider-name">{provider.name}</span>
          <span className="rw-provider-desc">{provider.description}</span>
        </div>
      </div>
      <div className="rw-provider-tasks">
        {provider.tasks.map(t => (
          <div key={t.id} className="rw-task-chip">
            <span className="rw-task-name">{t.name}</span>
            <span className="rw-task-points">~{t.maxPointsPerDay} pts/day</span>
          </div>
        ))}
      </div>
      <div className="rw-provider-footer">
        {lastRun ? (
          <>
            <span className={`rw-run-status ${lastRun.success ? 'success' : 'failed'}`}>
              {lastRun.success ? 'Last run passed' : 'Last run failed'}
            </span>
            <span className="rw-run-time">{formatRelative(lastRun.completedAt)}</span>
            {lastRun.pointsAfter != null && (
              <span className="rw-points-badge">{lastRun.pointsAfter.toLocaleString()} pts</span>
            )}
          </>
        ) : (
          <span className="rw-run-status pending">Never run</span>
        )}
      </div>
    </li>
  )
}

// ── Provider detail ───────────────────────────────────────────────────────

function ProviderDetail({ provider, onBack }: { provider: AutomationProvider; onBack: () => void }) {
  const { data: stats } = useFetch<AutomationStats>(`/automation/providers/${provider.id}/stats`)
  const { data: history, refetch: refetchHistory } = useFetch<AutomationRun[]>(
    `/automation/providers/${provider.id}/history?limit=20`
  )
  const [running, setRunning] = useState(false)
  const [lastResult, setLastResult] = useState<AutomationRun | null>(null)

  const runTask = async (taskId: string) => {
    setRunning(true)
    try {
      const result = await api.post<AutomationRun>(`/automation/providers/${provider.id}/run/${taskId}`, {})
      setLastResult(result)
      refetchHistory?.()
    } catch (e) {
      setLastResult({ success: false, error: String(e) } as AutomationRun)
    } finally {
      setRunning(false)
    }
  }

  return (
    <div className="rw-detail">
      <button className="rw-back" onClick={onBack}>&larr; Back to Rewards</button>

      <div className="rw-detail-header">
        <h2>{provider.name}</h2>
        <div className={`rw-service-badge ${provider.serviceHealthy ? 'online' : 'offline'}`}>
          {provider.serviceHealthy ? 'Service Online' : 'Service Offline'}
        </div>
      </div>

      <p className="rw-detail-desc">{provider.description}</p>

      {/* Stats */}
      {stats && (
        <div className="rw-stats-grid">
          <div className="rw-stat-card">
            <span className="rw-stat-value">{stats.currentPoints?.toLocaleString() ?? '—'}</span>
            <span className="rw-stat-label">Current Points</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">~{stats.today.pointsEstimate}</span>
            <span className="rw-stat-label">Today</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">~{stats.week.pointsEstimate}</span>
            <span className="rw-stat-label">This Week</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">~{stats.month.pointsEstimate}</span>
            <span className="rw-stat-label">This Month</span>
          </div>
        </div>
      )}

      {/* Tasks with Run Now */}
      <h3>Tasks</h3>
      <ul className="rw-task-list">
        {provider.tasks.map(t => (
          <li key={t.id} className="rw-task-item">
            <div className="rw-task-info">
              <span className="rw-task-title">{t.name}</span>
              <span className="rw-task-desc">{t.description}</span>
              <div className="rw-task-meta">
                <span className="rw-schedule-badge">{t.schedule}</span>
                <span className="rw-points-badge">~{t.maxPointsPerDay} pts/day</span>
              </div>
            </div>
            <button
              className="primary"
              onClick={() => runTask(t.id)}
              disabled={running || !provider.serviceHealthy}
            >
              {running ? 'Running...' : 'Run Now'}
            </button>
          </li>
        ))}
      </ul>

      {/* Last result feedback */}
      {lastResult && (
        <div className={`rw-result ${lastResult.success ? 'success' : 'failed'}`}>
          {lastResult.success
            ? `Completed ${lastResult.itemsCompleted}/${lastResult.itemsTotal} searches${lastResult.pointsAfter != null ? ` — ${lastResult.pointsAfter.toLocaleString()} points` : ''}`
            : `Failed: ${lastResult.error}`
          }
        </div>
      )}

      {/* Account settings (editable config) */}
      <ConfigSection providerId={provider.id} />

      {/* Login info */}
      {provider.loginRequired && (
        <div className="rw-login-section">
          <h3>Login Setup</h3>
          <p className="rw-login-text">{provider.loginInstructions}</p>
          <div className="rw-login-steps">
            <div className="rw-step">
              <span className="rw-step-num">1</span>
              <span>SSH into your server and navigate to the automation directory</span>
            </div>
            <div className="rw-step">
              <span className="rw-step-num">2</span>
              <span>Run <code>node login.js</code> — a browser window will open</span>
            </div>
            <div className="rw-step">
              <span className="rw-step-num">3</span>
              <span>Log in to your Microsoft account and close the browser when done</span>
            </div>
          </div>
        </div>
      )}

      {/* Run history */}
      <h3>Run History</h3>
      {!history || history.length === 0 ? (
        <p className="empty">No runs recorded yet.</p>
      ) : (
        <ul className="rw-history">
          {history.map(r => (
            <li key={r.id} className={`rw-history-item ${r.success ? '' : 'failed'}`}>
              <div className={`adapter-dot ${r.success ? 'healthy' : 'unhealthy'}`} />
              <div className="rw-history-info">
                <span className="rw-history-task">{r.task}</span>
                <span className="rw-history-meta">
                  {r.itemsCompleted}/{r.itemsTotal} completed
                  {r.pointsAfter != null && ` · ${r.pointsAfter.toLocaleString()} pts`}
                  {' · '}{formatDuration(r.startedAt, r.completedAt)}
                </span>
                {r.error && <span className="rw-history-error">{r.error}</span>}
              </div>
              <span className="rw-history-time">{formatTime(r.startedAt)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────

export default function RewardsPage() {
  const { data: providers, loading, error } = useFetch<AutomationProvider[]>('/automation/providers')
  const [selected, setSelected] = useState<string | null>(null)

  if (loading) return <p className="loading">Loading...</p>
  if (error) return <p className="error">{error}</p>

  const selectedProvider = providers?.find(p => p.id === selected)

  if (selectedProvider) {
    return <ProviderDetail provider={selectedProvider} onBack={() => setSelected(null)} />
  }

  return (
    <div>
      <h1>Rewards & Automation</h1>
      <p className="rw-intro">
        Automated tasks that earn points and rewards. Each provider runs on a daily schedule.
      </p>

      {!providers || providers.length === 0 ? (
        <p className="empty">No automation providers configured.</p>
      ) : (
        <ul className="rw-provider-list">
          {providers.map(p => (
            <ProviderCard key={p.id} provider={p} onClick={() => setSelected(p.id)} />
          ))}
        </ul>
      )}

      <div className="rw-coming-soon">
        <h3>Coming Soon</h3>
        <ul className="rw-future-list">
          <li className="rw-future-item">
            <span className="rw-future-name">Google Opinion Rewards</span>
            <span className="rw-future-desc">Survey notifications and tracking</span>
          </li>
          <li className="rw-future-item">
            <span className="rw-future-name">Flybuys</span>
            <span className="rw-future-desc">Offer tracking and boost reminders</span>
          </li>
          <li className="rw-future-item">
            <span className="rw-future-name">Everyday Rewards</span>
            <span className="rw-future-desc">Woolworths bonus offer alerts</span>
          </li>
          <li className="rw-future-item">
            <span className="rw-future-name">Qantas Frequent Flyer</span>
            <span className="rw-future-desc">Bonus points promotions</span>
          </li>
        </ul>
      </div>
    </div>
  )
}
