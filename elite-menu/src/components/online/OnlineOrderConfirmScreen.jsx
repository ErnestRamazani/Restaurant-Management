import { motion } from 'framer-motion'
import { Check, Download } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { formatUsd } from '../../utils/format'
import { downloadGuestReceiptPdf } from '../../utils/guestReceiptPdf'

/** @param {import('i18next').TFunction} t */
function fulfillmentDisplay(t, mode) {
  const m = String(mode ?? '').trim()
  if (m === 'Delivery') return t('guest.online.delivery')
  if (m === 'Pickup') return t('guest.online.pickup')
  return m
}

/**
 * Single-viewport confirmation for mobile online orders (no scroll).
 * @param {object} props
 * @param {string} [props.confirmationCode]
 * @param {object} [props.receipt]
 * @param {string} [props.restaurantName]
 * @param {string} [props.label]
 * @param {number | null} [props.estimatedPrepMinutes]
 * @param {() => void} props.onOrderMore
 * @param {() => void} [props.onBackToStart]
 */
export function OnlineOrderConfirmScreen({
  confirmationCode,
  receipt,
  restaurantName,
  label,
  estimatedPrepMinutes,
  onOrderMore,
  onBackToStart,
}) {
  const { t } = useTranslation()
  const code = String(confirmationCode ?? receipt?.confirmationCode ?? '').trim()
  const lines = Array.isArray(receipt?.lines) ? receipt.lines : []
  const itemCount = lines.reduce((n, l) => n + Number(l.quantity || 0), 0)
  const grandTotal = receipt?.grandTotal != null ? Number(receipt.grandTotal) : 0

  return (
    <motion.div
      className="flex h-[100dvh] max-h-[100dvh] flex-col overflow-hidden bg-midnight text-champagne"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center px-5 pt-[max(0.5rem,env(safe-area-inset-top))]">
        <div className="mb-2 flex h-11 w-11 items-center justify-center rounded-full border-2 border-emerald-500/40 bg-emerald-500/10">
          <Check className="h-5 w-5 text-emerald-400" strokeWidth={2.5} />
        </div>

        <h1 className="text-center font-display text-lg font-semibold italic leading-tight">
          {t('guest.online.confirmHeading')}
        </h1>
        <p className="mt-1 text-center font-body text-[0.7rem] leading-snug text-champagne/55">
          {t('guest.online.confirmScreenshot')}
        </p>

        {code ? (
          <div className="mt-3 w-full max-w-[280px] rounded-2xl border border-gold/35 bg-[var(--gold-dim)] px-4 py-3 text-center">
            <p className="font-body text-[0.58rem] font-semibold uppercase tracking-[0.2em] text-gold/85">
              {t('guest.online.confirmCode')}
            </p>
            <p className="mt-1 font-mono text-[2.1rem] font-bold leading-none tracking-[0.16em] text-champagne">
              {code}
            </p>
          </div>
        ) : null}

        {receipt ? (
          <div className="mt-3 w-full max-w-[280px] rounded-xl border border-champagne/12 bg-midnight-2/90 px-3 py-2.5 font-body text-[0.72rem] leading-snug text-champagne/75">
            <p>
              <span className="font-semibold text-gold/90">{fulfillmentDisplay(t, receipt.fulfillment)}</span>
              {receipt.customerName ? ` · ${receipt.customerName}` : ''}
            </p>
            {receipt.phone ? (
              <p className="mt-0.5 truncate text-champagne/60">
                {t('guest.online.tel')} {receipt.phone}
              </p>
            ) : null}
            {receipt.fulfillment === 'Delivery' && receipt.address ? (
              <p className="mt-0.5 line-clamp-2 text-champagne/60">{receipt.address}</p>
            ) : null}
            <p className="mt-1.5 flex justify-between border-t border-champagne/10 pt-1.5 font-semibold text-champagne">
              <span>{t('guest.cart.itemsInOrder', { count: itemCount })}</span>
              <span className="font-mono text-gold">{formatUsd(grandTotal)}</span>
            </p>
          </div>
        ) : label ? (
          <p className="mt-2 font-mono text-xs text-champagne/50">{label}</p>
        ) : null}

        {estimatedPrepMinutes != null && estimatedPrepMinutes > 0 ? (
          <p className="mt-2 text-center font-body text-[0.68rem] text-champagne/50">
            {t('guest.online.kitchenEstimate', { minutes: estimatedPrepMinutes })}
          </p>
        ) : null}

        {receipt ? (
          <button
            type="button"
            onClick={() => void downloadGuestReceiptPdf(receipt, restaurantName)}
            className="mt-3 flex min-h-[36px] items-center justify-center gap-1.5 rounded-lg border border-gold/30 px-3 font-body text-[0.65rem] font-bold uppercase tracking-[0.1em] text-gold"
          >
            <Download className="h-3.5 w-3.5" aria-hidden />
            {t('guest.online.downloadPdf')}
          </button>
        ) : null}
      </div>

      <div className="shrink-0 space-y-2 px-5 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-2">
        <button
          type="button"
          onClick={onOrderMore}
          className="flex min-h-[46px] w-full items-center justify-center rounded-xl border border-gold/45 bg-gold/10 font-body text-[0.82rem] font-bold uppercase tracking-[0.08em] text-gold"
        >
          {t('guest.online.orderMore')}
        </button>
        {onBackToStart ? (
          <button
            type="button"
            onClick={onBackToStart}
            className="flex min-h-[40px] w-full items-center justify-center font-body text-sm text-champagne/50"
          >
            {t('guest.online.backHome')}
          </button>
        ) : null}
      </div>
    </motion.div>
  )
}
