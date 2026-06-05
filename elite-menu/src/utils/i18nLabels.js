/** @typedef {import('i18next').TFunction} TFunction */

const FILTER_KEY_MAP = Object.freeze({
  All: 'filters.all',
  Starters: 'filters.starters',
  Main: 'filters.main',
  Dessert: 'filters.dessert',
  Alcohol: 'filters.alcohol',
  'Non-alcohol': 'filters.nonAlcohol',
  Other: 'filters.other',
})

/**
 * Translate built-in menu filter labels; taxonomy labels from admin pass through unchanged.
 * @param {TFunction} t
 * @param {string} label
 */
export function translateFilterLabel(t, label) {
  const key = FILTER_KEY_MAP[label]
  return key ? t(key) : label
}

/** @type {readonly string[]} */
export const CALL_SERVER_REASON_CODES = Object.freeze([
  'bring_bill',
  'refill_drink',
  'pack_leftover',
  'extra_cutlery',
  'problem_food',
  'other',
])

/**
 * @param {TFunction} t
 * @param {string} code
 */
export function callServerReasonLabel(t, code) {
  return t(`callServer.reasons.${code}`, { defaultValue: code })
}
