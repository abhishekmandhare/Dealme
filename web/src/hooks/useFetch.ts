import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface FetchState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

export function useFetch<T>(path: string): FetchState<T> {
  const [state, setState] = useState<FetchState<T>>({ data: null, loading: true, error: null })

  useEffect(() => {
    api.get<T>(path)
      .then(data => setState({ data, loading: false, error: null }))
      .catch(e => setState({ data: null, loading: false, error: String(e) }))
  }, [path])

  return state
}
