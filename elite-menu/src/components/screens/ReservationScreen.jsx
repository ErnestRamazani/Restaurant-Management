import { Link, useNavigate } from 'react-router-dom'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { GoldDivider } from '../ui/GoldDivider'
import { BottomSheet } from '../ui/BottomSheet'
import { OnlineReservationConfirmScreen } from '../online/OnlineReservationConfirmScreen'
import { publicAvailability, publicBookFloor, publicSuggestPlacements } from '../../utils/reservationApi'
import {
  formatRestaurantDateTime,
  getRestaurantTimeZone,
  restaurantDatetimeLocalAtStartOfDay,
  restaurantDatetimeLocalMonthsAhead,
  restaurantLocalInputToUtcIso,
  utcIsoToRestaurantDatetimeLocal,
} from '../../utils/restaurantDateTime'

function toUtcIsoFromLocalInput(value, config) {
  return restaurantLocalInputToUtcIso(value, getRestaurantTimeZone(config))
}

function addMinutesIso(iso, minutes) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  d.setMinutes(d.getMinutes() + minutes)
  return d.toISOString()
}

/** @param {string} localValue datetime-local value (restaurant wall clock) */
function validateReservationLeadTime(t, localValue, leadDays, maxMonthsAhead, config) {
  if (!localValue) return t('guest.reservation.chooseDateTime')
  const pickedIso = toUtcIsoFromLocalInput(localValue, config)
  if (!pickedIso) return t('guest.reservation.invalidDate')
  const pickedMs = new Date(pickedIso).getTime()
  const nowMs = Date.now()
  if (pickedMs <= nowMs) return t('guest.reservation.mustBeFuture')
  const minIso = toUtcIsoFromLocalInput(restaurantDatetimeLocalAtStartOfDay(leadDays, config), config)
  if (minIso && pickedMs < new Date(minIso).getTime()) {
    return leadDays <= 0 ? '' : t('guest.reservation.leadDays', { days: leadDays })
  }
  const maxIso = toUtcIsoFromLocalInput(restaurantDatetimeLocalMonthsAhead(maxMonthsAhead, config), config)
  if (maxIso && pickedMs > new Date(maxIso).getTime()) {
    return t('guest.reservation.maxMonths', { months: maxMonthsAhead })
  }
  return ''
}

