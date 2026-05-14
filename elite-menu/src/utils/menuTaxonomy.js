import { getMenuKind } from './menuKind'

function eq(a, b) {
  return String(a ?? '')
    .trim()
    .toLowerCase() ===
    String(b ?? '')
      .trim()
      .toLowerCase()
}

function matchesItemList(sub, items) {
  if (!items.length) return true
  const s = String(sub ?? '').trim()
  if (!s) return items.some((i) => !String(i ?? '').trim())
  return items.some((i) => eq(i, s))
}

/**
 * @param {string} cat
 * @param {string} sub
 * @param {{ name?: string; items?: string[] }} section
 * @param {boolean} isDrinkType
 */
export function sectionMatchesProduct(cat, sub, section, isDrinkType) {
  const c = String(cat ?? '').trim()
  const s = String(sub ?? '').trim()
  const sec = String(section?.name ?? '').trim()
  if (!sec) return false
  const items = Array.isArray(section.items) ? section.items : []

  if (!isDrinkType) {
    if (!eq(c, sec)) return false
    return matchesItemList(s, items)
  }

  if (eq(c, sec)) return matchesItemList(s, items)
  if (eq(c, 'Drink') && !eq(sec, 'Drink')) return items.length > 0 && matchesItemList(s, items)
  return false
}

/**
 * @param {Record<string, unknown> | null} config
 * @returns {any | null}
 */
export function parseMenuTaxonomy(config) {
  if (!config) return null
  const raw = /** @type {unknown} */ (config.menuTaxonomyJson ?? config.menuTaxonomy)
  if (typeof raw === 'string' && raw.trim()) {
    try {
      const o = JSON.parse(raw)
      if (o && typeof o === 'object' && Array.isArray(o.types)) return /** @type {any} */ (o)
    } catch {
      return null
    }
    return null
  }
  if (raw && typeof raw === 'object' && Array.isArray(/** @type {any} */ (raw).types)) return /** @type {any} */ (raw)
  return null
}

/**
 * @param {Record<string, unknown>} product
 * @param {ReturnType<typeof parseMenuTaxonomy>} taxonomy
 * @returns {'food' | 'drink'}
 */
export function getMenuKindWithTaxonomy(product, taxonomy) {
  if (!taxonomy?.types?.length) return getMenuKind(product)
  const cat = String(product?.category ?? '').trim()
  const sub = String(product?.subcategory ?? '').trim()
  for (const t of taxonomy.types) {
    const sections = Array.isArray(t.sections) ? t.sections : []
    for (const sec of sections) {
      if (sectionMatchesProduct(cat, sub, sec, !!t.isDrink)) return t.isDrink ? 'drink' : 'food'
    }
  }
  return getMenuKind(product)
}

/**
 * @param {Record<string, unknown>} p
 * @param {string} courseTab
 * @param {{ sections?: { name?: string; items?: string[] }[] } | null | undefined} foodType
 */
export function productMatchesFoodTaxonomy(p, courseTab, foodType) {
  if (courseTab === 'All') return true
  if (!foodType?.sections?.length) return true
  const cat = String(p?.category ?? '').trim()
  const sub = String(p?.subcategory ?? '').trim()
  const section = foodType.sections.find((s) => eq(s?.name, courseTab))
  if (!section) return true
  return sectionMatchesProduct(cat, sub, section, false)
}

/**
 * @param {Record<string, unknown>} p
 * @param {string} tab
 * @param {{ sections?: { name?: string; items?: string[] }[] } | null | undefined} drinkType
 */
export function productMatchesDrinkTaxonomy(p, tab, drinkType) {
  if (tab === 'All') return true
  if (!drinkType?.sections?.length) return true
  const cat = String(p?.category ?? '').trim()
  const sub = String(p?.subcategory ?? '').trim()
  const section = drinkType.sections.find((s) => eq(s?.name, tab))
  if (!section) return true
  return sectionMatchesProduct(cat, sub, section, true)
}
