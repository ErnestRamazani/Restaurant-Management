const configuredBase = import.meta.env.VITE_API_BASE_URL

export const API_ORIGIN = (configuredBase && configuredBase.trim())
  ? configuredBase.trim().replace(/\/$/, '')
  : window.location.origin

export const API_BASE = `${API_ORIGIN}/api`

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
