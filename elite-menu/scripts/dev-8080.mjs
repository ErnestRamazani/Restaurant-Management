/**
 * Default dev entry: build guest menu into API wwwroot, then open http://localhost:8080/
 * (API must already be running — use EliteRestaurant.Api/run-dev.ps1)
 */
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const menuRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const apiHealth = 'http://localhost:8080/api/health'
const menuUrl = 'http://localhost:8080/'

async function waitForApi(maxAttempts = 30) {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const res = await fetch(apiHealth, { cache: 'no-store' })
      if (res.ok) return true
    } catch {
      /* retry */
    }
    await new Promise((r) => setTimeout(r, 500))
  }
  return false
}

function openBrowser(url) {
  const platform = process.platform
  try {
    if (platform === 'win32') {
      execSync(`start "" "${url}"`, { stdio: 'ignore', shell: true })
    } else if (platform === 'darwin') {
      execSync(`open "${url}"`, { stdio: 'ignore' })
    } else {
      execSync(`xdg-open "${url}"`, { stdio: 'ignore' })
    }
  } catch {
    console.log(`Open in browser: ${url}`)
  }
}

console.log('Building guest menu → EliteRestaurant.Api/wwwroot …')
execSync('npm run build', { stdio: 'inherit', cwd: menuRoot })

console.log(`Waiting for API at ${apiHealth} …`)
const ready = await waitForApi()
if (!ready) {
  console.error('')
  console.error('API is not running on http://localhost:8080')
  console.error('Start it first (repo root):')
  console.error('  .\\EliteRestaurant.Api\\run-dev.ps1')
  console.error('Or: .\\scripts\\run-local-8080.ps1')
  process.exit(1)
}

console.log(`Guest menu ready at ${menuUrl}`)
openBrowser(menuUrl)
