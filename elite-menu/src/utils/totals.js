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
