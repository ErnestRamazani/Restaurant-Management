/**
 * API / JSON may expose `id` as number or string; compare coherently for cart lines.
 * @param {unknown} a
 * @param {unknown} b
 */
export function sameProductId(a, b) {
  if (a == null || b == null) return false
  return String(a) === String(b)
}
