import { apiFetch } from './apiClient'
import { API_ORIGIN } from './apiClient'

/** @param {string} hubPath e.g. `/hubs/reservation-floor` */
export function getSignalRHubUrl(hubPath) {
  const path = hubPath.startsWith('/') ? hubPath : `/${hubPath}`
  if (import.meta.env.DEV && typeof window !== 'undefined' && window.location.port === '5173') {
    return path
  }
  const origin =
    typeof API_ORIGIN === 'string' && API_ORIGIN.length > 0
      ? API_ORIGIN.replace(/\/$/, '')
      : typeof window !== 'undefined'
        ? window.location.origin.replace(/\/$/, '')
        : ''
  return `${origin}${path}`
}

/** @returns {Promise<any>} */
export function fetchFloorSnapshot() {
  return apiFetch('/floor/snapshot')
}

/** @param {number} id */
export function floorCheckIn(id) {
  return apiFetch(`/floor/engagements/${id}/check-in`, { method: 'POST' })
}

/** @param {number} id */
export function floorRelease(id) {
  return apiFetch(`/floor/engagements/${id}/release`, { method: 'POST' })
}

/** @param {number} id */
export function floorMarkClean(id) {
  return apiFetch(`/floor/placements/${id}/mark-clean`, { method: 'POST' })
}

/**
 * @param {number[]} placementUnitIds
 * @param {string | null} clusterKey
 */
export function floorMergePlacements(placementUnitIds, clusterKey = null) {
  return apiFetch('/floor/placements/merge', {
    method: 'POST',
    body: JSON.stringify({ placementUnitIds, clusterKey }),
  })
}

/**
 * @param {object} body
 * @param {number} body.partySize
 * @param {string} body.plannedStartUtc
 * @param {string} body.plannedEndUtc
 */
export function publicSuggestPlacements(body) {
  return apiFetch('/public/floor/suggest', { method: 'POST', body: JSON.stringify(body) })
}

/**
 * @param {object} body
 * @param {number} body.placementUnitId
 * @param {number} body.partySize
 * @param {string} body.rangeStartUtc
 * @param {string} body.rangeEndUtc
 * @param {number} body.maxSlots
 */
export function publicAvailability(body) {
  return apiFetch('/public/floor/availability', { method: 'POST', body: JSON.stringify(body) })
}

/**
 * @param {object} body
 */
export function publicBookFloor(body) {
  return apiFetch('/public/floor/book', { method: 'POST', body: JSON.stringify(body) })
}
