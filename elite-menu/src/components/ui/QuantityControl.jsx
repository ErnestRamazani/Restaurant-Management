import { Minus, Plus } from 'lucide-react'

/**
 * @param {'compact' | 'default'} variant
 * Plain <button> so Framer / layout does not eat touches on real phones.
 */
export function QuantityControl({ value, onMinus, onPlus, variant = 'default', disablePlus = false }) {
  const isCompact = variant === 'compact'
  const btn = isCompact ? 'h-[32px] w-[32px] min-h-[32px] min-w-[32px]' : 'h-11 w-11 min-h-[44px] min-w-[44px]'
  const text = isCompact ? 'text-[0.9rem]' : 'text-[1.1rem]'

  const cap = (e) => {
    e.stopPropagation()
    if (e.nativeEvent && typeof e.nativeEvent.stopImmediatePropagation === 'function') {
      e.nativeEvent.stopImmediatePropagation()
    }
  }

  return (
    <div className="relative z-10 flex items-center gap-1.5">
      <button
        type="button"
        onClick={(e) => {
          cap(e)
          onMinus()
        }}
        className={`active:scale-95 inline-flex touch-manipulation select-none items-center justify-center rounded-full border-2 border-gold bg-midnight-2 text-gold ${btn}`}
        aria-label="Decrease quantity"
      >
        <Minus className={isCompact ? 'h-3.5 w-3.5' : 'h-4 w-4'} strokeWidth={2.5} />
      </button>
      <span
        className={`min-w-[28px] text-center font-mono font-bold text-champagne ${text} ${isCompact ? 'min-w-[22px]' : 'min-w-[40px]'}`}
      >
        {value}
      </span>
      <button
        type="button"
        disabled={disablePlus}
        onClick={(e) => {
          cap(e)
          if (disablePlus) return
          onPlus()
        }}
        className={`inline-flex touch-manipulation select-none items-center justify-center rounded-full bg-gold text-black ${btn} ${disablePlus ? 'cursor-not-allowed opacity-40' : 'active:scale-95'}`}
        aria-label="Increase quantity"
      >
        <Plus className={isCompact ? 'h-3.5 w-3.5' : 'h-4 w-4'} strokeWidth={2.5} />
      </button>
    </div>
  )
}
