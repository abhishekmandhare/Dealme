import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import type { AvailableCompetition, CompetitionEntry, CompetitionStats } from '../types/competitions'

type Tab = 'available' | 'entered'

function formatTime(iso: string | null): string {
  if (!iso) return '—'
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

const STATUS_CLASS: Record<CompetitionEntry['status'], string> = {
  Entered: 'success',
  Skipped: 'pending',
  Failed: 'failed',
  Pending: 'pending',
}

export default function CompetitionsPage() {
  const [tab, setTab] = useState<Tab>('available')
  const { data: stats, refetch: refetchStats } = useFetch<CompetitionStats>('/competitions/stats')
  const { data: available, refetch: refetchAvailable } = useFetch<AvailableCompetition[]>('/competitions/available?limit=50')
  const { data: entered, refetch: refetchEntered } = useFetch<CompetitionEntry[]>('/competitions/entered?limit=100')
  const [running, setRunning] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const runBatch = async () => {
    setRunning(true)
    setMessage(null)
    try {
      await api.post('/competitions/run-batch', {})
      setMessage('Batch started. Refresh in a minute to see results.')
      setTimeout(() => {
        refetchStats?.()
        refetchAvailable?.()
        refetchEntered?.()
      }, 3000)
    } catch (e) {
      setMessage(`Failed: ${e}`)
    } finally {
      setRunning(false)
    }
  }

  return (
    <div>
      <h1>Competitions</h1>
      <p className="rw-intro">
        Free-entry comps pulled from OzBargain. Scheduled hourly batch auto-enters simple email-only forms —
        captcha/complex/social-gated comps are skipped and listed for manual entry.
      </p>

      {stats && (
        <div className="rw-stats-grid">
          <div className="rw-stat-card">
            <span className="rw-stat-value">{stats.available}</span>
            <span className="rw-stat-label">Available</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">{stats.entered}</span>
            <span className="rw-stat-label">Entered</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">{stats.skipped}</span>
            <span className="rw-stat-label">Skipped</span>
          </div>
          <div className="rw-stat-card">
            <span className="rw-stat-value">{stats.failed}</span>
            <span className="rw-stat-label">Failed</span>
          </div>
        </div>
      )}

      <div className="rw-tabs">
        <button
          className={`rw-tab ${tab === 'available' ? 'active' : ''}`}
          onClick={() => setTab('available')}
        >
          Available ({available?.length ?? 0})
        </button>
        <button
          className={`rw-tab ${tab === 'entered' ? 'active' : ''}`}
          onClick={() => setTab('entered')}
        >
          Entered ({entered?.length ?? 0})
        </button>
        <button
          className="primary"
          onClick={runBatch}
          disabled={running}
          style={{ marginLeft: 'auto' }}
        >
          {running ? 'Running...' : 'Run Batch Now'}
        </button>
      </div>

      {message && <p className="rw-result">{message}</p>}

      {tab === 'available' && (
        <ul className="rw-history">
          {!available || available.length === 0 ? (
            <p className="empty">No pending competitions. Wait for the next RSS poll.</p>
          ) : (
            available.map(c => (
              <li key={c.id} className="rw-history-item">
                <div className="adapter-dot pending" />
                <div className="rw-history-info">
                  <a href={c.url} target="_blank" rel="noopener noreferrer" className="rw-history-task">
                    {c.title} ↗
                  </a>
                  <span className="rw-history-meta">
                    {c.source} · posted {formatRelative(c.createdAt)}
                    {c.expiresAt && ` · expires ${formatTime(c.expiresAt)}`}
                  </span>
                </div>
              </li>
            ))
          )}
        </ul>
      )}

      {tab === 'entered' && (
        <ul className="rw-history">
          {!entered || entered.length === 0 ? (
            <p className="empty">No entries yet. Hit Run Batch or wait for the hourly schedule.</p>
          ) : (
            entered.map(e => (
              <li key={e.id} className={`rw-history-item ${e.status === 'Failed' ? 'failed' : ''}`}>
                <div className={`adapter-dot ${STATUS_CLASS[e.status]}`} />
                <div className="rw-history-info">
                  <a href={e.competitionUrl} target="_blank" rel="noopener noreferrer" className="rw-history-task">
                    {e.title} ↗
                  </a>
                  <span className="rw-history-meta">
                    <strong>{e.status}</strong>
                    {e.reason && ` · ${e.reason}`}
                    {e.emailUsed && ` · ${e.emailUsed}`}
                    {e.attemptedAt && ` · ${formatRelative(e.attemptedAt)}`}
                  </span>
                </div>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}
