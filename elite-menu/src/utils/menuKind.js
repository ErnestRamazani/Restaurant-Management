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
