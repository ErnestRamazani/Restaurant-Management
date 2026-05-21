import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'
import fr from './locales/fr.json'
import { API_ORIGIN } from './utils/apiClient'

export const LANGUAGE_STORAGE_KEY = 'elite_lang'

export function normalizeLanguage(code) {
  return String(code || '').toLowerCase().startsWith('fr') ? 'fr' : 'en'
}

export function getSavedLanguage() {
  try {
    return normalizeLanguage(localStorage.getItem(LANGUAGE_STORAGE_KEY))
  } catch {
    return 'en'
  }
}

function unflatten(flat) {
  const root = {}
  if (!flat || typeof flat !== 'object') return root
  for (const [key, value] of Object.entries(flat)) {
    const parts = key.split('.')
    let node = root
    for (let i = 0; i < parts.length; i += 1) {
      const part = parts[i]
      if (i === parts.length - 1) {
        node[part] = value
      } else {
        node[part] = node[part] ?? {}
        node = node[part]
      }
    }
  }
  return root
}

export async function loadRemoteTranslations(lang) {
  const code = normalizeLanguage(lang)
  const base = (API_ORIGIN || '').replace(/\/$/, '')
  const url = `${base}/api/language/strings?lang=${code}`
  try {
    const res = await fetch(url)
    if (!res.ok) return false
    const body = await res.json()
    const strings = body.strings ?? body.Strings
    if (!strings) return false
    const nested = unflatten(strings)
    i18n.addResourceBundle(code, 'translation', nested, true, true)
    return true
  } catch {
    return false
  }
}

export async function setAppLanguage(lang) {
  const code = normalizeLanguage(lang)
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, code)
  } catch {
    /* ignore */
  }
  await loadRemoteTranslations(code)
  await i18n.changeLanguage(code)
  return code
}

const saved = getSavedLanguage()

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    fr: { translation: fr },
  },
  lng: saved,
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

loadRemoteTranslations(saved).then((ok) => {
  if (ok) i18n.changeLanguage(saved)
})

export default i18n
