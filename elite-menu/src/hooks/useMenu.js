import { useCallback, useEffect, useState } from 'react'
import { fetchConfig, fetchProducts } from '../utils/api'
import { restaurantDisplayName } from '../utils/guestMenuConfig'

export function useMenu() {
  const [config, setConfig] = useState(null)
  const [products, setProducts] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [c, p] = await Promise.all([fetchConfig(), fetchProducts()])
      setConfig(c)
      const title = restaurantDisplayName(c)
      if (title) document.title = title
      // Web menu must mirror API/desktop data exactly (no local fallback injection).
      setProducts(Array.isArray(p) ? p : [])
    } catch (e) {
      const raw = e instanceof Error ? e.message : String(e)
      const dev =
        import.meta.env.DEV &&
        (raw === 'Failed to fetch' ||
          raw.includes('NetworkError') ||
          raw.includes('Load failed'))
      setError(
        dev
          ? 'Could not reach the API from the menu dev server. Run EliteRestaurant.Api on port 8080 (dotnet run), then refresh — Vite sends /api to http://localhost:8080.'
          : e instanceof Error
            ? e.message
            : 'Could not load menu',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  return { config, products, loading, error, refetch: load }
}
