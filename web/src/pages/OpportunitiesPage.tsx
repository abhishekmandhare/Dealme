import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import { getCategory, getCategories } from '../lib/categories'
import type { Opportunity, OpportunityType, OpportunityStatus } from '../types/opportunity'

const TYPE_TABS: { type: OpportunityType | 'All'; label: string }[] = [
  { type: 'All',        label: 'All' },
  { type: 'Deal',       label: 'Deals' },
  { type: 'Cashback',   label: 'Cashback' },
  { type: 'Bonus',      label: 'Credit Card' },
  { type: 'Rebate',     label: 'Rebates' },
  { type: 'PriceAlert', label: 'Price Alerts' },
]

const STATUS_FILTERS: { value: OpportunityStatus | 'Active'; label: string }[] = [
  { value: 'Active',    label: 'Active' },
  { value: 'Saved',     label: 'Saved' },
  { value: 'Dismissed', label: 'Dismissed' },
]

function DealPricing({ o }: { o: Opportunity }) {
  if (!o.value && !o.discountPercent) return null
  return (
    <div className="deal-pricing">
      {o.value          != null && <span className="deal-price">${o.value.toFixed(2)}</span>}
      {o.originalValue  != null && <span className="deal-original">${o.originalValue.toFixed(2)}</span>}
      {o.discountPercent != null && <span className="deal-badge">{o.discountPercent}% off</span>}
    </div>
  )
}

function OpportunityActions({ o, onStatusChange }: {
  o: Opportunity
  onStatusChange: (id: string, status: OpportunityStatus) => void
}) {
  const [busy, setBusy] = useState(false)

  const setStatus = async (status: OpportunityStatus) => {
    setBusy(true)
    try {
      await api.patch(`/opportunities/${o.id}/status`, { status })
      onStatusChange(o.id, status)
    } finally {
      setBusy(false)
    }
  }

  if (busy) return <span className="action-busy">...</span>

  if (o.status === 'Saved') {
    return (
      <div className="opp-actions">
        <button className="action-btn action-unsave" onClick={() => setStatus('New')}>Unsave</button>
        <button className="action-btn action-dismiss" onClick={() => setStatus('Dismissed')}>Dismiss</button>
      </div>
    )
  }

  if (o.status === 'Dismissed') {
    return (
      <div className="opp-actions">
        <button className="action-btn action-restore" onClick={() => setStatus('New')}>Restore</button>
      </div>
    )
  }

  // New / Seen
  return (
    <div className="opp-actions">
      <button className="action-btn action-save" onClick={() => setStatus('Saved')}>Save</button>
      <button className="action-btn action-acted" onClick={() => setStatus('ActedOn')}>Acted On</button>
      <button className="action-btn action-dismiss" onClick={() => setStatus('Dismissed')}>Dismiss</button>
    </div>
  )
}

type SortOption = 'newest' | 'oldest' | 'price-low' | 'price-high' | 'discount'

const SORT_OPTIONS: { value: SortOption; label: string }[] = [
  { value: 'newest',     label: 'Newest first' },
  { value: 'oldest',     label: 'Oldest first' },
  { value: 'price-low',  label: 'Price: low to high' },
  { value: 'price-high', label: 'Price: high to low' },
  { value: 'discount',   label: 'Biggest discount' },
]

function sortOpportunities(list: Opportunity[], sort: SortOption): Opportunity[] {
  return [...list].sort((a, b) => {
    switch (sort) {
      case 'newest':     return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      case 'oldest':     return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
      case 'price-low':  return (a.value ?? Infinity) - (b.value ?? Infinity)
      case 'price-high': return (b.value ?? -Infinity) - (a.value ?? -Infinity)
      case 'discount':   return (b.discountPercent ?? 0) - (a.discountPercent ?? 0)
    }
  })
}

