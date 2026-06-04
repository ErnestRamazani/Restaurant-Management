/**
 * Product `category` from API: "Drink" and variants = beverages.
 * All other top-level categories are treated as food.
 * @param {Record<string, unknown>} product
 * @returns {'food' | 'drink'}
 */
export function getMenuKind(product) {
  const c = String(product?.category || '').toLowerCase()
  if (c === 'drink' || c === 'drinks' || c === 'beverage' || c === 'beverages' || c === 'bar') {
    return 'drink'
  }
  return 'food'
}

/**
 * @param {Record<string, unknown>} product
 * @returns {boolean}
 */
export function isDrinkProduct(product) {
  return getMenuKind(product) === 'drink'
}

/**
 * @param {{ product: Record<string, unknown> }[]} lines
 * @returns {'food' | 'drink' | 'mixed'}
 */
export function inferOrderKindFromLines(lines) {
  let sawFood = false
  let sawDrink = false
  for (const line of lines) {
    if (getMenuKind(line.product) === 'drink') sawDrink = true
    else sawFood = true
    if (sawFood && sawDrink) return 'mixed'
  }
  if (sawDrink) return 'drink'
  return 'food'
}
