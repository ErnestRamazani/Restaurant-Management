import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useRef } from 'react'

const focusableSelector =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * Hero entry: choose reservation vs online ordering.
 */
export function ReservationOrderGatewayModal({ open, onClose, onBookTable, onOrderOnline }) {
  const panelRef = useRef(/** @type {HTMLDivElement | null} */ (null))
  const prevActiveRef = useRef(/** @type {Element | null} */ (null))

  useEffect(() => {
    if (!open) return
    prevActiveRef.current = document.activeElement

    const panel = panelRef.current
    const trap = (/** @type {KeyboardEvent} */ e) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
        return
      }
      if (e.key !== 'Tab' || !panel) return

      const nodes = Array.from(panel.querySelectorAll(focusableSelector)).filter(
        (el) =>
          el instanceof HTMLElement &&
          !el.hasAttribute('disabled') &&
          el.tabIndex !== -1 &&
          el.offsetParent !== null,
      )
      if (!nodes.length) return

      const first = nodes[0]
      const last = nodes[nodes.length - 1]
      const active = document.activeElement

      if (e.shiftKey) {
        if (active === first || !panel.contains(active)) {
          e.preventDefault()
          last.focus()
        }
      } else if (active === last || !panel.contains(active)) {
        e.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', trap)
    window.setTimeout(() => {
      const first = panel?.querySelector(focusableSelector)
      if (first instanceof HTMLElement) first.focus()
    }, 0)

    document.body.style.overflow = 'hidden'

    return () => {
      document.removeEventListener('keydown', trap)
      document.body.style.overflow = ''
      const prev = prevActiveRef.current
      if (prev instanceof HTMLElement) prev.focus()
    }
  }, [open, onClose])

  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-[120] flex items-end justify-center sm:items-center"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          role="presentation"
        >
          <button
            type="button"
            aria-label="Close dialog"
            className="absolute inset-0 bg-midnight/90 backdrop-blur-xl"
            onClick={onClose}
          />

          <motion.div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="reservation-order-gateway-title"
            initial={{ y: 28, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: 20, opacity: 0 }}
            transition={{ type: 'spring', stiffness: 380, damping: 36 }}
            className="relative z-10 mx-4 mb-[max(1.5rem,env(safe-area-inset-bottom))] w-full max-w-[min(100%,28rem)] rounded-2xl border border-gold/50 bg-midnight/92 p-6 shadow-[0_22px_70px_rgba(0,0,0,0.5)] backdrop-blur-xl sm:mb-0"
            onClick={(e) => e.stopPropagation()}
          >
            <p
              id="reservation-order-gateway-title"
              className="text-center font-display text-[0.72rem] font-semibold uppercase tracking-[0.24em] text-champagne/55"
            >
              Continue as guest
            </p>
            <p className="mt-2 text-center font-body text-[0.82rem] leading-snug text-champagne/72">
              Choose how you&apos;d like to dine with us.
            </p>

            <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:gap-4">
              <button
                type="button"
                onClick={() => {
                  onBookTable()
                  onClose()
                }}
                className="relative min-h-[64px] w-full shrink-0 border border-gold/55 bg-gradient-to-b from-gold/[0.12] to-gold/[0.04] px-5 py-4 font-display text-[0.9rem] font-semibold uppercase tracking-[0.14em] text-gold shadow-[0_10px_36px_rgba(200,168,76,0.18)] transition-colors hover:border-gold hover:bg-[var(--gold-dim)] hover:brightness-[1.04] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-gold/60 focus-visible:ring-offset-2 focus-visible:ring-offset-midnight active:scale-[0.985] sm:min-h-[72px] sm:flex-1"
                style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
              >
                <span className="pointer-events-none absolute left-[-3px] top-[-3px] h-3 w-3 border-l border-t border-gold/70" />
                <span className="pointer-events-none absolute right-[-3px] top-[-3px] h-3 w-3 border-r border-t border-gold/70" />
                <span className="pointer-events-none absolute bottom-[-3px] left-[-3px] h-3 w-3 border-b border-l border-gold/70" />
                <span className="pointer-events-none absolute bottom-[-3px] right-[-3px] h-3 w-3 border-b border-r border-gold/70" />
                Book a table
              </button>

              <button
                type="button"
                onClick={() => {
                  onOrderOnline()
                  onClose()
                }}
                className="relative min-h-[64px] w-full shrink-0 border border-gold/40 bg-midnight-2/95 px-5 py-4 font-display text-[0.9rem] font-semibold uppercase tracking-[0.14em] text-champagne shadow-[inset_0_1px_0_rgba(200,168,76,0.12)] transition hover:border-gold/55 hover:text-gold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-champagne/40 focus-visible:ring-offset-2 focus-visible:ring-offset-midnight active:scale-[0.985] sm:min-h-[72px] sm:flex-1"
                style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
              >
                Order online
              </button>
            </div>

            <button
              type="button"
              onClick={onClose}
              className="mt-5 w-full min-h-[48px] rounded-xl border border-champagne/18 font-body text-[0.78rem] font-semibold uppercase tracking-[0.14em] text-champagne/50 transition hover:border-champagne/30 hover:text-champagne/78 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-champagne/35 focus-visible:ring-offset-2 focus-visible:ring-offset-midnight"
            >
              Cancel
            </button>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
