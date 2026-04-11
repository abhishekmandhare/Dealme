import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import { useTheme, type Theme } from '../hooks/useTheme'
import type { UserPreferences } from '../types/preferences'
import type { NotificationChannel } from '../types/channel'
import type { AdapterStatus } from '../types/adapter'

// ── Preferences section ────────────────────────────────────────────────────

function PreferencesSection() {
  const { data: fetched, loading, error } = useFetch<UserPreferences>('/settings/preferences')
  const [prefs, setPrefs] = useState<UserPreferences | null>(null)
  const [saved, setSaved] = useState(false)

  const effective = prefs ?? fetched

  const save = async () => {
    if (!effective) return
    await api.put<UserPreferences>('/settings/preferences', effective)
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  if (loading) return <p className="loading">Loading...</p>
  if (error) return <p className="error">{error}</p>
  if (!effective) return null

  return (
    <section className="settings-section">
      <h2>Preferences</h2>
      <div className="settings-form">
        <label>
          Min Deal Value ($)
          <input
            type="number"
            value={effective.minDealValue}
            onChange={e => setPrefs({ ...effective, minDealValue: Number(e.target.value) })}
          />
        </label>
        <label>
          Timezone
          <input
            type="text"
            value={effective.timezone}
            onChange={e => setPrefs({ ...effective, timezone: e.target.value })}
          />
        </label>
        <div>
          <button className="primary" onClick={save}>Save</button>
          {saved && <span className="saved-badge">Saved!</span>}
        </div>
      </div>
    </section>
  )
}

// ── Appearance section ────────────────────────────────────────────────────

const THEME_OPTIONS: { value: Theme; label: string }[] = [
  { value: 'light',  label: 'Light' },
  { value: 'dark',   label: 'Dark' },
  { value: 'system', label: 'System' },
]

function AppearanceSection() {
  const { theme, setTheme } = useTheme()

  return (
    <section className="settings-section">
      <h2>Appearance</h2>
      <div className="theme-picker">
        {THEME_OPTIONS.map(t => (
          <button
            key={t.value}
            className={`theme-option ${theme === t.value ? 'active' : ''}`}
            onClick={() => setTheme(t.value)}
          >
            <span className="theme-option-icon">
              {t.value === 'light' ? '\u2600' : t.value === 'dark' ? '\u263E' : '\uD83D\uDCBB'}
            </span>
            {t.label}
          </button>
        ))}
      </div>
    </section>
  )
}

// ── Channels section ───────────────────────────────────────────────────────

const CHANNEL_TYPES = ['email', 'discord', 'telegram', 'slack', 'ntfy', 'pushover', 'gotify', 'other']

const APPRISE_HINTS: Record<string, string> = {
  discord:  'discord://webhook_id/webhook_token',
  telegram: 'tgram://bot_token/chat_id',
  slack:    'slack://token_a/token_b/token_c',
  ntfy:     'ntfy://ntfy.sh/topic',
  pushover: 'pover://user_key@app_token',
  gotify:   'gotify://hostname/app_token',
  other:    'schema://...',
}

const SMTP_PRESETS: { label: string; server: string; port: string; hint: string }[] = [
  { label: 'Gmail',       server: 'smtp.gmail.com',      port: '587', hint: 'Use an App Password from myaccount.google.com > Security > App passwords' },
  { label: 'Yahoo',       server: 'smtp.mail.yahoo.com', port: '587', hint: 'Use an App Password from login.yahoo.com/account/security' },
  { label: 'Outlook',     server: 'smtp.office365.com',  port: '587', hint: 'Use your Outlook password or an App Password' },
  { label: 'iCloud',      server: 'smtp.mail.me.com',    port: '587', hint: 'Use an App-Specific Password from appleid.apple.com' },
  { label: 'Custom SMTP', server: '',                     port: '587', hint: 'Enter your SMTP server details' },
]

type EmailForm = {
  preset: number
  smtpServer: string
  port: string
  email: string
  password: string
  toEmail: string
  secure: 'tls' | 'ssl'
}

const INITIAL_EMAIL: EmailForm = {
  preset: 0,
  smtpServer: SMTP_PRESETS[0].server,
  port: SMTP_PRESETS[0].port,
  email: '',
  password: '',
  toEmail: '',
  secure: 'tls',
}

function buildAppriseEmailUrl(e: EmailForm): string {
  const scheme = e.secure === 'ssl' ? 'mailtos' : 'mailto'
  const user = encodeURIComponent(e.email)
  const pass = encodeURIComponent(e.password)
  const to = e.toEmail.trim() || e.email
  return `${scheme}://${user}:${pass}@${e.smtpServer}:${e.port}?from=${encodeURIComponent(e.email)}&to=${encodeURIComponent(to)}`
}

function EmailFormFields({ email, onChange }: { email: EmailForm; onChange: (e: EmailForm) => void }) {
  const preset = SMTP_PRESETS[email.preset]
  return (
    <>
      <label>
        Email Provider
        <select
          value={email.preset}
          onChange={ev => {
            const idx = Number(ev.target.value)
            const p = SMTP_PRESETS[idx]
            onChange({ ...email, preset: idx, smtpServer: p.server, port: p.port })
          }}
        >
          {SMTP_PRESETS.map((p, i) => <option key={p.label} value={i}>{p.label}</option>)}
        </select>
      </label>
      <p className="field-hint">{preset.hint}</p>
      <div className="channel-form-row">
        <label>
          SMTP Server
          <input
            type="text"
            placeholder="smtp.example.com"
            value={email.smtpServer}
            onChange={e => onChange({ ...email, smtpServer: e.target.value })}
          />
        </label>
        <label>
          Port
          <input
            type="text"
            placeholder="587"
            value={email.port}
            onChange={e => onChange({ ...email, port: e.target.value })}
          />
        </label>
        <label>
          Security
          <select value={email.secure} onChange={e => onChange({ ...email, secure: e.target.value as 'tls' | 'ssl' })}>
            <option value="tls">STARTTLS (587)</option>
            <option value="ssl">SSL/TLS (465)</option>
          </select>
        </label>
      </div>
      <label>
        Email Address
        <input
          type="email"
          placeholder="you@example.com"
          value={email.email}
          onChange={e => onChange({ ...email, email: e.target.value })}
        />
      </label>
      <label>
        App Password
        <input
          type="password"
          placeholder="Your app-specific password"
          value={email.password}
          onChange={e => onChange({ ...email, password: e.target.value })}
        />
      </label>
      <label>
        Send To <span className="field-optional">(leave blank to send to yourself)</span>
        <input
          type="email"
          placeholder={email.email || 'you@example.com'}
          value={email.toEmail}
          onChange={e => onChange({ ...email, toEmail: e.target.value })}
        />
      </label>
    </>
  )
}

function ChannelsSection() {
  const { data: channels, loading, error } = useFetch<NotificationChannel[]>('/settings/channels')
  const [list, setList] = useState<NotificationChannel[] | null>(null)
  const [adding, setAdding] = useState(false)
  const [channelType, setChannelType] = useState('email')
  const [name, setName] = useState('')
  const [appriseUrl, setAppriseUrl] = useState('')
  const [emailForm, setEmailForm] = useState<EmailForm>(INITIAL_EMAIL)
  const [formError, setFormError] = useState<string | null>(null)

  const effective = list ?? channels ?? []

  const resetForm = () => {
    setChannelType('email')
    setName('')
    setAppriseUrl('')
    setEmailForm(INITIAL_EMAIL)
    setFormError(null)
  }

  const add = async () => {
    setFormError(null)
    if (!name.trim()) { setFormError('Name is required'); return }

    let url = appriseUrl.trim()
    if (channelType === 'email') {
      if (!emailForm.email.trim()) { setFormError('Email address is required'); return }
      if (!emailForm.password.trim()) { setFormError('App password is required'); return }
      if (!emailForm.smtpServer.trim()) { setFormError('SMTP server is required'); return }
      url = buildAppriseEmailUrl(emailForm)
    } else {
      if (!url) { setFormError('Apprise URL is required'); return }
    }

    try {
      const created = await api.post<NotificationChannel>('/settings/channels', {
        type: channelType,
        name: name.trim(),
        appriseUrl: url,
        isEnabled: true,
      })
      setList([...effective, created])
      resetForm()
      setAdding(false)
    } catch (e) {
      setFormError(String(e))
    }
  }

  const remove = async (id: string) => {
    await api.delete(`/settings/channels/${id}`)
    setList(effective.filter(c => c.id !== id))
  }

  if (loading) return <p className="loading">Loading...</p>
  if (error) return <p className="error">{error}</p>

  return (
    <section className="settings-section">
      <h2>Notification Channels</h2>
      <p className="section-hint">
        Get notified about deals and price alerts via email, Discord, and more.
      </p>

      {effective.length === 0 && !adding && (
        <p className="empty">No channels configured. Add one to start receiving notifications.</p>
      )}

      {effective.length > 0 && (
        <ul className="channel-list">
          {effective.map(c => (
            <li key={c.id} className="channel-item">
              <div className="channel-info">
                <span className="channel-type">{c.type}</span>
                <span className="channel-name">{c.name}</span>
                {c.type === 'email'
                  ? <span className="channel-url">{decodeURIComponent(c.appriseUrl.match(/to=([^&]+)/)?.[1] ?? '')}</span>
                  : <span className="channel-url">{c.appriseUrl}</span>
                }
              </div>
              <button className="btn-danger" onClick={() => remove(c.id)}>Remove</button>
            </li>
          ))}
        </ul>
      )}

      {adding ? (
        <div className="channel-form">
          <div className="channel-form-row">
            <label>
              Type
              <select value={channelType} onChange={e => { setChannelType(e.target.value); setAppriseUrl('') }}>
                {CHANNEL_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </label>
            <label>
              Name
              <input
                type="text"
                placeholder={channelType === 'email' ? 'e.g. My Yahoo Email' : `e.g. My ${channelType.charAt(0).toUpperCase() + channelType.slice(1)}`}
                value={name}
                onChange={e => setName(e.target.value)}
              />
            </label>
          </div>

          {channelType === 'email' ? (
            <EmailFormFields email={emailForm} onChange={setEmailForm} />
          ) : (
            <label>
              Apprise URL
              <input
                type="text"
                placeholder={APPRISE_HINTS[channelType] ?? 'schema://...'}
                value={appriseUrl}
                onChange={e => setAppriseUrl(e.target.value)}
              />
            </label>
          )}

          {formError && <p className="error">{formError}</p>}
          <div className="form-actions">
            <button className="primary" onClick={add}>Add Channel</button>
            <button className="btn-secondary" onClick={() => { setAdding(false); resetForm() }}>Cancel</button>
          </div>
        </div>
      ) : (
        <button className="primary" onClick={() => setAdding(true)}>+ Add Channel</button>
      )}
    </section>
  )
}

// ── Adapters section ───────────────────────────────────────────────────────

function formatRelative(iso: string | null): string {
  if (!iso) return 'Never'
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (diff < 60)  return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  return `${Math.floor(diff / 3600)}h ago`
}

function AdaptersSection() {
  const { data: initial, loading, error } = useFetch<AdapterStatus[]>('/adapters/status')
  const [list, setList] = useState<AdapterStatus[] | null>(null)
  const [running, setRunning] = useState<string | null>(null)

  const adapters = list ?? initial ?? []

  const run = async (name: string) => {
    setRunning(name)
    try {
      const updated = await api.post<AdapterStatus>(`/adapters/${name}/run`, {})
      setList(adapters.map(a => a.adapterName === name ? updated : a))
    } finally {
      setRunning(null)
    }
  }

  if (loading) return <p className="loading">Loading...</p>
  if (error)   return <p className="error">{error}</p>

  return (
    <section className="settings-section">
      <h2>Adapter Status</h2>
      <ul className="adapter-list">
        {adapters.map(a => (
          <li key={a.adapterName} className="adapter-item">
            <div className={`adapter-dot ${a.isHealthy ? 'healthy' : 'unhealthy'}`} />
            <div className="adapter-info">
              <span className="adapter-name">{a.adapterName}</span>
              <span className="adapter-meta">
                Last run: {formatRelative(a.lastRunAt)}
                {a.lastRunAt && ` · ${a.itemsLastRun} items`}
              </span>
              {a.lastError && <span className="adapter-error">{a.lastError}</span>}
            </div>
            <button
              className="action-btn action-save"
              onClick={() => run(a.adapterName)}
              disabled={running !== null}
            >
              {running === a.adapterName ? 'Running...' : 'Run'}
            </button>
            <span className={`adapter-badge ${a.isHealthy ? 'badge-ok' : 'badge-err'}`}>
              {a.isHealthy ? 'OK' : 'Error'}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

// ── Page ───────────────────────────────────────────────────────────────────

export default function SettingsPage() {
  return (
    <div>
      <h1>Settings</h1>
      <AppearanceSection />
      <PreferencesSection />
      <ChannelsSection />
      <AdaptersSection />
    </div>
  )
}
