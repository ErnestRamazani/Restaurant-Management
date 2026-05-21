import { AnimatePresence, motion } from 'framer-motion'

/**
 * In-app confirm (replaces window.confirm) — matches midnight / gold UI.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel = 'Continue',
  cancelLabel = 'Cancel',
  /** @param {boolean} danger */
  danger = false,
  onConfirm,
  onCancel,
}) {
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
            aria-label="Dismiss"
            className="absolute inset-0 bg-black/75"
            onClick={onCancel}
          />
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-dialog-title"
            initial={{ y: 28, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: 24, opacity: 0 }}
            transition={{ type: 'spring', stiffness: 380, damping: 36 }}
            className="relative z-10 mx-4 mb-[max(1.5rem,env(safe-area-inset-bottom))] w-full max-w-[min(100%,22rem)] rounded-2xl border border-champagne/15 bg-midnight-2 p-5 shadow-2xl sm:mb-0"
            onClick={(e) => e.stopPropagation()}
          >
            <h2
              id="confirm-dialog-title"
              className="font-display text-xl italic text-champagne"
            >
              {title}
            </h2>
            {children ? (
              <div className="mt-3 font-body text-[0.9rem] leading-relaxed text-champagne/85">{children}</div>
            ) : null}
            <div className={`flex flex-col gap-2 sm:flex-row-reverse sm:justify-end ${children ? 'mt-6' : 'mt-5'}`}>
              <button
                type="button"
                onClick={onConfirm}
                className={`min-h-[48px] flex-1 rounded-xl px-4 font-body text-[0.88rem] font-bold uppercase tracking-[0.08em] sm:flex-initial sm:min-w-[8rem] ${
                  danger
                    ? 'bg-red-500/90 text-white shadow-lg shadow-red-900/30'
                    : 'bg-gold text-black shadow-[0_4px_20px_rgba(200,168,76,0.35)]'
                }`}
              >
                {confirmLabel}
              </button>
              <button
                type="button"
                onClick={onCancel}
                className="min-h-[48px] flex-1 rounded-xl border border-champagne/25 bg-champagne/[0.06] px-4 font-body text-[0.88rem] font-semibold text-champagne sm:flex-initial sm:min-w-[8rem]"
              >
                {cancelLabel}
              </button>
            </div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
