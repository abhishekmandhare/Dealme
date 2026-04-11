import { useState } from 'react'
import { api } from '../api/client'
import { useFetch } from '../hooks/useFetch'
import type { PriceWatch } from '../types/priceWatch'
import type { ProductSearchResult } from '../types/productSearch'

function formatRelative(iso: string | null): string {
  if (!iso) return 'Never'
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (diff < 60)   return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  return `${Math.floor(diff / 3600)}h ago`
}

// ── Add Watch Form ─────────────────────────────────────────────────────────

interface AddWatchFormProps {
  onAdded: (w: PriceWatch) => void
  onCancel: () => void
}

function AddWatchForm({ onAdded, onCancel }: AddWatchFormProps) {
  const [query,      setQuery]      = useState('')
  const [searching,  setSearching]  = useState(false)
  const [results,    setResults]    = useState<ProductSearchResult[] | null>(null)
  const [selected,   setSelected]   = useState<ProductSearchResult | null>(null)
  const [targetPrice, setTargetPrice] = useState('')
  const [formError,  setFormError]  = useState<string | null>(null)
  const [saving,     setSaving]     = useState(false)

  const search = async () => {
    if (!query.trim()) return
    setSearching(true)
    setResults(null)
    setSelected(null)
    setFormError(null)
    try {
      const data = await api.get<ProductSearchResult[]>(`/pricewatches/search?q=${encodeURIComponent(query)}`)
      setResults(data)
    } catch (e) {
      setFormError(String(e))
    } finally {
      setSearching(false)
    }
  }

  const pick = (r: ProductSearchResult) => {
    setSelected(r)
    setTargetPrice(r.price.toFixed(2))
    setResults(null)
  }

  const save = async () => {
    setFormError(null)
    if (!selected)  { setFormError('Search and select a product first'); return }
    const target = parseFloat(targetPrice)
    if (isNaN(target) || target <= 0) { setFormError('Enter a valid target price'); return }
    setSaving(true)
    try {
      const created = await api.post<PriceWatch>('/pricewatches', {
        name:        selected.name,
        searchQuery: query.trim() || selected.name,
        targetPrice: target,
      })
      onAdded(created)
    } catch (e) {
      setFormError(String(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="channel-form">
      <label>
        Search for a product
        <div className="search-row">
          <input
            type="text"
            placeholder="e.g. MacBook Pro 14, Sony WH-1000XM5"
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && search()}
          />
          <button className="primary" onClick={search} disabled={searching}>
            {searching ? 'Searching…' : 'Search'}
          </button>
        </div>
      </label>

      {results !== null && (
        results.length === 0
          ? <p className="empty" style={{ padding: '8px 0' }}>No results — try a shorter search term.</p>
          : (
            <ul className="search-results">
              {results.map((r, i) => (
                <li key={i} className="search-result-item" onClick={() => pick(r)}>
                  <div className="sr-name">{r.name}</div>
                  <div className="sr-meta">
                    <span className="sr-retailer">{r.retailer}</span>
                    <span className="deal-price">${r.price.toFixed(2)}</span>
                  </div>
                </li>
              ))}
            </ul>
          )
      )}

      {selected && (
        <>
          <div className="selected-product">
            <span className="selected-label">Selected</span>
            <span className="selected-name">{selected.name}</span>
            <span className="sr-retailer">{selected.retailer} · ${selected.price.toFixed(2)}</span>
          </div>
          <label>
            Alert me when price drops to ($)
            <input
              type="number"
              placeholder={selected.price.toFixed(2)}
              value={targetPrice}
              onChange={e => setTargetPrice(e.target.value)}
            />
          </label>
        </>
      )}

      {formError && <p className="error">{formError}</p>}
      <div className="form-actions">
        <button className="primary" onClick={save} disabled={saving || !selected}>
          {saving ? 'Saving…' : 'Add Watch'}
        </button>
        <button className="btn-secondary" onClick={onCancel}>Cancel</button>
      </div>
    </div>
  )
}

// ── Page ───────────────────────────────────────────────────────────────────

export default function PriceWatchesPage() {
  const { data: initial, loading, error } = useFetch<PriceWatch[]>('/pricewatches')
  const [list,   setList]   = useState<PriceWatch[] | null>(null)
  const [adding, setAdding] = useState(false)

  const effective = list ?? initial ?? []

  const handleAdded = (w: PriceWatch) => {
    setList([w, ...effective])
    setAdding(false)
  }

  const remove = async (id: string) => {
    await api.delete(`/pricewatches/${id}`)
    setList(effective.filter(w => w.id !== id))
  }

  const check = async (id: string) => {
    const updated = await api.post<PriceWatch>(`/pricewatches/${id}/check`, {})
    setList(effective.map(w => w.id === id ? updated : w))
  }

  const toggle = (w: PriceWatch) =>
    setList(effective.map(x => x.id === w.id ? { ...x, isActive: !x.isActive } : x))

  if (loading) return <p className="loading">Loading...</p>
  if (error)   return <p className="error">{error}</p>

  return (
    <div>
      <h1>Price Watches</h1>

      {effective.length === 0 && !adding && (
        <p className="empty">No price watches yet. Search for a product to get started.</p>
      )}

      {effective.length > 0 && (
        <ul className="pw-list">
          {effective.map(w => (
            <li key={w.id} className={`pw-item ${!w.isActive ? 'pw-inactive' : ''}`}>
              <div className="pw-main">
                <div className="pw-header">
                  <span className="pw-name">{w.name}</span>
                  {w.currentPrice != null && w.currentPrice <= w.targetPrice && (
                    <span className="pw-alert-badge">Price hit!</span>
                  )}
                </div>
                <div className="pw-prices">
                  <span className="pw-label">Target</span>
                  <span className="deal-price">${w.targetPrice.toFixed(2)}</span>
                  {w.currentPrice != null && (
                    <>
                      <span className="pw-label">Best price</span>
                      <span className={w.currentPrice <= w.targetPrice ? 'deal-price' : 'pw-current-high'}>
                        ${w.currentPrice.toFixed(2)}
                        {w.cheapestRetailer && <span className="sr-retailer"> · {w.cheapestRetailer}</span>}
                      </span>
                    </>
                  )}
                </div>
                <div className="pw-footer">
                  {w.cheapestUrl
                    ? <a href={w.cheapestUrl} target="_blank" rel="noreferrer" className="pw-url">View best deal →</a>
                    : <span className="pw-url muted">Checking prices…</span>
                  }
                  <small>Checked: {formatRelative(w.lastCheckedAt)} · {w.isActive ? 'Active' : 'Paused'}</small>
                </div>
              </div>
              <div className="pw-actions">
                <button className="action-btn action-save" onClick={() => check(w.id)}>Check Now</button>
                <button className="action-btn action-dismiss" onClick={() => toggle(w)}>
                  {w.isActive ? 'Pause' : 'Resume'}
                </button>
                <button className="btn-danger" onClick={() => remove(w.id)}>Remove</button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {adding
        ? <AddWatchForm onAdded={handleAdded} onCancel={() => setAdding(false)} />
        : <button className="primary" style={{ marginTop: effective.length > 0 ? '16px' : '0' }} onClick={() => setAdding(true)}>
            + Add Watch
          </button>
      }
    </div>
  )
}
