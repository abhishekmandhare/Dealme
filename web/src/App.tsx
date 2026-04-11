import { useState } from 'react'
import type { JSX } from 'react'
import OpportunitiesPage from './pages/OpportunitiesPage'
import PriceWatchesPage from './pages/PriceWatchesPage'
import RewardsPage from './pages/RewardsPage'
import SettingsPage from './pages/SettingsPage'
import { useTheme, type Theme } from './hooks/useTheme'

type Page = 'opportunities' | 'price-watches' | 'rewards' | 'settings'

const pages: { id: Page; label: string }[] = [
  { id: 'opportunities', label: 'Opportunities' },
  { id: 'price-watches', label: 'Price Watches' },
  { id: 'rewards', label: 'Rewards' },
  { id: 'settings', label: 'Settings' },
]

function Logo() {
  return (
    <svg className="app-logo" viewBox="0 0 32 32" width="28" height="28" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="28" height="28" rx="8" fill="var(--accent)" />
      <path d="M10 12.5L16 8l6 4.5v7L16 24l-6-4.5v-7z" fill="var(--accent-hover)" stroke="#fff" strokeWidth="1.2" strokeLinejoin="round" />
      <text x="16" y="18.5" textAnchor="middle" fill="#fff" fontSize="8" fontWeight="800" fontFamily="sans-serif">$</text>
    </svg>
  )
}

const THEME_CYCLE: Theme[] = ['light', 'dark', 'system']

function SunIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="5" />
      <line x1="12" y1="1" x2="12" y2="3" /><line x1="12" y1="21" x2="12" y2="23" />
      <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" /><line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
      <line x1="1" y1="12" x2="3" y2="12" /><line x1="21" y1="12" x2="23" y2="12" />
      <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" /><line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  )
}

function MonitorIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" />
    </svg>
  )
}

const THEME_ICON_MAP: Record<Theme, () => JSX.Element> = {
  light: SunIcon,
  dark: MoonIcon,
  system: MonitorIcon,
}

function ThemeToggle({ theme, setTheme }: { theme: Theme; setTheme: (t: Theme) => void }) {
  const next = THEME_CYCLE[(THEME_CYCLE.indexOf(theme) + 1) % THEME_CYCLE.length]
  const Icon = THEME_ICON_MAP[theme]
  return (
    <button
      className="theme-toggle"
      onClick={() => setTheme(next)}
      title={`Theme: ${theme} (click for ${next})`}
      aria-label={`Switch to ${next} theme`}
    >
      <Icon />
    </button>
  )
}

export default function App() {
  const [page, setPage] = useState<Page>('opportunities')
  const { theme, setTheme } = useTheme()

  return (
    <>
      <nav>
        <Logo />
        <span className="brand">DealMe</span>
        {pages.map(p => (
          <button
            key={p.id}
            className={page === p.id ? 'active' : undefined}
            onClick={() => setPage(p.id)}
          >
            {p.label}
          </button>
        ))}
        <ThemeToggle theme={theme} setTheme={setTheme} />
      </nav>
      <main>
        {page === 'opportunities' && <OpportunitiesPage />}
        {page === 'price-watches' && <PriceWatchesPage />}
        {page === 'rewards' && <RewardsPage />}
        {page === 'settings' && <SettingsPage />}
      </main>
    </>
  )
}
