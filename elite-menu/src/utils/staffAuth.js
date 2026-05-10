/** @param {string} role */
export function isReservationFloorRole(role) {
  const r = (role || '').trim().toLowerCase()
  return r === 'admin' || r === 'cashier'
}

/** @param {string | null | undefined} token */
export function parseJwtPayload(token) {
  if (!token || typeof token !== 'string') return null
  try {
    const parts = token.split('.')
    if (parts.length < 2) return null
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const json = typeof atob === 'function' ? atob(base64) : Buffer.from(base64, 'base64').toString('utf8')
    return JSON.parse(json)
  } catch {
    return null
  }
}

/**
 * JWT role claim from System.IdentityModel (multiple possible keys).
 * @param {Record<string, unknown> | null} payload
 */
export function roleFromJwtPayload(payload) {
  if (!payload || typeof payload !== 'object') return ''
  for (const [k, v] of Object.entries(payload)) {
    if (k === 'role' || k.endsWith('/role')) {
      if (typeof v === 'string' && v.length) return v
      if (Array.isArray(v) && v.length && typeof v[0] === 'string') return v[0]
    }
  }
  return ''
}

export function canAccessReservationFloorFromStoredToken() {
  if (typeof window === 'undefined') return false
  const token = window.sessionStorage.getItem('elite_access_token') || ''
  if (!token) return false
  return isReservationFloorRole(roleFromJwtPayload(parseJwtPayload(token)))
}
