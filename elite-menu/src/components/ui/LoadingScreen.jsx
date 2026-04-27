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
    </div>
  )
}
