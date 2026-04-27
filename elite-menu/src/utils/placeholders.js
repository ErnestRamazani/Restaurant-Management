const PALETTE = [
  '#1a2f2a',
  '#2a1f2f',
  '#2f2a1a',
  '#1a2a2f',
  '#2f1a1a',
  '#1f2f1a',
  '#2a2f1a',
  '#1a1f2f',
]

/**
 * @param {string} [category]
 * @returns {string}
 */
export function getCategoryColor(category) {
  if (!category) return PALETTE[0]
  let hash = 0
  for (let i = 0; i < category.length; i++) {
    hash = category.charCodeAt(i) + ((hash << 5) - hash)
  }
  return PALETTE[Math.abs(hash) % PALETTE.length]
}
