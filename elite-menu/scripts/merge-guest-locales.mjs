/**
 * Merges guest-en.json / guest-fr.json into API wwwroot/locales/*.json
 * so GET /api/language/strings includes guest menu keys.
 */
import { readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const localesDir = path.resolve(__dirname, '../src/locales')
const apiLocalesDir = path.resolve(__dirname, '../../EliteRestaurant.Api/wwwroot/locales')

function deepMerge(target, source) {
  const out = { ...target }
  for (const key of Object.keys(source)) {
    const sv = source[key]
    const tv = target[key]
    if (
      sv &&
      typeof sv === 'object' &&
      !Array.isArray(sv) &&
      tv &&
      typeof tv === 'object' &&
      !Array.isArray(tv)
    ) {
      out[key] = deepMerge(tv, sv)
    } else {
      out[key] = sv
    }
  }
  return out
}

for (const lang of ['en', 'fr']) {
  const basePath = path.join(apiLocalesDir, `${lang}.json`)
  const guestPath = path.join(localesDir, `guest-${lang}.json`)
  const base = JSON.parse(readFileSync(basePath, 'utf8'))
  const guest = JSON.parse(readFileSync(guestPath, 'utf8'))
  const merged = deepMerge(base, guest)
  writeFileSync(basePath, `${JSON.stringify(merged, null, 2)}\n`, 'utf8')
  console.log(`Merged guest-${lang}.json → wwwroot/locales/${lang}.json`)
}
