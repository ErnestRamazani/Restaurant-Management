import { apiFetch } from './apiClient'

const BASE = '/public/menu'

/** @returns {Promise<Record<string, unknown>>} */
export async function fetchConfig() {
  return apiFetch(`${BASE}/config`)
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchProducts() {
  const bust = Date.now()
  return apiFetch(`${BASE}/products?_=${bust}`, { cache: 'no-store' })
}

/** @returns {Promise<Record<string, unknown>[]>} */
export async function fetchTables() {
  return apiFetch(`${BASE}/tables`)
}

/**
 * @param {string} code Staff passcode
 * @param {{ signInId?: string; pin?: string }} [extra] Optional employee sign-in ID + PIN (after passcode) for role-aware JWT
 */
export async function validateStaffLoginCode(code, extra = {}) {
  const signInId = typeof extra.signInId === 'string' ? extra.signInId.trim() : ''
  const pin = typeof extra.pin === 'string' ? extra.pin.trim() : ''
  const data = await apiFetch(`${BASE}/staff-login-code`, {
    method: 'POST',
    body: JSON.stringify({
      code,
      signInId: signInId.length ? signInId : null,
      pin: pin.length ? pin : null,
    }),
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
/**
 * @param {number} tableId
 * @param {string} reasonCode bring_bill | refill_drink | pack_leftover | extra_cutlery | problem_food | other
 */
export async function callServer(tableId, reasonCode) {
  return apiFetch(`${BASE}/call-server`, {
    method: 'POST',
    body: JSON.stringify({ tableId, reasonCode }),
  })
}

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

export async function submitOnlineOrder(payload) {
  try {
    return await apiFetch(`${BASE}/orders/online`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  } catch (error) {
    throw new Error(error instanceof Error ? error.message : 'Failed to place order', { cause: error })
  }
}

/** Public: kitchen / payment stage + fulfillment text when you have the ticket code from checkout. */
export async function fetchOrderStatusByCode(orderCode) {
  const c = encodeURIComponent(String(orderCode ?? '').trim())
  if (c.length < 3) throw new Error('Order code is required.')
  return apiFetch(`${BASE}/orders/${c}/status`)
}