export default function OpportunitiesPage() {
  const { data: initial, loading, error } = useFetch<Opportunity[]>('/opportunities')
  const [overrides, setOverrides] = useState<Record<string, OpportunityStatus>>({})
  const [activeType,     setActiveType]     = useState<OpportunityType | 'All'>('All')
  const [activeCategory, setActiveCategory] = useState('All')
  const [activeStatus,   setActiveStatus]   = useState<OpportunityStatus | 'Active'>('Active')
  const [search,         setSearch]         = useState('')
  const [sort,           setSort]           = useState<SortOption>('newest')

  if (loading) return <p className="loading">Loading...</p>
  if (error)   return <p className="error">{error}</p>

  const all = (initial ?? []).map(o =>
    overrides[o.id] ? { ...o, status: overrides[o.id] } : o
  )

  const handleStatusChange = (id: string, status: OpportunityStatus) =>
    setOverrides(prev => ({ ...prev, [id]: status }))

  // Status filter
  const byStatus = activeStatus === 'Active'
    ? all.filter(o => o.status === 'New' || o.status === 'Seen' || o.status === 'ActedOn')
    : all.filter(o => o.status === activeStatus)

  // Type tabs
  const byType = activeType === 'All' ? byStatus : byStatus.filter(o => o.type === activeType)

  // Search filter
  const searchLower = search.toLowerCase().trim()
  const bySearch = searchLower
    ? byType.filter(o =>
        o.title.toLowerCase().includes(searchLower) ||
        o.source.toLowerCase().includes(searchLower) ||
        o.tags.some(t => t.toLowerCase().includes(searchLower)))
    : byType

  // Category chips (Deals only)
  const showCategories = activeType === 'Deal'
  const categories = showCategories ? getCategories(bySearch) : []
  const filtered = showCategories && activeCategory !== 'All'
    ? bySearch.filter(o => getCategory(o.tags) === activeCategory)
    : bySearch

  // Sort
  const opportunities = sortOpportunities(filtered, sort)

  const typeCounts = Object.fromEntries(
    TYPE_TABS.map(t => [t.type, t.type === 'All' ? byStatus.length : byStatus.filter(o => o.type === t.type).length])
  )
  const visibleTypeTabs = TYPE_TABS.filter(t => typeCounts[t.type] > 0)

  const catCounts = Object.fromEntries(
    categories.map(c => [c, c === 'All' ? byType.length : byType.filter(o => getCategory(o.tags) === c).length])
  )

  const statusCounts = Object.fromEntries(
    STATUS_FILTERS.map(s => [
      s.value,
      s.value === 'Active'
        ? all.filter(o => o.status === 'New' || o.status === 'Seen' || o.status === 'ActedOn').length
        : all.filter(o => o.status === s.value).length
    ])
  )

  return (
    <div>
      <h1>Opportunities</h1>

      {/* Status filter */}
      <div className="status-filters">
        {STATUS_FILTERS.map(s => (
          <button
            key={s.value}
            className={activeStatus === s.value ? 'active' : undefined}
            onClick={() => { setActiveStatus(s.value); setActiveType('All'); setActiveCategory('All') }}
          >
            {s.label}
            {statusCounts[s.value] > 0 && <span className="tab-count">{statusCounts[s.value]}</span>}
          </button>
        ))}
      </div>

      {/* Type tabs */}
      {visibleTypeTabs.length > 1 && (
        <div className="type-tabs">
          {visibleTypeTabs.map(t => (
            <button
              key={t.type}
              className={activeType === t.type ? 'active' : undefined}
              onClick={() => { setActiveType(t.type); setActiveCategory('All') }}
            >
              {t.label}
              <span className="tab-count">{typeCounts[t.type]}</span>
            </button>
          ))}
        </div>
      )}

      {/* Category chips */}
      {showCategories && categories.length > 2 && (
        <div className="category-chips">
          {categories.map(c => (
            <button
              key={c}
              className={activeCategory === c ? 'active' : undefined}
              onClick={() => setActiveCategory(c)}
            >
              {c}
              <span className="chip-count">{catCounts[c]}</span>
            </button>
          ))}
        </div>
      )}

      {/* Search & sort */}
      <div className="opp-toolbar">
        <input
          type="text"
          className="opp-search"
          placeholder="Search deals..."
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
        <select value={sort} onChange={e => setSort(e.target.value as SortOption)}>
          {SORT_OPTIONS.map(s => (
            <option key={s.value} value={s.value}>{s.label}</option>
          ))}
        </select>
      </div>

      {opportunities.length === 0
        ? <p className="empty">Nothing here.</p>
        : (
          <ul>
            {opportunities.map(o => (
              <li key={o.id} className={`opp-item opp-${o.status.toLowerCase()}`}>
                <div className="opp-main">
                  <a href={o.url} target="_blank" rel="noreferrer">{o.title}</a>
                  <DealPricing o={o} />
                  <div className="opp-meta">
                    <span className="opp-category-tag">{getCategory(o.tags)}</span>
                    <small>{o.source} · {o.status}</small>
                  </div>
                </div>
                <OpportunityActions o={o} onStatusChange={handleStatusChange} />
              </li>
            ))}
          </ul>
        )
      }
    </div>
  )
}
