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

/** Merchandise tax+service/grand plus a delivery fee line (matches server OrderTotalsHelper pattern). */
export function computeTotalsWithDelivery(subtotal, taxPercent, servicePercent, deliveryFeeUsd) {
  const core = computeTotals(subtotal, taxPercent, servicePercent)
  const fee = round2(Math.max(0, Number(deliveryFeeUsd)))
  return {
    subtotal: core.subtotal,
    tax: core.tax,
    service: core.service,
    deliveryFee: fee,
    grand: round2(core.grand + fee),
  }
}
