import { motion } from 'framer-motion'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatUsd } from '../../utils/format'

export function CartButton({ totalItems, grandTotal, onClick }) {
  const { t } = useTranslation()
  const empty = totalItems <= 0
  const prevCount = useRef(0)
  const [bump, setBump] = useState(0)

  useEffect(() => {
    if (totalItems > prevCount.current && totalItems > 0) setBump((b) => b + 1)
    prevCount.current = totalItems
  }, [totalItems])

  if (empty) {
    return null
  }

  return (
    <div
      className="pointer-events-none fixed bottom-0 left-0 right-0 z-50 flex justify-center px-4"
      style={{ paddingBottom: 'max(1.5rem, env(safe-area-inset-bottom, 0px))' }}
    >
      <div className="mx-auto flex w-full max-w-full justify-center px-0">
        <motion.button
          key={bump}
          type="button"
          onClick={onClick}
          initial={{ y: 48, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          exit={{ y: 28, opacity: 0 }}
          transition={{ type: 'spring', stiffness: 400, damping: 32 }}
          className="pointer-events-auto mx-auto flex min-h-[3.5rem] w-full max-w-full flex-col items-center justify-center gap-0.5 rounded-[14px] bg-gradient-to-br from-gold to-[#A87820] px-4 py-2.5 text-center shadow-[0_8px_28px_rgba(200,168,76,0.35)]"
        >
          <span className="font-body text-[0.9rem] font-extrabold tracking-[0.1em] text-black">
            {t('guest.cart.viewOrder')}
          </span>
          <div className="flex items-baseline justify-center gap-1.5 font-mono text-[0.88rem] font-bold tabular-nums text-black/95">
            <span>{t('guest.cart.itemsInOrder', { count: totalItems })}</span>
            <span className="text-black/40" aria-hidden>
              ·
            </span>
            <span>{formatUsd(grandTotal)}</span>
          </div>
        </motion.button>
      </div>
    </div>
  )
}
