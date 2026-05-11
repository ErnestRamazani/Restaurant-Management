import { motion } from 'framer-motion'

export function LoadingScreen() {
  return (
    <div className="flex min-h-[100svh] flex-col items-center justify-center bg-midnight px-6">
      <motion.div
        className="h-12 w-12 rounded-full border-2 border-gold/30 border-t-gold"
        animate={{ rotate: 360 }}
        transition={{ repeat: Infinity, duration: 0.9, ease: 'linear' }}
      />
      <p className="mt-6 font-body text-sm text-[var(--text-muted)]">Preparing your experience…</p>
      {import.meta.env.DEV ? (
        <p className="mt-4 max-w-sm text-center font-body text-xs leading-relaxed text-champagne/40">
          Dev: the menu calls the API via <span className="text-champagne/55">/api</span> →{' '}
          <span className="text-champagne/55">localhost:8080</span>. If this stays here, start{' '}
          <span className="text-champagne/55">EliteRestaurant.Api</span> (or wait for the request to time out).
        </p>
      ) : null}
    </div>
  )
}
