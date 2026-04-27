const BASE = '/api/public/menu'

/** @returns {Promise<Record<string, unknown>>} */
export async function fetchConfig() {
  const r = await fetch(`${BASE}/config`)
  if (!r.ok) throw new Error('Failed to load restaurant config')
  return r.json()
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchProducts() {
  const r = await fetch(`${BASE}/products`)
  if (!r.ok) throw new Error('Failed to load menu')
  return r.json()
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchTables() {
  const r = await fetch(`${BASE}/tables`)
  if (!r.ok) throw new Error('Failed to load tables')
  return r.json()
}

/**
 * @param {object} payload
 * @returns {Promise<{ success: boolean; label?: string; message?: string; errors?: string[] }>}
 */
export async function submitDraft(payload) {
  const r = await fetch(`${BASE}/draft`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  const data = await r.json().catch(() => ({}))
  if (!r.ok) {
    const msg = Array.isArray(data.errors) && data.errors.length
      ? data.errors.join('. ')
      : 'Failed to send order'
    throw new Error(msg)
  }
  return data
}
