import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { CALL_SERVER_REASON_CODES, callServerReasonLabel } from '../../utils/i18nLabels'

const spring = { type: 'spring', stiffness: 380, damping: 34 }

/**
 * @param {{ open: boolean; busy?: boolean; onSelect: (code: string) => void; onClose: () => void }} props
 */
export function CallServerReasonSheet({ open, busy = false, onSelect, onClose }) {
  const { t } = useTranslation()

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
            aria-label={t('common.close')}
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
                {t('callServer.title')}
              </h2>
              <button
                type="button"
                onClick={onClose}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-champagne/70 hover:bg-champagne/10"
                aria-label={t('common.close')}
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="flex flex-col gap-2">
              {CALL_SERVER_REASON_CODES.map((code) => (
                <button
                  key={code}
                  type="button"
                  disabled={busy}
                  onClick={() => onSelect(code)}
                  className="min-h-[48px] rounded-xl border border-champagne/20 bg-midnight-3 px-4 py-3 text-left font-body text-[0.92rem] font-semibold text-champagne transition hover:border-gold/45 hover:bg-gold/10 disabled:opacity-50"
                >
                  {callServerReasonLabel(t, code)}
                </button>
              ))}
            </div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
