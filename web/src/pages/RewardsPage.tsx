import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import type { AutomationProvider, AutomationRun, AutomationStats } from '../types/automation'

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
  const { data: history } = useFetch<AutomationRun[]>(`/automation/providers/${provider.id}/history?limit=20`)
  const [running, setRunning] = useState(false)
  const [lastResult, setLastResult] = useState<AutomationRun | null>(null)

  const runTask = async (taskId: string) => {
    setRunning(true)
    try {
      const result = await api.post<AutomationRun>(`/automation/providers/${provider.id}/run/${taskId}`, {})
      setLastResult(result)
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

      {/* Tasks */}
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

      {/* Future providers placeholder */}
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
