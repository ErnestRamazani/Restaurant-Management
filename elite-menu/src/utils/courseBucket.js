/**
 * Maps a food product (non-drink) to a fixed course for the main menu tabs.
 * Uses category + subcategory text from the API; adjust heuristics if your DB uses other names.
 * @param {Record<string, unknown>} p
 * @returns {'starters' | 'main' | 'dessert'}
 */
export function getFoodCourseBucket(p) {
  const c = String(p.category || '').toLowerCase().trim()
  const s = String(p.subcategory || '').toLowerCase().trim()
  const t = `${c} ${s}`

  if (/(^|[^a-z])(dessert|sweets?|patiss|ice\s*cream|gelato|mousse|sorbet|cr[èe]me|br[ûu]l[ée]e|cheesecake|pudding|tarte|g[aâ]teau|chocolate\ssauce)/.test(t)) {
    return 'dessert'
  }
  if (/(^|[^a-z])(starter|appetiz|soup|salad|amuse|antipast|ceviche|bruschetta|tapas)/.test(t)) {
    return 'starters'
  }
  if (c === 'starters' || c === 'starter' || c === 'appetizers' || c === 'appetizer' || c === 'soups' || c === 'soup' || c === 'salads' || c === 'salad') {
    return 'starters'
  }
  if (c === 'dessert' || c === 'desserts' || c === 'sweets') {
    return 'dessert'
  }
  if (c === 'main' || c === 'mains' || c === 'main course' || c === 'mains and grills' || c === 'pasta' || c === 'pizza' || c === 'seafood' || c === 'grill') {
    return 'main'
  }
  if (c.includes('dessert') || c.includes('sweet') || s.includes('dessert')) {
    return 'dessert'
  }
  if (c.includes('starter') || c.includes('appetiz') || c.includes('soup') || c.includes('salad')) {
    return 'starters'
  }
  return 'main'
}

/** @param {'All' | 'Starters' | 'Main' | 'Dessert'} course */
export function productMatchesCourse(p, course) {
  if (course === 'All') return true
  const b = getFoodCourseBucket(p)
  if (course === 'Starters') return b === 'starters'
  if (course === 'Main') return b === 'main'
  if (course === 'Dessert') return b === 'dessert'
  return true
}
