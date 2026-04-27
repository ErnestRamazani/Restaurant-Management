export function ErrorScreen({ message, onRetry }) {
  return (
    <div className="flex min-h-[100svh] flex-col items-center justify-center bg-midnight px-8 text-center">
      <p className="font-body text-champagne/90">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-8 min-h-[44px] rounded-xl border border-gold/50 px-6 font-body text-sm font-semibold text-gold"
      >
        Try again
      </button>
    </div>
  )
}