export function ReservationScreen({ config }) {
  const { t } = useTranslation()
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

  const tzLabel = getRestaurantTimeZone(config)
  const minLocalDateTime = useMemo(
    () => restaurantDatetimeLocalAtStartOfDay(leadDays, config),
    [leadDays, config],
  )
  const maxLocalDateTime = useMemo(
    () => restaurantDatetimeLocalMonthsAhead(maxMonthsAhead, config),
    [maxMonthsAhead, config],
  )

  const defaultLocalStart = useCallback(() => {
    const base = restaurantDatetimeLocalAtStartOfDay(leadDays, config)
    return base.replace(/T\d{2}:\d{2}$/, 'T12:00')
  }, [leadDays, config])

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
    const startIso = toUtcIsoFromLocalInput(localStart, config)
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
      setError(e instanceof Error ? e.message : t('guest.reservation.loadSuggestionsFailed'))
      setSuggestions([])
    } finally {
      setBusy(false)
    }
  }, [localStart, partySize])

  useEffect(() => {
    if (!localStart) return
    const timerId = window.setTimeout(() => {
      refreshSuggestions()
    }, 400)
    return () => window.clearTimeout(timerId)
  }, [localStart, partySize, refreshSuggestions])

  const loadSlots = async () => {
    if (placementId == null) return
    const startIso = toUtcIsoFromLocalInput(localStart, config)
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
      setError(e instanceof Error ? e.message : t('guest.reservation.loadSlotsFailed'))
    } finally {
      setBusy(false)
    }
  }

  const submit = async () => {
    const leadErr = validateReservationLeadTime(t, localStart, leadDays, maxMonthsAhead, config)
    if (leadErr) {
      setError(leadErr)
      return
    }
    const startIso = toUtcIsoFromLocalInput(localStart, config)
    if (!startIso || !guestName.trim() || !guestPhone.trim()) {
      setError(t('guest.reservation.requiredFields'))
      return
    }
    if (placementId == null) {
      setError(t('guest.reservation.noTableMatch'))
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
          t('guest.reservation.noConfirmationCode'),
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
        String(sel?.tableDisplayName ?? sel?.TableDisplayName ?? t('guest.reservation.yourTable'))
      const startUtc = res?.plannedStartUtc ?? res?.PlannedStartUtc ?? startIso
      const endUtc = res?.plannedEndUtc ?? res?.PlannedEndUtc ?? endIso
      const arrivalLabel = startUtc
        ? formatRestaurantDateTime(startUtc, config, { dateStyle: 'medium', timeStyle: 'short' })
        : localStart
          ? formatRestaurantDateTime(toUtcIsoFromLocalInput(localStart, config), config, {
              dateStyle: 'medium',
              timeStyle: 'short',
            })
          : ''
      const endLabel = endUtc
        ? formatRestaurantDateTime(endUtc, config, { timeStyle: 'short' })
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
        bookedAtLabel: formatRestaurantDateTime(bookedAt, config, {
          dateStyle: 'medium',
          timeStyle: 'short',
        }),
      }
      setBookingDone({ confirmationCode, ticket })
    } catch (e) {
      const msg = e instanceof Error ? e.message : t('guest.reservation.bookingFailed')
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
        restaurantName={name || t('guest.general.restaurant')}
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
          {t('guest.general.back')}
        </Link>

        <p className="font-body text-[0.68rem] font-bold uppercase tracking-[0.28em] text-gold/80">
          {t('guest.reservation.title')}
        </p>
        <h1
          className="mt-3 font-display text-4xl italic leading-tight text-champagne"
          style={{ fontFamily: '"Playfair Display", serif' }}
        >
          {t('guest.reservation.headline')}
        </h1>
        <GoldDivider className="my-5" />

        <p className="font-body text-[0.95rem] leading-relaxed text-champagne/80">
          {t('guest.reservation.intro', { name: name || t('guest.general.restaurant') })}
        </p>

        <div className="mt-5 rounded-2xl border border-amber-500/30 bg-amber-500/[0.07] p-4">
            <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.2em] text-amber-200/90">
              {t('guest.reservation.orderFoodTitle')}
            </p>
            <p className="mt-2 font-body text-[0.82rem] leading-relaxed text-champagne/75">
              {t('guest.reservation.orderFoodBody')}
            </p>
            <button
              type="button"
              onClick={() => navigate('/order-online')}
              className="mt-3 min-h-[48px] w-full rounded-xl border border-amber-400/50 bg-amber-500/15 font-display text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-amber-50 transition hover:border-amber-300 hover:bg-amber-500/25"
            >
              {t('menu.orderOnline')}
            </button>
          </div>

          <div className="mt-6 space-y-4 rounded-2xl border border-champagne/10 bg-black/15 p-4">
            <p className="font-body text-[0.65rem] font-bold uppercase tracking-[0.2em] text-gold/70">
              {t('guest.reservation.onlineBooking')}
            </p>
            <div className="rounded-xl border border-gold/25 bg-gold/[0.06] p-3">
              <p className="font-body text-[0.72rem] leading-relaxed text-champagne/85">
                {t('guest.reservation.policyIntro', { days: leadDays, months: maxMonthsAhead })}{' '}
                {phone ? (
                  <>
                    {t('guest.reservation.policyCallBefore')}{' '}
                    <a
                      href={`tel:${phone.replace(/\s/g, '')}`}
                      className="font-semibold text-gold underline decoration-gold/40"
                    >
                      {phone}
                    </a>
                    .
                  </>
                ) : (
                  t('guest.reservation.policyCallRestaurant')
                )}
              </p>
            </div>
            <label className="block font-body text-xs text-champagne/60" htmlFor="party">
              {t('guest.reservation.partySize')}
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
              {t('guest.reservation.arrival')} ({tzLabel.replace(/_/g, ' ')})
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
                const err = validateReservationLeadTime(t, v, leadDays, maxMonthsAhead, config)
                setError(err)
              }}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold [color-scheme:dark]"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gname">
              {t('guest.reservation.guestName')}
            </label>
            <input
              id="gname"
              value={guestName}
              onChange={(e) => setGuestName(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gphone">
              {t('guest.reservation.phone')}
            </label>
            <input
              id="gphone"
              value={guestPhone}
              onChange={(e) => setGuestPhone(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="gemail">
              {t('guest.reservation.email')}
            </label>
            <input
              id="gemail"
              value={guestEmail}
              onChange={(e) => setGuestEmail(e.target.value)}
              className="h-11 w-full rounded-xl border border-gold/20 bg-black/25 px-3 font-body text-champagne outline-none focus:border-gold"
            />
            <label className="block font-body text-xs text-champagne/60" htmlFor="notes">
              {t('guest.reservation.notes')}
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
                <p className="font-body text-xs font-bold uppercase tracking-[0.12em] text-gold/75">
                  {t('guest.reservation.tableSection')}
                </p>
                <p className="mt-1 font-body text-[0.7rem] leading-relaxed text-champagne/50">
                  {t('guest.reservation.tableSectionHint')}
                </p>
                <button
                  type="button"
                  onClick={() => setTablePickerOpen(true)}
                  aria-label={t('guest.reservation.chooseTableAria')}
                  className="mt-3 flex min-h-[52px] w-full items-center justify-between gap-3 rounded-xl border border-champagne/15 bg-black/20 px-4 py-3 text-left transition hover:border-gold/35 hover:bg-black/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-gold/50"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-display text-sm font-semibold text-champagne">
                      {selectedTableLabel ||
                        (placementId != null ? t('guest.reservation.tableSelected') : t('guest.reservation.chooseTable'))}
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
              <p className="font-body text-xs text-champagne/45">{t('guest.reservation.findingTables')}</p>
            ) : (
              <p className="font-body text-xs text-amber-200/90">{t('guest.reservation.noTablesForTime')}</p>
            )}

            {slots.length > 0 ? (
              <div className="rounded-xl border border-gold/15 bg-gold/5 p-3">
                <p className="font-body text-xs font-bold uppercase tracking-[0.14em] text-gold/90">
                  {t('guest.reservation.suggestedSlots')}
                </p>
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
                            setLocalStart(utcIsoToRestaurantDatetimeLocal(st, config))
                          }}
                          className="text-left text-gold/90 underline decoration-gold/30 hover:text-gold"
                        >
                          {st ? formatRestaurantDateTime(st, config, { dateStyle: 'short', timeStyle: 'short' }) : ''}
                          {' '}
                          –{' '}
                          {en ? formatRestaurantDateTime(en, config, { timeStyle: 'short' }) : ''}
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
              {busy ? t('guest.reservation.pleaseWait') : t('guest.reservation.requestReservation')}
            </button>
          </div>

          {phone ? (
            <a
              href={`tel:${phone.replace(/\s/g, '')}`}
              className="mt-6 flex min-h-[52px] items-center justify-center rounded-sm border-2 border-gold/45 bg-gold/5 px-6 py-3 font-body text-sm font-bold uppercase tracking-[0.16em] text-gold transition hover:border-gold hover:bg-[var(--gold-dim)]"
            >
              {t('guest.reservation.callButton', { phone })}
            </a>
          ) : (
            <p className="mt-6 rounded-2xl border border-gold/15 bg-gold/5 px-4 py-3 font-body text-sm text-champagne/65">
              {t('guest.reservation.noPhoneConfigured')}
            </p>
          )}

          {address ? (
            <p className="mt-5 font-body text-sm leading-relaxed text-champagne/60">{address}</p>
          ) : null}
      </section>

      <BottomSheet open={tablePickerOpen} onClose={() => setTablePickerOpen(false)}>
        <div className="px-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-1">
          <p className="text-center font-body text-[0.66rem] font-bold uppercase tracking-[0.24em] text-gold/80">
            {t('guest.reservation.suggestedTables')}
          </p>
          <h2 className="mt-2 text-center font-display text-xl italic text-champagne">
            {t('guest.reservation.pickYourTable')}
          </h2>
          <p className="mx-auto mt-2 max-w-sm text-center font-body text-xs leading-relaxed text-champagne/55">
            {t('guest.reservation.pickerHint')}
          </p>
          <div className="mt-5 max-h-[min(52vh,420px)] space-y-2 overflow-y-auto overscroll-contain pr-1">
            {suggestions.map((s) => {
              const id = s.placementUnitId ?? s.PlacementUnitId
              const nid = id != null ? Number(id) : NaN
              const label =
                s.tableDisplayName ??
                s.TableDisplayName ??
                t('guest.reservation.placementFallback', { id })
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
                      {t('guest.reservation.selected')}
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
            {t('common.close')}
          </button>
        </div>
      </BottomSheet>
    </main>
  )
}
