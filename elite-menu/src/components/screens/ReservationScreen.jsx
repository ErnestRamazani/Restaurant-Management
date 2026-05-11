import { Link, useNavigate } from 'react-router-dom'
import { useCallback, useEffect, useState } from 'react'
import { GoldDivider } from '../ui/GoldDivider'
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

export function ReservationScreen({ config }) {
  const navigate = useNavigate()
  const name = config?.restaurantName ? String(config.restaurantName) : 'Elite Restaurant'
  const phone = config ? String(config.phone ?? config.Phone ?? '').trim() : ''
  const address = config ? String(config.address ?? config.Address ?? '').trim() : ''

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
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const defaultLocalStart = useCallback(() => {
    const d = new Date()
    d.setMinutes(0, 0, 0)
    d.setHours(d.getHours() + 2)
    const pad = (n) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
  }, [])

  useEffect(() => {
    setLocalStart(defaultLocalStart())
  }, [defaultLocalStart])

  const refreshSuggestions = useCallback(async () => {
    const startIso = toUtcIsoFromLocalInput(localStart)
    if (!startIso) return
    const endIso = addMinutesIso(startIso, 105)
    setError('')
    setMessage('')
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
    setMessage('')
    const endIso = addMinutesIso(startIso, 105)
    try {
      await publicBookFloor({
        placementUnitId: placementId,
        plannedStartUtc: startIso,
        plannedEndUtc: endIso,
        guestName: guestName.trim(),
        guestPhone: guestPhone.trim(),
        guestEmail: guestEmail.trim(),
        partySize: Number(partySize) || 2,
        userNotes: notes.trim(),
      })
      setMessage('You are booked. We look forward to seeing you.')
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Booking failed.'
      setError(msg)
      if (msg.toLowerCase().includes('conflict')) await loadSlots()
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="relative min-h-[100svh] overflow-hidden bg-midnight px-6 py-8 text-champagne">
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[60vw] w-[60vw] rounded-full bg-[rgba(200,168,76,0.04)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[40vw] w-[40vw] rounded-full bg-[rgba(237,232,220,0.02)] blur-3xl" />

      <section className="relative z-10 mx-auto flex min-h-[calc(100svh-4rem)] max-w-lg flex-col justify-center">
        <Link
          to="/"
          className="mb-8 inline-flex min-h-[44px] items-center self-start font-body text-xs font-bold uppercase tracking-[0.18em] text-gold/80 transition hover:text-gold"
        >
          Back
        </Link>

        <div className="rounded-[2rem] border border-champagne/10 bg-midnight-2/80 p-6 shadow-[0_22px_70px_rgba(0,0,0,0.35)]">
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
              onClick={() => navigate('/', { state: { startOnlineOrder: true } })}
              className="mt-3 min-h-[48px] w-full rounded-xl border border-amber-400/50 bg-amber-500/15 font-display text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-amber-50 transition hover:border-amber-300 hover:bg-amber-500/25"
            >
              Order online
            </button>
          </div>

          <div className="mt-6 space-y-4 rounded-2xl border border-champagne/10 bg-black/15 p-4">
            <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.2em] text-gold/70">Online booking</p>
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
              value={localStart}
              onChange={(e) => setLocalStart(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
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
                <p className="font-body text-xs text-champagne/55">Suggested table</p>
                <select
                  value={placementId ?? ''}
                  onChange={(e) => setPlacementId(Number(e.target.value))}
                  className="mt-1 h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-sm text-champagne outline-none focus:border-gold"
                >
                  {suggestions.map((s) => {
                    const id = s.placementUnitId ?? s.PlacementUnitId
                    const label = s.tableDisplayName ?? s.TableDisplayName ?? `Table ${id}`
                    return (
                      <option key={id} value={id}>
                        {label}
                      </option>
                    )
                  })}
                </select>
              </div>
            ) : (
              !busy && <p className="font-body text-xs text-amber-200/90">No open tables for that time — adjust the time or party size.</p>
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
            {message ? (
              <p className="rounded-xl border border-emerald-500/25 bg-emerald-500/10 px-3 py-2 font-body text-xs text-emerald-100">
                {message}
              </p>
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
        </div>
      </section>
    </main>
  )
}
