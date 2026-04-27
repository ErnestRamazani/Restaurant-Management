import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import { getMenuKind } from '../utils/menuKind'
import { sameProductId } from '../utils/productId'
import { estimateTicketPrepMinutes } from '../utils/estimatePrepMinutes'
import { computeTotals } from '../utils/totals'
import { productIsAvailable } from '../utils/availability'

const defaultConfig = { taxPercent: 7, servicePercent: 10 }

/**
 * @param {Record<string, unknown> | null} config
 */
export function useCart(config) {
  const [lines, setLines] = useState(/** @type {{ product: Record<string, unknown>; quantity: number }[]} */ ([]))
  const [sectionConflict, setSectionConflict] = useState(
    /** @type {null | { product: Record<string, unknown>; mode: 'add' | 'batch'; batchQty?: number; message: string }} */ (null),
  )

  const linesRef = useRef(lines)
  useEffect(() => {
    linesRef.current = lines
  }, [lines])

  const taxPct = config?.taxPercent ?? defaultConfig.taxPercent
  const servicePct = config?.servicePercent ?? defaultConfig.servicePercent

  const addItem = useCallback((product) => {
    if (!productIsAvailable(product)) return
    const prev = linesRef.current
    if (prev.length > 0) {
      const incoming = getMenuKind(product)
      const existing = getMenuKind(prev[0].product)
      if (incoming !== existing) {
        const cur = existing === 'drink' ? 'drinks' : 'food'
        const nxt = incoming === 'drink' ? 'drinks' : 'food'
        setSectionConflict({
          product,
          mode: 'add',
          message: `Your cart has ${cur}. To add ${nxt}, your current order will be cleared.`,
        })
        return
      }
    }
    setLines((p) => {
      if (p.length > 0) {
        const inc = getMenuKind(product)
        const ex = getMenuKind(p[0].product)
        if (inc !== ex) return p
      }
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
    const prev = linesRef.current
    if (prev.length > 0) {
      const incoming = getMenuKind(product)
      const existing = getMenuKind(prev[0].product)
      if (incoming !== existing) {
        const cur = existing === 'drink' ? 'drinks' : 'food'
        const nxt = incoming === 'drink' ? 'drinks' : 'food'
        setSectionConflict({
          product,
          mode: 'batch',
          batchQty: q,
          message: `Your cart has ${cur}. To add ${nxt}, your current order will be cleared.`,
        })
        return
      }
    }
    setLines((p) => {
      if (p.length > 0) {
        if (getMenuKind(product) !== getMenuKind(p[0].product)) return p
      }
      const i = p.findIndex((l) => sameProductId(l.product.id, product.id))
      if (i < 0) return [...p, { product, quantity: q }]
      const n = [...p]
      n[i] = { ...n[i], quantity: Math.min(20, n[i].quantity + q) }
      return n
    })
  }, [])

  const confirmSectionSwitch = useCallback(() => {
    if (!sectionConflict) return
    const { product, mode, batchQty } = sectionConflict
    const qty = mode === 'add' ? 1 : Math.max(1, Math.min(20, Math.floor(Number(batchQty ?? 1))))
    flushSync(() => setLines([{ product, quantity: qty }]))
    setSectionConflict(null)
  }, [sectionConflict])

  const cancelSectionSwitch = useCallback(() => {
    setSectionConflict(null)
  }, [])

  const setQuantity = useCallback((productId, qty) => {
    const q = Math.max(0, Math.floor(Number(qty)))
    setLines((prev) => {
      if (q === 0) return prev.filter((l) => !sameProductId(l.product.id, productId))
      const i = prev.findIndex((l) => sameProductId(l.product.id, productId))
      if (i < 0) return prev
      const n = [...prev]
      n[i] = { ...n[i], quantity: q }
      return n
    })
  }, [])

  /**
   * Delta from the latest `lines` (avoids stale quantity when minus/plus is wired from a closure).
   */
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

  const { tax, service, grandTotal } = useMemo(
    () => {
      const t = computeTotals(subtotal, taxPct, servicePct)
      return { tax: t.tax, service: t.service, grandTotal: t.grand }
    },
    [subtotal, taxPct, servicePct],
  )

  const totalItems = useMemo(() => lines.reduce((a, l) => a + l.quantity, 0), [lines])

  const estimatedPrepMinutes = useMemo(() => {
    if (lines.length === 0) return 0
    return estimateTicketPrepMinutes(
      lines.map((l) => ({
        quantity: l.quantity,
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
    sectionConflict,
    confirmSectionSwitch,
    cancelSectionSwitch,
  }
}
