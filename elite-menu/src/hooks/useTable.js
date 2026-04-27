import { useMemo, useSyncExternalStore } from 'react'

function getLocationSearch() {
  return typeof window !== 'undefined' ? window.location.search : ''
}

function subscribeToSearchParam(onChange) {
  const handler = () => onChange()
  window.addEventListener('popstate', handler)
  return () => window.removeEventListener('popstate', handler)
}

/**
 * Parse `?table=` as the database table id (positive integer). Non-numeric or zero is ignored.
 * Syncs with `location.search` so the value is not stuck on the first (sometimes empty) mount.
 */
function parseTableParam(search) {
  const params = new URLSearchParams(search)
  const raw = params.get('table')
  if (raw == null || raw === '') {
    return { tableId: null, tableIdRaw: raw, hadInvalidTableParam: false }
  }
  const s = String(raw).trim()
  if (!/^\d+$/.test(s)) {
    return { tableId: null, tableIdRaw: raw, hadInvalidTableParam: true }
  }
  const n = parseInt(s, 10)
  const valid = n > 0 && !Number.isNaN(n)
  return {
    tableId: valid ? n : null,
    tableIdRaw: raw,
    hadInvalidTableParam: !valid,
  }
}

export function useTable() {
  const search = useSyncExternalStore(
    subscribeToSearchParam,
    getLocationSearch,
    () => ''
  )

  return useMemo(() => parseTableParam(search), [search])
}
