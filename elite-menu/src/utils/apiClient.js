export const PRODUCTION_API_ORIGIN = 'https://starfish-app-owtoz.ondigitalocean.app'
export const DEVELOPMENT_API_ORIGIN = 'http://localhost:8080'

const configuredBase = import.meta.env.VITE_API_BASE_URL
const defaultBase = import.meta.env.DEV ? DEVELOPMENT_API_ORIGIN : PRODUCTION_API_ORIGIN

export const API_ORIGIN = (configuredBase && configuredBase.trim())
  ? configuredBase.trim().replace(/\/$/, '')
  : defaultBase

export const API_BASE = `${API_ORIGIN}/api`

export async function pingApi(options = {}) {
  const response = await fetch(`${API_BASE}/health`, {
    method: 'GET',
    cache: 'no-store',
    ...options,
  })

  if (!response.ok) {
    throw new Error(`Cloud API unavailable (${response.status})`)
  }

  return response.json().catch(() => ({ status: 'ok' }))
}

export async function apiFetch(path, options = {}) {
  const url = path.startsWith('http') ? path : `${API_BASE}${path.startsWith('/') ? path : `/${path}`}`
  const headers = new Headers(options.headers || {})
  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const token = window.sessionStorage.getItem('elite_access_token') || ''
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(url, { ...options, headers })
  const data = await response.json().catch(() => null)
  if (!response.ok) {
    const message = data?.message || data?.title || data?.detail || `Request failed (${response.status})`
    throw new Error(message)
  }

  return data
}
