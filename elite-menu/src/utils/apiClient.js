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
      : ''

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
  const { signal: userSignal, ...fetchOpts } = options
  const tc = new AbortController()
  const tid = window.setTimeout(() => tc.abort(), 8_000)
  const signal =
    userSignal && typeof AbortSignal.any === 'function'
      ? AbortSignal.any([userSignal, tc.signal])
      : userSignal ?? tc.signal

  try {
    const response = await fetch(`${API_BASE}/health`, {
      method: 'GET',
      cache: 'no-store',
      ...fetchOpts,
      signal,
    })

    if (!response.ok) {
      throw new Error(`Cloud API unavailable (${response.status})`)
    }

    return response.json().catch(() => ({ status: 'ok' }))
  } finally {
    window.clearTimeout(tid)
  }
}

export async function apiFetch(path, options = {}) {
  const { timeoutMs = 25_000, signal: userSignal, headers: inputHeaders, ...rest } = options
  const url = path.startsWith('http') ? path : `${API_BASE}${path.startsWith('/') ? path : `/${path}`}`
  const headers = new Headers(inputHeaders || {})
  if (rest.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const token = window.sessionStorage.getItem('elite_access_token') || ''
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const tc = new AbortController()
  const tid = window.setTimeout(() => tc.abort(), timeoutMs)
  const signal =
    userSignal && typeof AbortSignal.any === 'function'
      ? AbortSignal.any([userSignal, tc.signal])
      : userSignal ?? tc.signal

  try {
    const response = await fetch(url, { ...rest, headers, signal })
    const data = await response.json().catch(() => null)
    if (!response.ok) {
      const errs = Array.isArray(data?.errors) ? data.errors.filter(Boolean).join(' ') : ''
      const message =
        errs || data?.message || data?.title || data?.detail || `Request failed (${response.status})`
      throw new Error(message)
    }

    return data
  } catch (e) {
    if (e?.name === 'AbortError' && tc.signal.aborted && (!userSignal || !userSignal.aborted)) {
      throw new Error(
        import.meta.env.DEV
          ? 'Could not reach the API (timed out). Start EliteRestaurant.Api on port 8080 — Vite proxies /api from :5173 to http://localhost:8080.'
          : 'Could not reach the server (timed out). Check your connection.',
      )
    }
    if (
      import.meta.env.DEV &&
      e instanceof TypeError &&
      typeof e.message === 'string' &&
      (e.message === 'Failed to fetch' || e.message.includes('NetworkError'))
    ) {
      throw new Error(
        'Could not reach the API. Start EliteRestaurant.Api on port 8080 so http://localhost:5173 can proxy /api.',
      )
    }
    throw e
  } finally {
    window.clearTimeout(tid)
  }
}
