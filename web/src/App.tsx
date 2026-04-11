import { useState } from 'react'
import OpportunitiesPage from './pages/OpportunitiesPage'
import PriceWatchesPage from './pages/PriceWatchesPage'
import SettingsPage from './pages/SettingsPage'

type Page = 'opportunities' | 'price-watches' | 'settings'

const pages: { id: Page; label: string }[] = [
  { id: 'opportunities', label: 'Opportunities' },
  { id: 'price-watches', label: 'Price Watches' },
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

export default function App() {
  const [page, setPage] = useState<Page>('opportunities')

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
      </nav>
      <main>
        {page === 'opportunities' && <OpportunitiesPage />}
        {page === 'price-watches' && <PriceWatchesPage />}
        {page === 'settings' && <SettingsPage />}
      </main>
    </>
  )
}
