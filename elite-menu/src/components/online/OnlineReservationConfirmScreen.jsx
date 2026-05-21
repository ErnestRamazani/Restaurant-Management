import { motion } from 'framer-motion'
import { Check, Download } from 'lucide-react'
import { downloadGuestReservationPdf } from '../../utils/guestReservationPdf'

/**
 * Single-viewport confirmation for online table reservations (mirrors order confirm).
 * @param {object} props
 * @param {string} [props.confirmationCode]
 * @param {object} [props.ticket]
 * @param {string} [props.restaurantName]
 * @param {() => void} props.onNewReservation
 * @param {() => void} [props.onBackToStart]
 */
export function OnlineReservationConfirmScreen({
  confirmationCode,
  ticket,
  restaurantName,
  onNewReservation,
  onBackToStart,
}) {
  const code = String(confirmationCode ?? ticket?.confirmationCode ?? '').trim()
  const partySize = ticket?.partySize != null ? Number(ticket.partySize) : null

  return (
    <motion.div
      className="flex h-[100dvh] max-h-[100dvh] flex-col overflow-hidden bg-midnight text-champagne"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center px-5 pt-[max(0.5rem,env(safe-area-inset-top))]">
        <div className="mb-2 flex h-11 w-11 items-center justify-center rounded-full border-2 border-amber-400/45 bg-amber-500/10">
          <Check className="h-5 w-5 text-amber-300" strokeWidth={2.5} />
        </div>

        <h1 className="text-center font-display text-lg font-semibold italic leading-tight">
          We received your reservation
        </h1>
        <p className="mt-1 text-center font-body text-[0.7rem] leading-snug text-champagne/55">
          Screenshot your code below
        </p>

        <div className="mt-3 w-full max-w-[280px] rounded-2xl border border-gold/35 bg-[var(--gold-dim)] px-4 py-3 text-center shadow-[0_12px_40px_rgba(0,0,0,0.2)]">
          <p className="font-body text-[0.58rem] font-semibold uppercase tracking-[0.2em] text-gold/85">
            Confirmation code
          </p>
          {code ? (
            <p className="mt-1 font-mono text-[2.1rem] font-bold leading-none tracking-[0.16em] text-champagne">
              {code}
            </p>
          ) : (
            <p className="mt-2 font-body text-xs leading-relaxed text-champagne/55">
              Code unavailable — call the restaurant with the phone number on your booking.
            </p>
          )}
        </div>

        {ticket ? (
          <div className="mt-3 w-full max-w-[280px] rounded-xl border border-champagne/12 bg-midnight-2/90 px-3 py-2.5 font-body text-[0.72rem] leading-snug text-champagne/75">
            <p>
              <span className="font-semibold text-gold/90">{ticket.guestName}</span>
              {partySize != null ? ` · ${partySize} guest${partySize === 1 ? '' : 's'}` : ''}
            </p>
            {ticket.phone ? (
              <p className="mt-0.5 truncate text-champagne/60">Tel: {ticket.phone}</p>
            ) : null}
            {ticket.arrivalLabel ? (
              <p className="mt-0.5 text-champagne/60">Arrival: {ticket.arrivalLabel}</p>
            ) : null}
            {ticket.tableLabel ? (
              <p className="mt-0.5 font-semibold text-champagne">Table: {ticket.tableLabel}</p>
            ) : null}
            {ticket.userNotes ? (
              <p className="mt-0.5 line-clamp-2 text-champagne/55">Notes: {ticket.userNotes}</p>
            ) : null}
          </div>
        ) : null}

        {ticket ? (
          <button
            type="button"
            onClick={() => void downloadGuestReservationPdf(ticket, restaurantName)}
            className="mt-3 flex min-h-[36px] items-center justify-center gap-1.5 rounded-lg border border-gold/30 px-3 font-body text-[0.65rem] font-bold uppercase tracking-[0.1em] text-gold"
          >
            <Download className="h-3.5 w-3.5" aria-hidden />
            Download PDF ticket
          </button>
        ) : null}
      </div>

      <div className="shrink-0 space-y-2 px-5 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-2">
        <button
          type="button"
          onClick={onNewReservation}
          className="flex min-h-[46px] w-full items-center justify-center rounded-xl border border-gold/45 bg-gold/10 font-body text-[0.82rem] font-bold uppercase tracking-[0.08em] text-gold"
        >
          New reservation
        </button>
        {onBackToStart ? (
          <button
            type="button"
            onClick={onBackToStart}
            className="flex min-h-[40px] w-full items-center justify-center font-body text-sm text-champagne/50"
          >
            Back to home
          </button>
        ) : null}
      </div>
    </motion.div>
  )
}
