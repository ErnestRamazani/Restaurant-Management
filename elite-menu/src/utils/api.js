import { apiFetch } from './apiClient'

const BASE = '/public/menu'

/** @returns {Promise<Record<string, unknown>>} */
export async function fetchConfig() {
  return apiFetch(`${BASE}/config`)
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchProducts() {
  return apiFetch(`${BASE}/products`)
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchTables() {
  return apiFetch(`${BASE}/tables`)
}

/** @param {string} code */
export async function validateStaffLoginCode(code) {
  const data = await apiFetch(`${BASE}/staff-login-code/${encodeURIComponent(code)}`, {
    method: 'POST',
  })
  const token = data?.accessToken != null ? String(data.accessToken) : ''
  if (token && typeof window !== 'undefined') {
    window.sessionStorage.setItem('elite_access_token', token)
  }
  return data
}

/**
 * @param {object} payload
 * @returns {Promise<{ success: boolean; label?: string; message?: string; errors?: string[] }>}
 */
export async function submitDraft(payload) {
  try {
    return await apiFetch(`${BASE}/draft`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  } catch (error) {
    throw new Error(error instanceof Error ? error.message : 'Failed to send order', { cause: error })
  }
}
