import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion } from 'framer-motion'

/**
 * Hero “Reservation / Order” gateway — book a table vs order online (pickup/delivery).
 */
export function ReservationOrderGatewayModal({ open, onClose, onBookTable, onOrderOnline }) {
  const panelRef = useRef(/** @type {HTMLDivElement | null} */ (null))

  useEffect(() => {
    if (!open) return
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = prev
    }
  }, [open])

  useEffect(() => {
    if (!open) return
    const onKey = (e) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  useEffect(() => {
    if (!open) return
    const t = window.setTimeout(() => panelRef.current?.querySelector('button')?.focus(), 50)
    return () => window.clearTimeout(t)
  }, [open])

  if (typeof document === 'undefined') return null

  return createPortal(
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-[100] flex items-end justify-center p-4 pb-[max(1rem,env(safe-area-inset-bottom))] sm:items-center sm:p-6"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
          role="presentation"
        >
          <motion.button
            type="button"
            aria-label="Close"
            className="absolute inset-0 bg-midnight/80 backdrop-blur-xl"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />
          <motion.div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="gateway-title"
            className="relative z-[1] w-full max-w-md rounded-3xl border border-gold/35 bg-midnight-2/95 p-6 shadow-[0_24px_80px_rgba(0,0,0,0.55)]"
            initial={{ opacity: 0, y: 24, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 16, scale: 0.98 }}
            transition={{ type: 'spring', stiffness: 380, damping: 36 }}
          >
            <h2
              id="gateway-title"
              className="text-center font-display text-xl italic text-champagne"
              style={{ fontFamily: '"Playfair Display", serif' }}
            >
              How can we serve you?
            </h2>
            <p className="mt-2 text-center font-body text-[0.78rem] leading-relaxed text-champagne/55">
              Choose reservation or online ordering — both stay separate from browsing the menu.
            </p>

            <div className="mt-6 flex flex-col gap-3 sm:min-h-0">
              <button
                type="button"
                className="min-h-[56px] w-full rounded-2xl border border-champagne/20 bg-champagne/[0.06] px-4 font-display text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-champagne transition hover:border-gold/45 hover:text-gold active:scale-[0.99]"
                style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
                onClick={() => {
                  onClose()
                  onBookTable()
                }}
              >
                Book a table
              </button>
              <button
                type="button"
                className="min-h-[56px] w-full rounded-2xl border border-amber-400/50 bg-amber-500/10 px-4 font-display text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-amber-50 transition hover:border-amber-300 hover:bg-amber-500/[0.15] active:scale-[0.99]"
                style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
                onClick={() => {
                  onClose()
                  onOrderOnline()
                }}
              >
                Order online
              </button>
            </div>

            <button
              type="button"
              onClick={onClose}
              className="mt-5 w-full min-h-[44px] rounded-2xl border border-champagne/10 font-body text-xs font-semibold uppercase tracking-wider text-champagne/50 transition hover:text-champagne/75"
            >
              Cancel
            </button>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>,
    document.body,
  )
}
