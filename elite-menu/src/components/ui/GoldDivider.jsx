export function GoldDivider({ className = '', narrow }) {
  return (
    <div
      className={`h-px bg-gold/20 ${narrow ? 'mx-auto w-[60px]' : 'w-full'} ${className}`}
      aria-hidden
    />
  )
}
