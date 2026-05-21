import { Link, useNavigate } from 'react-router-dom'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronLeft } from 'lucide-react'
import { GoldDivider } from '../ui/GoldDivider'
import { BottomSheet } from '../ui/BottomSheet'
import { OnlineReservationConfirmScreen } from '../online/OnlineReservationConfirmScreen'
import { publicAvailability, publicBookFloor, publicSuggestPlacements } from '../../utils/reservationApi'

function toUtcIsoFromLocalInput(value) {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  return d.toISOString()
}

function addMinutesIso(iso, minutes) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  d.setMinutes(d.getMinutes() + minutes)
  return d.toISOString()
}

/** Start of local calendar day (today + dayOffset) as datetime-local string. */
function localDateTimeAtStartOfDay(dayOffsetFromToday) {
  const d = new Date()
  d.setDate(d.getDate() + dayOffsetFromToday)
  d.setHours(0, 0, 0, 0)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function localDateTimeAtEndOfDayMonthsAhead(monthsAhead) {
  const d = new Date()
  d.setMonth(d.getMonth() + monthsAhead)
  d.setHours(23, 59, 0, 0)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** @param {string} localValue datetime-local value */
function validateReservationLeadTime(localValue, leadDays, maxMonthsAhead) {
  if (!localValue) return 'Choose a date and time for your visit.'
  const picked = new Date(localValue)
  if (Number.isNaN(picked.getTime())) return 'Invalid date.'
  const now = new Date()
  if (picked.getTime() <= now.getTime()) return 'Reservation must be in the future — past dates are not allowed.'
  const min = new Date()
  min.setDate(min.getDate() + leadDays)
  min.setHours(0, 0, 0, 0)
  if (picked.getTime() < min.getTime()) {
    return leadDays <= 0
      ? ''
      : `Online bookings need at least ${leadDays} full day(s)’ notice. For sooner dates, please call the restaurant.`
  }
  const max = new Date()
  max.setMonth(max.getMonth() + maxMonthsAhead)
  max.setHours(23, 59, 59, 999)
  if (picked.getTime() > max.getTime()) {
    return `Online bookings are available up to ${maxMonthsAhead} month(s) ahead.`
  }
  return ''
}

export function ReservationScreen({ config }) {
  const navigate = useNavigate()
  const name =
    config?.restaurantName && String(config.restaurantName).trim()
      ? String(config.restaurantName).trim()
      : config?.RestaurantName && String(config.RestaurantName).trim()
        ? String(config.RestaurantName).trim()
        : ''
  const phone = config ? String(config.phone ?? config.Phone ?? '').trim() : ''
  const address = config ? String(config.address ?? config.Address ?? '').trim() : ''
  const leadDaysRaw = Number(config?.reservationLeadDays ?? 2)
  const monthsAheadRaw = Number(config?.reservationMaxMonthsAhead ?? 6)
  const leadDays = Number.isFinite(leadDaysRaw) ? Math.max(0, leadDaysRaw) : 2
  const maxMonthsAhead = Number.isFinite(monthsAheadRaw) ? Math.max(1, monthsAheadRaw) : 6

  const [partySize, setPartySize] = useState(2)
  const [guestName, setGuestName] = useState('')
  const [guestPhone, setGuestPhone] = useState('')
  const [guestEmail, setGuestEmail] = useState('')
  const [notes, setNotes] = useState('')
  const [localStart, setLocalStart] = useState('')
  const [suggestions, setSuggestions] = useState(/** @type {any[]} */ ([]))
  const [slots, setSlots] = useState(/** @type {any[]} */ ([]))
  const [placementId, setPlacementId] = useState(/** @type {number | null} */ (null))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [bookingDone, setBookingDone] = useState(
    /** @type {null | { confirmationCode: string; ticket: Record<string, unknown> }} */
    (null),
  )
  const [tablePickerOpen, setTablePickerOpen] = useState(false)

  const minLocalDateTime = useMemo(() => localDateTimeAtStartOfDay(leadDays), [leadDays])
  const maxLocalDateTime = useMemo(() => localDateTimeAtEndOfDayMonthsAhead(maxMonthsAhead), [maxMonthsAhead])

  const defaultLocalStart = useCallback(() => {
    const d = new Date()
    d.setDate(d.getDate() + leadDays)
    d.setMinutes(0, 0, 0)
    d.setHours(12, 0, 0, 0)
    const pad = (n) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
  }, [leadDays])

  useEffect(() => {
    const def = defaultLocalStart()
    setLocalStart(def < minLocalDateTime ? minLocalDateTime : def)
  }, [defaultLocalStart, minLocalDateTime])

  const selectedTableLabel = useMemo(() => {
    if (placementId == null) return ''
    const s = suggestions.find((x) => {
      const id = x.placementUnitId ?? x.PlacementUnitId
      return id != null && Number(id) === Number(placementId)
    })
    if (!s) return ''
    const lbl = s.tableDisplayName ?? s.TableDisplayName
    return lbl != null ? String(lbl) : ''
  }, [suggestions, placementId])

  const refreshSuggestions = useCallback(async () => {
    const startIso = toUtcIsoFromLocalInput(localStart)
    if (!startIso) return
    const endIso = addMinutesIso(startIso, 105)
    setError('')
    setBusy(true)
    try {
      const rows = await publicSuggestPlacements({
        partySize: Number(partySize) || 2,
        plannedStartUtc: startIso,
        plannedEndUtc: endIso,
      })
      const list = Array.isArray(rows) ? rows : []
      setSuggestions(list)
      if (list.length > 0) {
        const first = list[0]
        const id = first.placementUnitId ?? first.PlacementUnitId
        setPlacementId(id != null ? Number(id) : null)
      } else {
        setPlacementId(null)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load table suggestions.')
      setSuggestions([])
    } finally {
      setBusy(false)
    }
  }, [localStart, partySize])

  useEffect(() => {
    if (!localStart) return
    const t = window.setTimeout(() => {
      refreshSuggestions()
    }, 400)
    return () => window.clearTimeout(t)
  }, [localStart, partySize, refreshSuggestions])

  const loadSlots = async () => {
    if (placementId == null) return
    const startIso = toUtcIsoFromLocalInput(localStart)
    if (!startIso) return
    const rangeStart = new Date(startIso)
    const rangeEnd = new Date(rangeStart)
    rangeEnd.setDate(rangeEnd.getDate() + 3)
    setBusy(true)
    setError('')
    try {
      const rows = await publicAvailability({
        placementUnitId: placementId,
        partySize: Number(partySize) || 2,
        rangeStartUtc: rangeStart.toISOString(),
        rangeEndUtc: rangeEnd.toISOString(),
        maxSlots: 12,
      })
      setSlots(Array.isArray(rows) ? rows : [])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load open times.')
    } finally {
      setBusy(false)
    }
  }

  const submit = async () => {
    const leadErr = validateReservationLeadTime(localStart, leadDays, maxMonthsAhead)
    if (leadErr) {
      setError(leadErr)
      return
    }
    const startIso = toUtcIsoFromLocalInput(localStart)
    if (!startIso || !guestName.trim() || !guestPhone.trim()) {
      setError('Name, phone, and arrival time are required.')
      return
    }
    if (placementId == null) {
      setError('No table matched that party size and time. Try another time.')
      return
    }
    setBusy(true)
    setError('')
    const endIso = addMinutesIso(startIso, 105)
    try {
      const res = await publicBookFloor({
        placementUnitId: placementId,
        plannedStartUtc: startIso,
        plannedEndUtc: endIso,
        guestName: guestName.trim(),
        guestPhone: guestPhone.trim(),
        guestEmail: guestEmail.trim(),
        partySize: Number(partySize) || 2,
        userNotes: notes.trim(),
      })
      const confirmationCode = String(res?.confirmationCode ?? res?.ConfirmationCode ?? '').trim()
      if (!confirmationCode) {
        setError(
          'Reservation was saved but no confirmation code was returned. Restart the API after migrations, then try again or call the restaurant.',
        )
        return
      }
      const tableFromApi = String(res?.tableDisplayName ?? res?.TableDisplayName ?? '').trim()
      const sel = suggestions.find((s) => {
        const id = s.placementUnitId ?? s.PlacementUnitId
        return id != null && Number(id) === Number(placementId)
      })
      const tableLabel =
        tableFromApi ||
        String(sel?.tableDisplayName ?? sel?.TableDisplayName ?? 'Your table')
      const startUtc = res?.plannedStartUtc ?? res?.PlannedStartUtc ?? startIso
      const endUtc = res?.plannedEndUtc ?? res?.PlannedEndUtc ?? endIso
      const arrivalLabel = startUtc
        ? new Date(startUtc).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
        : localStart
          ? new Date(localStart).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
          : ''
      const endLabel = endUtc
        ? new Date(endUtc).toLocaleTimeString(undefined, { timeStyle: 'short' })
        : ''
      const guest = String(res?.guestName ?? res?.GuestName ?? guestName).trim()
      const phoneOnFile = String(res?.guestPhone ?? res?.GuestPhone ?? guestPhone).trim()
      const size = Number(res?.partySize ?? res?.PartySize ?? partySize) || 2
      const userNotes = String(res?.userNotes ?? res?.UserNotes ?? notes).trim()
      const bookedAt = new Date()
      const ticket = {
        confirmationCode,
        guestName: guest,
        phone: phoneOnFile,
        partySize: size,
        tableLabel,
        arrivalLabel,
        endLabel,
        userNotes: userNotes || undefined,
        bookedAtLabel: bookedAt.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }),
      }
      setBookingDone({ confirmationCode, ticket })
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Booking failed.'
      setError(msg)
      if (msg.toLowerCase().includes('conflict')) await loadSlots()
    } finally {
      setBusy(false)
    }
  }

  if (bookingDone) {
    return (
      <OnlineReservationConfirmScreen
        confirmationCode={bookingDone.confirmationCode}
        ticket={bookingDone.ticket}
        restaurantName={name || 'Restaurant'}
        onNewReservation={() => {
          setBookingDone(null)
          setError('')
          setNotes('')
        }}
        onBackToStart={() => navigate('/')}
      />
    )
  }

  return (
    <main className="relative min-h-[100svh] overflow-hidden bg-midnight px-6 py-8 text-champagne">
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[60vw] w-[60vw] rounded-full bg-[rgba(200,168,76,0.04)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[40vw] w-[40vw] rounded-full bg-[rgba(237,232,220,0.02)] blur-3xl" />

      <section className="relative z-10 mx-auto flex w-full max-w-lg flex-col pb-16 pt-2">
        <Link
          to="/"
          className="mb-8 inline-flex min-h-[44px] items-center self-start font-body text-xs font-bold uppercase tracking-[0.18em] text-gold/80 transition hover:text-gold"
        >
          Back
        </Link>

        <p className="font-body text-[0.68rem] font-bold uppercase tracking-[0.28em] text-gold/80">Reservations</p>
        <h1
          className="mt-3 font-display text-4xl italic leading-tight text-champagne"
          style={{ fontFamily: '"Playfair Display", serif' }}
        >
          Reserve your table
        </h1>
        <GoldDivider className="my-5" />

        <p className="font-body text-[0.95rem] leading-relaxed text-champagne/80">
          Book online for {name}, or reach us by phone.
        </p>

        <div className="mt-5 rounded-2xl border border-amber-500/30 bg-amber-500/[0.07] p-4">
            <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.2em] text-amber-200/90">
              Order food instead
            </p>
            <p className="mt-2 font-body text-[0.82rem] leading-relaxed text-champagne/75">
              Pickup or delivery — browse the menu and send an order without reserving a table.
            </p>
            <button
              type="button"
              onClick={() => navigate('/order-online')}
              className="mt-3 min-h-[48px] w-full rounded-xl border border-amber-400/50 bg-amber-500/15 font-display text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-amber-50 transition hover:border-amber-300 hover:bg-amber-500/25"
            >
              Order online
            </button>
          </div>

          <div className="mt-6 space-y-4 rounded-2xl border border-champagne/10 bg-black/15 p-4">
            <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.2em] text-gold/70">Online booking</p>
            <div className="rounded-xl border border-gold/25 bg-gold/[0.06] p-3">
              <p className="font-body text-[0.72rem] leading-relaxed text-champagne/85">
                We accept online reservations <strong className="text-gold">at least {leadDays} day(s) in advance</strong> and up to{' '}
                <strong className="text-gold">{maxMonthsAhead} month(s)</strong> ahead. For
                table requests sooner than that — or special occasions —{' '}
                {phone ? (
                  <>
                    please <a href={`tel:${phone.replace(/\s/g, '')}`} className="text-gold underline decoration-gold/40">call {phone}</a>.
                  </>
                ) : (
                  'please call the restaurant.'
                )}
              </p>
            </div>
            <label className="block font-body text-xs text-champagne/60" htmlFor="party">
              Party size
            </label>
            <input
              id="party"
              type="number"
              min={1}
              max={20}
              value={partySize}
              onChange={(e) => setPartySize(Number(e.target.value))}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="start">
              Arrival (local time)
            </label>
            <input
              id="start"
              type="datetime-local"
              min={minLocalDateTime}
              max={maxLocalDateTime}
              value={localStart}
              onChange={(e) => {
                const v = e.target.value
                setLocalStart(v)
                const err = validateReservationLeadTime(v, leadDays, maxMonthsAhead)
                setError(err)
              }}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold [color-scheme:dark]"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gname">
              Guest name
            </label>
            <input
              id="gname"
              value={guestName}
              onChange={(e) => setGuestName(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gphone">
              Phone
            </label>
            <input
              id="gphone"
              value={guestPhone}
              onChange={(e) => setGuestPhone(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gemail">
              Email (optional)
            </label>
            <input
              id="gemail"
              value={guestEmail}
              onChange={(e) => setGuestEmail(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="notes">
              Notes
            </label>
            <textarea
              id="notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              className="w-full rounded-xl border border-gold/20 bg-black/25 px-3 py-2 font-body text-sm text-champagne outline-none focus:border-gold"
            />

            {suggestions.length > 0 ? (
              <div>
                <p className="font-body text-xs font-bold uppercase tracking-[0.12em] text-gold/75">Table</p>
                <p className="mt-1 font-body text-[0.7rem] leading-relaxed text-champagne/50">
                  Choose a table that fits your party and arrival — we’ll hold it for your visit.
                </p>
                <button
                  type="button"
                  onClick={() => setTablePickerOpen(true)}
                  aria-label="Choose table"
                  className="mt-3 flex min-h-[52px] w-full items-center justify-between gap-3 rounded-xl border border-champagne/15 bg-black/20 px-4 py-3 text-left transition hover:border-gold/35 hover:bg-black/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-gold/50"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-display text-sm font-semibold text-champagne">
                      {selectedTableLabel || (placementId != null ? 'Table selected' : 'Choose a table')}
                    </p>
                  </div>
                  <ChevronLeft
                    className="h-6 w-6 shrink-0 text-gold/90"
                    aria-hidden
                    strokeWidth={2.2}
                  />
                </button>
              </div>
            ) : busy ? (
              <p className="font-body text-xs text-champagne/45">Finding tables for this time…</p>
            ) : (
              <p className="font-body text-xs text-amber-200/90">No open tables for that time — adjust the time or party size.</p>
            )}

            {slots.length > 0 ? (
              <div className="rounded-xl border border-gold/15 bg-gold/5 p-3">
                <p className="font-body text-xs font-bold uppercase tracking-[0.14em] text-gold/90">Suggested slots</p>
                <ul className="mt-2 max-h-32 space-y-1 overflow-auto font-body text-xs text-champagne/80">
                  {slots.map((s, i) => {
                    const st = s.startUtc ?? s.StartUtc
                    const en = s.endUtc ?? s.EndUtc
                    return (
                      <li key={i}>
                        <button
                          type="button"
                          onClick={() => {
                            if (!st) return
                            const d = new Date(st)
                            const pad = (n) => String(n).padStart(2, '0')
                            setLocalStart(
                              `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`,
                            )
                          }}
                          className="text-left text-gold/90 underline decoration-gold/30 hover:text-gold"
                        >
                          {st ? new Date(st).toLocaleString() : ''} – {en ? new Date(en).toLocaleTimeString() : ''}
                        </button>
                      </li>
                    )
                  })}
                </ul>
              </div>
            ) : null}

            {error ? (
              <p className="rounded-xl border border-red-500/25 bg-red-500/10 px-3 py-2 font-body text-xs text-red-200">{error}</p>
            ) : null}

            <button
              type="button"
              disabled={busy}
              onClick={submit}
              className="flex min-h-[52px] w-full items-center justify-center rounded-xl bg-gold font-body text-sm font-extrabold uppercase tracking-[0.12em] text-black transition hover:brightness-105 disabled:opacity-50"
            >
              {busy ? 'Please wait…' : 'Request reservation'}
            </button>
          </div>

          {phone ? (
            <a
              href={`tel:${phone.replace(/\s/g, '')}`}
              className="mt-6 flex min-h-[52px] items-center justify-center rounded-sm border-2 border-gold/45 bg-gold/5 px-6 py-3 font-body text-sm font-bold uppercase tracking-[0.16em] text-gold transition hover:border-gold hover:bg-[var(--gold-dim)]"
            >
              Call {phone}
            </a>
          ) : (
            <p className="mt-6 rounded-2xl border border-gold/15 bg-gold/5 px-4 py-3 font-body text-sm text-champagne/65">
              Reservation contact is set in the restaurant back office.
            </p>
          )}

          {address ? (
            <p className="mt-5 font-body text-sm leading-relaxed text-champagne/60">{address}</p>
          ) : null}
      </section>

      <BottomSheet open={tablePickerOpen} onClose={() => setTablePickerOpen(false)}>
        <div className="px-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-1">
          <p className="text-center font-body text-[0.66rem] font-bold uppercase tracking-[0.24em] text-gold/80">
            Suggested tables
          </p>
          <h2 className="mt-2 text-center font-display text-xl italic text-champagne">Pick your table</h2>
          <p className="mx-auto mt-2 max-w-sm text-center font-body text-xs leading-relaxed text-champagne/55">
            These placements match your party size and time. Tap one to select — you can change it anytime before
            submitting.
          </p>
          <div className="mt-5 max-h-[min(52vh,420px)] space-y-2 overflow-y-auto overscroll-contain pr-1">
            {suggestions.map((s) => {
              const id = s.placementUnitId ?? s.PlacementUnitId
              const nid = id != null ? Number(id) : NaN
              const label = s.tableDisplayName ?? s.TableDisplayName ?? `Placement ${id}`
              const selected = placementId != null && nid === placementId
              return (
                <button
                  key={String(id)}
                  type="button"
                  onClick={() => {
                    setPlacementId(Number.isFinite(nid) ? nid : null)
                    setTablePickerOpen(false)
                  }}
                  className={`w-full rounded-xl border px-4 py-3.5 text-left transition ${
                    selected
                      ? 'border-gold bg-gold/[0.14] shadow-[0_0_0_1px_rgba(200,168,76,0.35)]'
                      : 'border-champagne/15 bg-black/25 hover:border-gold/35 hover:bg-black/35'
                  }`}
                >
                  <span className="font-display text-sm font-semibold text-champagne">{label}</span>
                  {selected ? (
                    <span className="mt-1 block font-body text-[0.65rem] uppercase tracking-[0.16em] text-gold">
                      Selected
                    </span>
                  ) : null}
                </button>
              )
            })}
          </div>
          <button
            type="button"
            onClick={() => setTablePickerOpen(false)}
            className="mt-4 min-h-[48px] w-full rounded-xl border border-champagne/25 bg-champagne/[0.06] font-body text-[0.72rem] font-bold uppercase tracking-[0.14em] text-champagne/85 transition hover:border-gold/40 hover:bg-gold/10"
          >
            Close
          </button>
        </div>
      </BottomSheet>
    </main>
  )
}
