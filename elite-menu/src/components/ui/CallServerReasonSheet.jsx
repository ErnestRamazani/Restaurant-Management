import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'

const spring = { type: 'spring', stiffness: 380, damping: 34 }

/** @type {{ code: string; label: string }[]} */
export const CALL_SERVER_REASONS = [
  { code: 'bring_bill', label: 'Bring the bill' },
  { code: 'refill_drink', label: 'Refill drink' },
  { code: 'pack_leftover', label: 'Pack leftover' },
  { code: 'extra_cutlery', label: 'Missing items / Extra cutlery' },
  { code: 'problem_food', label: 'Problem with food' },
  { code: 'other', label: 'Other / Call server' },
]

/**
 * @param {{ open: boolean; busy?: boolean; onSelect: (code: string) => void; onClose: () => void }} props
 */
export function CallServerReasonSheet({ open, busy = false, onSelect, onClose }) {
  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-[200] flex items-end justify-center sm:items-center"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          role="presentation"
        >
          <button
            type="button"
            aria-label="Close"
            className="absolute inset-0 bg-black/75"
            onClick={onClose}
          />
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-labelledby="call-server-reason-title"
            initial={{ y: 40, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: 32, opacity: 0 }}
            transition={spring}
            className="relative z-10 mx-4 mb-[max(1.25rem,env(safe-area-inset-bottom))] w-full max-w-md rounded-2xl border border-champagne/15 bg-midnight-2 p-4 shadow-2xl sm:mb-0"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-start justify-between gap-3">
              <h2
                id="call-server-reason-title"
                className="font-display text-xl italic text-champagne"
              >
                What do you need?
              </h2>
              <button
                type="button"
                onClick={onClose}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-champagne/70 hover:bg-champagne/10"
                aria-label="Close"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="flex flex-col gap-2">
              {CALL_SERVER_REASONS.map((r) => (
                <button
                  key={r.code}
                  type="button"
                  disabled={busy}
                  onClick={() => onSelect(r.code)}
                  className="min-h-[48px] rounded-xl border border-champagne/20 bg-midnight-3 px-4 py-3 text-left font-body text-[0.92rem] font-semibold text-champagne transition hover:border-gold/45 hover:bg-gold/10 disabled:opacity-50"
                >
                  {r.label}
                </button>
              ))}
            </div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
