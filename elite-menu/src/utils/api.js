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
