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

/** Split plain-text settings into paragraphs for display. */
export function textParagraphs(text) {
  const raw = String(text ?? '').trim()
  if (!raw) return []
  return raw.split(/\n{2,}|\r\n\r\n/).map((p) => p.trim()).filter(Boolean)
}
