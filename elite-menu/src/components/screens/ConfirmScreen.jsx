import { motion } from 'framer-motion'
import { Check } from 'lucide-react'
import { useTranslation } from 'react-i18next'

function Particles() {
  const dots = Array.from({ length: 12 }, (_, i) => ({
    id: i,
    left: `${6 + ((i * 19) % 88)}%`,
    duration: 7 + (i % 4) * 2,
    delay: i * 0.25,
    opacity: 0.1 + (i % 5) * 0.04,
  }))
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden>
      {dots.map((d) => (
        <motion.span
          key={d.id}
          className="absolute h-0.5 w-0.5 rounded-full bg-gold"
          style={{ left: d.left, bottom: '5%' }}
          initial={{ opacity: d.opacity, y: 0 }}
          animate={{ y: [-10, -380], opacity: [d.opacity, 0.04] }}
          transition={{
            duration: d.duration,
            repeat: Infinity,
            ease: 'linear',
            delay: d.delay,
          }}
        />
      ))}
    </div>
  )
}

/**
 * @param {object} props
 * @param {string} [props.heading]
 * @param {string} [props.primaryCtaLabel]
 * @param {string} [props.secondaryCtaLabel]
 * @param {'emerald' | 'gold'} [props.accent]
 * @param {{ label: string; value: string }[]} [props.details]
 * @param {string} [props.confirmationCode]
 * @param {import('react').ReactNode} [props.receipt]
 */
export function ConfirmScreen({
  label,
  message,
  estimatedPrepMinutes,
  onOrderMore,
  onBackToStart,
  heading,
  primaryCtaLabel,
  secondaryCtaLabel,
  accent = 'emerald',
  details,
  confirmationCode,
  receipt,
}) {
  const { t } = useTranslation()
  const headingText = heading ?? t('guest.confirm.heading')
  const primaryLabel = primaryCtaLabel ?? t('guest.confirm.orderMore')
  const secondaryLabel = secondaryCtaLabel ?? t('guest.confirm.backToStart')
  const ringAccent =
    accent === 'gold'
      ? 'border-amber-400/45 bg-amber-500/10 shadow-[0_0_32px_rgba(200,168,76,0.22)]'
      : 'border-emerald-500/40 bg-emerald-500/10 shadow-[0_0_32px_rgba(16,185,129,0.2)]'
  const iconAccent = accent === 'gold' ? 'text-amber-300' : 'text-emerald-400'

  return (
    <motion.div
      className="relative flex min-h-[100svh] flex-col overflow-x-hidden bg-midnight"
      initial={{ opacity: 0, scale: 0.98 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0, y: -16 }}
      transition={{ type: 'spring', stiffness: 320, damping: 34 }}
    >
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[50vw] w-[50vw] rounded-full bg-[rgba(200,168,76,0.05)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[35vw] w-[35vw] rounded-full bg-[rgba(16,185,129,0.04)] blur-3xl" />
      <Particles />

      <div className="relative z-10 flex min-h-0 flex-1 flex-col items-center overflow-y-auto overscroll-y-contain px-8 pb-[max(2rem,env(safe-area-inset-bottom))] pt-10">
        <motion.div
          initial={{ scale: 0.6, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ type: 'spring', stiffness: 400, damping: 22, delay: 0.05 }}
          className={'mb-6 flex h-20 w-20 items-center justify-center rounded-full border-2 ' + ringAccent}
        >
          <Check className={'h-9 w-9 ' + iconAccent} strokeWidth={2.5} />
        </motion.div>

        <h2 className="text-center font-display text-2xl italic font-semibold text-champagne">{headingText}</h2>
        <p className="mt-2 max-w-[300px] text-center font-body text-sm leading-relaxed text-[var(--text-muted)]">
          {t('guest.confirm.message')}
        </p>
        {confirmationCode ? (
          <div className="mt-6 w-full max-w-[320px] rounded-2xl border border-gold/30 bg-[var(--gold-dim)] px-6 py-5 text-center shadow-[0_12px_40px_rgba(0,0,0,0.2)]">
            <p className="font-body text-[0.65rem] font-semibold uppercase tracking-[0.22em] text-gold/85">
              {t('guest.confirm.confirmationCode')}
            </p>
            <p className="mt-3 font-mono text-[2.5rem] font-bold leading-none tracking-[0.2em] text-champagne">
              {confirmationCode}
            </p>
            <p className="mt-3 font-body text-xs leading-relaxed text-champagne/55">
              {t('guest.confirm.codeHint')}
            </p>
          </div>
        ) : null}
        {receipt ? <div className="mt-6 flex w-full justify-center">{receipt}</div> : null}
        {details != null && details.length > 0 ? (
          <div className="mt-6 w-full max-w-[320px] space-y-3">
            {details.map((row) => (
              <div
                key={row.label}
                className="rounded-2xl border border-champagne/15 bg-midnight-2 px-5 py-3 text-left shadow-[0_12px_40px_rgba(0,0,0,0.2)]"
              >
                <p className="font-body text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-gold/80">
                  {row.label}
                </p>
                <p className="mt-1 font-body text-sm font-medium leading-snug text-champagne">{row.value}</p>
              </div>
            ))}
          </div>
        ) : null}
        {label ? (
          <div className="mt-6 rounded-2xl border border-gold/20 bg-[var(--gold-dim)] px-6 py-3 text-center">
            <p className="font-body text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-gold/80">
              {t('guest.general.reference')}
            </p>
            <p className="mt-1 font-mono text-lg font-medium text-champagne">{label}</p>
          </div>
        ) : null}
        {estimatedPrepMinutes != null && estimatedPrepMinutes > 0 ? (
          <div className="mt-4 max-w-[300px] rounded-2xl border border-champagne/15 bg-midnight-2 px-5 py-3 text-center">
            <p className="font-body text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-gold/80">
              {t('guest.confirm.estPrepTitle')}
            </p>
            <p className="mt-1 font-body text-base font-semibold text-champagne">
              {t('guest.confirm.estPrepMinutes', { minutes: estimatedPrepMinutes })}
            </p>
          </div>
        ) : null}

        <div className="mt-10 flex w-full max-w-[280px] flex-col gap-3">
          <button
            type="button"
            onClick={onOrderMore}
            className="min-h-[50px] w-full rounded-xl border border-gold/45 bg-gold/10 font-body text-[0.9rem] font-bold uppercase tracking-[0.1em] text-gold transition-colors hover:bg-gold/15"
          >
            {primaryLabel}
          </button>
          {onBackToStart ? (
            <button
              type="button"
              onClick={onBackToStart}
              className="min-h-[44px] w-full font-body text-sm text-champagne/50 hover:text-champagne/80"
            >
              {secondaryLabel}
            </button>
          ) : null}
        </div>
      </div>
    </motion.div>
  )
}
