import { useCallback, useEffect, useMemo, useState } from 'react'
import { fetchConfig, fetchProducts } from '../utils/api'
import { withLocalFoodPhoto } from '../utils/localFoodImages'

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
      setProducts(Array.isArray(p) ? p.map(withLocalFoodPhoto) : [])
    } catch (e) {
      try {
        const [c, p] = await Promise.all([fetchConfig(), fetchProducts()])
        setConfig(c)
        setProducts(Array.isArray(p) ? p.map(withLocalFoodPhoto) : [])
      } catch (e2) {
        setError(e2 instanceof Error ? e2.message : 'Could not load menu')
      }
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  return { config, products, loading, error, refetch: load }
}
