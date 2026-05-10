export const PRODUCTION_API_ORIGIN = 'https://starfish-app-owtoz.ondigitalocean.app'
/** Use when you want the phone to talk to the API directly (see README / LAN testing). */
export const DEVELOPMENT_API_ORIGIN = 'http://localhost:8080'

const configuredBase = import.meta.env.VITE_API_BASE_URL?.trim()

/**
 * In Vite `dev`, default to same-origin `/api` so the dev proxy works from any device on your LAN
 * (e.g. http://192.168.x.x:5173 → proxy → API on the PC). Override with VITE_API_BASE_URL if needed.
 */
export const API_ORIGIN =
  configuredBase && configuredBase.length > 0
    ? configuredBase.replace(/\/$/, '')
    : import.meta.env.DEV
      ? ''
      : PRODUCTION_API_ORIGIN

export const API_BASE = API_ORIGIN ? `${API_ORIGIN}/api` : '/api'

/**
 * Turn API-relative URLs ("/api/public/…") into an absolute URL for images when the SPA is hosted
 * on another origin via VITE_API_BASE_URL or when opening the menu from a non-API host.
 */
export function resolveApiAssetUrl(assetPath) {
  const raw = typeof assetPath === 'string' ? assetPath.trim() : ''
  if (!raw || /^https?:\/\//i.test(raw)) {
    return raw
  }

  try {
    const base =
      typeof API_ORIGIN === 'string' && API_ORIGIN.length > 0
        ? API_ORIGIN.replace(/\/$/, '')
        : typeof window !== 'undefined'
          ? window.location.origin.replace(/\/$/, '')
          : ''
    if (!base) return raw
    const pathPart = raw.startsWith('/') ? raw : `/${raw}`
    return new URL(pathPart, `${base}/`).href
  } catch {
    return raw
  }
}
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
