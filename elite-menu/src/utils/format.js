export function formatUsd(amount) {
  return `$${Number(amount || 0).toFixed(2)}`
}

export function formatFc(amount, rate) {
  const r = Number(rate) || 0
  return `FC ${Math.round(Number(amount) * r).toLocaleString()}`
}
