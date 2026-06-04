import { readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(__dirname, '..')
const apiLocales = path.resolve(root, '../EliteRestaurant.Api/wwwroot/locales')

function deepMerge(target, source) {
  for (const [key, value] of Object.entries(source)) {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      target[key] = deepMerge(target[key] && typeof target[key] === 'object' ? { ...target[key] } : {}, value)
    } else {
      target[key] = value
    }
  }
  return target
}

for (const lang of ['en', 'fr']) {
  const guest = JSON.parse(readFileSync(path.join(root, `src/locales/guest-${lang}.json`), 'utf8'))
  const basePath = path.join(apiLocales, `${lang}.json`)
  const base = JSON.parse(readFileSync(basePath, 'utf8'))
  const merged = deepMerge(base, guest)
  writeFileSync(basePath, `${JSON.stringify(merged, null, 2)}\n`, 'utf8')
  console.log(`Merged guest-${lang}.json → ${basePath}`)
}
