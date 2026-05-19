import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import { sameProductId } from '../utils/productId'
import { estimateTicketPrepMinutes } from '../utils/estimatePrepMinutes'
import { computeTotals } from '../utils/totals'
import { productIsAvailable } from '../utils/availability'

const defaultConfig = { taxPercent: 7, servicePercent: 10 }

/**
 * Mixed food + drink cart for dedicated online order flow (no single-section restriction).
 * @param {Record<string, unknown> | null} config
 */
export function useOnlineOrderCart(config) {
  const [lines, setLines] = useState(/** @type {{ product: Record<string, unknown>; quantity: number }[]} */ ([]))

  const linesRef = useRef(lines)
  useEffect(() => {
    linesRef.current = lines
  }, [lines])

  const taxPct =
    Number(config?.taxPercent ?? config?.TaxPercent) > 0
      ? Number(config?.taxPercent ?? config?.TaxPercent)
      : defaultConfig.taxPercent
  const servicePct =
    Number(config?.servicePercent ?? config?.ServicePercent) > 0
      ? Number(config?.servicePercent ?? config?.ServicePercent)
      : defaultConfig.servicePercent

  const addItem = useCallback((product) => {
    if (!productIsAvailable(product)) return
    setLines((p) => {
      const i = p.findIndex((l) => sameProductId(l.product.id, product.id))
      if (i < 0) return [...p, { product, quantity: 1 }]
      const n = [...p]
      n[i] = { ...n[i], quantity: Math.min(20, n[i].quantity + 1) }
      return n
    })
  }, [])

  const addProductBatch = useCallback((product, qtyToAdd) => {
    if (!productIsAvailable(product)) return
    const q = Math.max(1, Math.min(20, Math.floor(Number(qtyToAdd))))
    setLines((p) => {
      const i = p.findIndex((l) => sameProductId(l.product.id, product.id))
      if (i < 0) return [...p, { product, quantity: q }]
      const n = [...p]
      n[i] = { ...n[i], quantity: Math.min(20, n[i].quantity + q) }
      return n
    })
  }, [])

  const setQuantity = useCallback((productId, qty) => {
    const raw = Math.max(0, Math.floor(Number(qty)))
    const q = raw === 0 ? 0 : Math.min(20, raw)
    setLines((prev) => {
      if (q === 0) return prev.filter((l) => !sameProductId(l.product.id, productId))
      const i = prev.findIndex((l) => sameProductId(l.product.id, productId))
      if (i < 0) return prev
      const n = [...prev]
      n[i] = { ...n[i], quantity: q }
      return n
    })
  }, [])

  const adjustLineQuantity = useCallback((productId, delta) => {
    const d = Math.trunc(Number(delta))
    if (d === 0) return
    flushSync(() => {
      setLines((prev) => {
        const i = prev.findIndex((l) => sameProductId(l.product.id, productId))
        if (i < 0) return prev
        const q = prev[i].quantity + d
        if (q <= 0) return prev.filter((l) => !sameProductId(l.product.id, productId))
        if (q > 20) return prev
        const n = [...prev]
        n[i] = { ...n[i], quantity: q }
        return n
      })
    })
  }, [])

  const incrementLine = useCallback((productId) => {
    adjustLineQuantity(productId, 1)
  }, [adjustLineQuantity])

  const decrementLine = useCallback((productId) => {
    adjustLineQuantity(productId, -1)
  }, [adjustLineQuantity])

  const removeItem = useCallback((productId) => {
    setLines((prev) => prev.filter((l) => !sameProductId(l.product.id, productId)))
  }, [])

  const clearCart = useCallback(() => setLines([]), [])

  const getItemQty = useCallback(
    (productId) => lines.find((l) => sameProductId(l.product.id, productId))?.quantity ?? 0,
    [lines],
  )

  const subtotal = useMemo(
    () => lines.reduce((a, l) => a + Number(l.product.price) * l.quantity, 0),
    [lines],
  )

  const { tax, service, grandTotal } = useMemo(() => {
    const t = computeTotals(subtotal, taxPct, servicePct)
    return { tax: t.tax, service: t.service, grandTotal: t.grand }
  }, [subtotal, taxPct, servicePct])

  const totalItems = useMemo(() => lines.reduce((a, l) => a + l.quantity, 0), [lines])

  const estimatedPrepMinutes = useMemo(() => {
    if (lines.length === 0) return 0
    return estimateTicketPrepMinutes(
      lines.map((l) => ({
        quantity: l.quantity,
        prepMinutes: Number(l.product.prepMinutes ?? l.product.PrepMinutes ?? 0),
        category: String(l.product.category ?? ''),
        subCategory: String(l.product.subcategory ?? 'General'),
      })),
    )
  }, [lines])

  return {
    lines,
    addItem,
    addProductBatch,
    setQuantity,
    adjustLineQuantity,
    incrementLine,
    decrementLine,
    removeItem,
    clearCart,
    getItemQty,
    subtotal,
    tax,
    service,
    grandTotal,
    totalItems,
    estimatedPrepMinutes,
    taxPercent: taxPct,
    servicePercent: servicePct,
  }
}
