/** Read a string from public menu config (camelCase or PascalCase API). */
export function configString(config, ...keys) {
  if (!config) return ''
  for (const key of keys) {
    const value = config[key]
    if (value != null && String(value).trim()) return String(value).trim()
  }
  return ''
}

export function restaurantDisplayName(config) {
  return configString(config, 'restaurantName', 'RestaurantName')
}

export function menuTagline(config) {
  return configString(config, 'tagline', 'Tagline')
}

export function menuAboutText(config) {
  return configString(config, 'aboutText', 'AboutText')
}

export function menuContactIntro(config) {
  return configString(config, 'contactIntro', 'ContactIntro')
}

export function menuNotesText(config) {
  return configString(config, 'menuNotesText', 'MenuNotesText')
}

/**
 * Tax and service % from GET /api/public/menu/config.
 * No hardcoded fallback — callers should wait until config is loaded (see useMenu).
 * @param {Record<string, unknown> | null | undefined} config
 * @returns {{ taxPercent: number; servicePercent: number } | null}
 */
export function pricingPercentsFromConfig(config) {
  if (!config) return null
  const tax = Number(config.taxPercent ?? config.TaxPercent)
  const service = Number(config.servicePercent ?? config.ServicePercent)
  if (!Number.isFinite(tax) || tax < 0 || !Number.isFinite(service) || service < 0) return null
  return { taxPercent: tax, servicePercent: service }
}

/** Delivery fee % from GET /api/public/menu/config (default 20 when missing). */
export function deliveryFeePercentFromConfig(config) {
  if (!config) return 20
  const raw = Number(config.deliveryFeePercent ?? config.DeliveryFeePercent)
  if (!Number.isFinite(raw) || raw < 0) return 20
  return Math.min(100, raw)
}

/** Split plain-text settings into paragraphs for display. */
export function textParagraphs(text) {
  const raw = String(text ?? '').trim()
  if (!raw) return []
  return raw.split(/\n{2,}|\r\n\r\n/).map((p) => p.trim()).filter(Boolean)
}
