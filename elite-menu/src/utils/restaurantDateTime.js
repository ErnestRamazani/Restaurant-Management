const DEFAULT_TZ = 'Africa/Kinshasa'

export function getRestaurantTimeZone(config) {
  const raw = config?.restaurantTimeZoneId ?? config?.RestaurantTimeZoneId
  const t = raw != null ? String(raw).trim() : ''
  return t || DEFAULT_TZ
}

function partsInTimeZone(date, timeZone) {
  const fmt = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  })
  const map = {}
  for (const p of fmt.formatToParts(date)) {
    if (p.type !== 'literal') map[p.type] = p.value
  }
  return {
    year: Number(map.year),
    month: Number(map.month),
    day: Number(map.day),
    hour: Number(map.hour),
    minute: Number(map.minute),
  }
}

/** datetime-local wall clock in restaurant TZ → UTC ISO */
export function restaurantLocalInputToUtcIso(localValue, timeZoneId) {
  if (!localValue) return ''
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(String(localValue).trim())
  if (!m) return ''
  const tz = getRestaurantTimeZone({ restaurantTimeZoneId: timeZoneId })
  const target = {
    year: Number(m[1]),
    month: Number(m[2]),
    day: Number(m[3]),
    hour: Number(m[4]),
    minute: Number(m[5]),
  }
  let guess = Date.UTC(target.year, target.month - 1, target.day, target.hour, target.minute, 0)
  for (let i = 0; i < 4; i++) {
    const got = partsInTimeZone(new Date(guess), tz)
    const diffMin =
      (target.hour - got.hour) * 60 +
      (target.minute - got.minute) +
      (target.day - got.day) * 24 * 60
    if (diffMin === 0) break
    guess += diffMin * 60 * 1000
  }
  return new Date(guess).toISOString()
}

export function formatRestaurantDateTime(isoOrDate, config, options) {
  const d = isoOrDate instanceof Date ? isoOrDate : new Date(isoOrDate)
  if (Number.isNaN(d.getTime())) return ''
  const tz = getRestaurantTimeZone(config)
  return new Intl.DateTimeFormat(undefined, { timeZone: tz, ...options }).format(d)
}

export function restaurantDatetimeLocalAtStartOfDay(dayOffsetFromToday, config) {
  const tz = getRestaurantTimeZone(config)
  const p = partsInTimeZone(new Date(), tz)
  const pad = (n) => String(n).padStart(2, '0')
  const d = new Date(Date.UTC(p.year, p.month - 1, p.day + dayOffsetFromToday, 0, 0, 0))
  const got = partsInTimeZone(d, tz)
  return `${got.year}-${pad(got.month)}-${pad(got.day)}T00:00`
}

export function restaurantDatetimeLocalMonthsAhead(monthsAhead, config) {
  const tz = getRestaurantTimeZone(config)
  const p = partsInTimeZone(new Date(), tz)
  const d = new Date(Date.UTC(p.year, p.month - 1 + monthsAhead, p.day, 23, 59, 0))
  const got = partsInTimeZone(d, tz)
  const pad = (n) => String(n).padStart(2, '0')
  return `${got.year}-${pad(got.month)}-${pad(got.day)}T23:59`
}

export function restaurantNowInTimeZone(config) {
  return partsInTimeZone(new Date(), getRestaurantTimeZone(config))
}

/** UTC ISO → datetime-local value in restaurant wall clock */
export function utcIsoToRestaurantDatetimeLocal(iso, config) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const p = partsInTimeZone(d, getRestaurantTimeZone(config))
  const pad = (n) => String(n).padStart(2, '0')
  return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hour)}:${pad(p.minute)}`
}
