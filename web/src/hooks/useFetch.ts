import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'

interface FetchState<T> {
  data: T | null
  loading: boolean
  error: string | null
  refetch: () => void
}

export function useFetch<T>(path: string): FetchState<T> {
  const [tick, setTick] = useState(0)
  const [state, setState] = useState<Omit<FetchState<T>, 'refetch'>>({ data: null, loading: true, error: null })

  useEffect(() => {
    setState(s => ({ ...s, loading: true }))
    api.get<T>(path)
      .then(data => setState({ data, loading: false, error: null }))
      .catch(e => setState({ data: null, loading: false, error: String(e) }))
  }, [path, tick])

  const refetch = useCallback(() => setTick(t => t + 1), [])

  return { ...state, refetch }
}
