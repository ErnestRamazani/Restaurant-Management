import { motion } from 'framer-motion'
import { Check } from 'lucide-react'

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

export function ConfirmScreen({ label, message, estimatedPrepMinutes, onOrderMore, onBackToStart }) {
  return (
    <motion.div
      className="relative flex min-h-[100svh] flex-col overflow-hidden bg-midnight"
      initial={{ opacity: 0, scale: 0.98 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0, y: -16 }}
      transition={{ type: 'spring', stiffness: 320, damping: 34 }}
    >
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[50vw] w-[50vw] rounded-full bg-[rgba(200,168,76,0.05)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[35vw] w-[35vw] rounded-full bg-[rgba(16,185,129,0.04)] blur-3xl" />
      <Particles />

      <div className="relative z-10 flex flex-1 flex-col items-center justify-center px-8 pb-[max(2rem,env(safe-area-inset-bottom))] pt-[12vh]">
        <motion.div
          initial={{ scale: 0.6, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ type: 'spring', stiffness: 400, damping: 22, delay: 0.05 }}
          className="mb-6 flex h-20 w-20 items-center justify-center rounded-full border-2 border-emerald-500/40 bg-emerald-500/10 shadow-[0_0_32px_rgba(16,185,129,0.2)]"
        >
          <Check className="h-9 w-9 text-emerald-400" strokeWidth={2.5} />
        </motion.div>

        <h2 className="text-center font-display text-2xl italic font-semibold text-champagne">We received it</h2>
        <p className="mt-2 max-w-[300px] text-center font-body text-sm leading-relaxed text-[var(--text-muted)]">
          {message || 'The kitchen and your server can see this order as a request.'}
        </p>
        {label ? (
          <div className="mt-6 rounded-2xl border border-gold/20 bg-[var(--gold-dim)] px-6 py-3 text-center">
            <p className="font-body text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-gold/80">Reference</p>
            <p className="mt-1 font-mono text-lg font-medium text-champagne">{label}</p>
          </div>
        ) : null}
        {estimatedPrepMinutes != null && estimatedPrepMinutes > 0 ? (
          <div className="mt-4 max-w-[300px] rounded-2xl border border-champagne/15 bg-midnight-2 px-5 py-3 text-center">
            <p className="font-body text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-gold/80">
              Estimated kitchen time
            </p>
            <p className="mt-1 font-body text-base font-semibold text-champagne">
              About {estimatedPrepMinutes} minutes
            </p>
          </div>
        ) : null}

        <div className="mt-10 flex w-full max-w-[280px] flex-col gap-3">
          <button
            type="button"
            onClick={onOrderMore}
            className="min-h-[50px] w-full rounded-xl border border-gold/45 bg-gold/10 font-body text-[0.9rem] font-bold uppercase tracking-[0.1em] text-gold transition-colors hover:bg-gold/15"
          >
            Order more
          </button>
          {onBackToStart ? (
            <button
              type="button"
              onClick={onBackToStart}
              className="min-h-[44px] w-full font-body text-sm text-champagne/50 hover:text-champagne/80"
            >
              Back to start
            </button>
          ) : null}
        </div>
      </div>
    </motion.div>
  )
}
