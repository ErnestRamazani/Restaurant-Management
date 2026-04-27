/**
 * @param {Record<string, unknown> | null | undefined} product
 */
export function productIsAvailable(product) {
  if (!product) return true
  if (product.isAvailable === false) return false
  if (product.IsAvailable === false) return false
  return true
}
