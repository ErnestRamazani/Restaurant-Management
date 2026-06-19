function round2(n) {
  return Math.round(Number(n) * 100) / 100
}

/**
 * @param {number} subtotal
 * @param {number} taxPercent
 * @param {number} servicePercent
 */
export function computeTotals(subtotal, taxPercent, servicePercent) {
  const s = round2(subtotal)
  const tax = round2(s * (Number(taxPercent) / 100))
  const service = round2(s * (Number(servicePercent) / 100))
  const grand = round2(s + tax + service)
  return { subtotal: s, tax, service, grand }
}

/**
 * Delivery fee is included in the taxable base before tax/service (matches server OrderTotalsHelper).
 * @param {number} merchandiseSubtotal
 * @param {number} taxPercent
 * @param {number} servicePercent
 * @param {number} deliveryFeeUsd
 */
export function computeTotalsWithDelivery(merchandiseSubtotal, taxPercent, servicePercent, deliveryFeeUsd) {
  const merch = round2(Math.max(0, Number(merchandiseSubtotal)))
  const fee = round2(Math.max(0, Number(deliveryFeeUsd)))
  const subtotalWithFee = round2(merch + fee)
  const core = computeTotals(subtotalWithFee, taxPercent, servicePercent)
  return {
    subtotal: merch,
    tax: core.tax,
    service: core.service,
    deliveryFee: fee,
    grand: core.grand,
  }
}

/** merchandiseSubtotal × percent / 100, rounded (matches DeliveryFeeHelper). */
export function resolveDeliveryFeeUsd(merchandiseSubtotal, deliveryFeePercent) {
  const subtotal = round2(Math.max(0, Number(merchandiseSubtotal)))
  if (subtotal <= 0) return 0
  const pct = Math.min(100, Math.max(0, Number(deliveryFeePercent) || 20))
  return round2(subtotal * pct / 100)
}
