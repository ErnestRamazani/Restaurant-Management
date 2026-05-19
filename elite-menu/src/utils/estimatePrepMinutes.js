/**
 * Mirrors `OrderPrepTimeEstimator` in EliteRestaurant.Core (same as POS / WPF).
 * @param {number} [prepMinutes] Menu admin value; when &gt; 0, overrides category estimate.
 * @param {string} category
 * @param {string} subCategory
 * @returns {number}
 */
export function minutesForLineItem(prepMinutes, category, subCategory) {
  if (subCategory === undefined && typeof prepMinutes === 'string') {
    return minutesForLineItem(0, prepMinutes, category)
  }

  const stored = Number(prepMinutes)
  if (Number.isFinite(stored) && stored > 0) {
    return Math.min(480, Math.max(1, Math.round(stored)))
  }

  const c = (category || '').trim()
  const s = (subCategory || '').trim()
  let minutes = 10
  switch (c) {
    case 'Drink':
      minutes = 3
      break
    case 'Starter/Appetizer':
      minutes = 8
      break
    case 'Main':
      minutes = 16
      break
    case 'Dessert':
      minutes = 6
      break
    default:
      minutes = 10
      break
  }
  if (/^Cocktail$/i.test(s)) minutes += 2
  if (/^Seafood$/i.test(s)) minutes += 3
  if (/^Meat Meal$/i.test(s)) minutes += 4
  if (/^Pasta$/i.test(s)) minutes += 2
  return minutes
}

/**
 * Parallel prep: max line time + small bump for multiple lines (same as C# `EstimateTicketPrepMinutes`).
 * @param {{ quantity: number; prepMinutes?: number; category: string; subCategory: string }[]} lines
 * @returns {number}
 */
export function estimateTicketPrepMinutes(lines) {
  if (!lines || lines.length === 0) return 0
  const prep = []
  for (const t of lines) {
    const n = Math.max(0, Math.min(20, Math.floor(t.quantity)) || 0)
    const m = minutesForLineItem(t.prepMinutes, t.category, t.subCategory)
    for (let i = 0; i < n; i += 1) prep.push(m)
  }
  if (prep.length === 0) return 0
  return Math.max(...prep) + Math.min(10, Math.max(0, prep.length - 1))
}
