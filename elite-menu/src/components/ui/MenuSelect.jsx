import { ChevronDown, ChevronUp } from 'lucide-react'
import { useCallback, useEffect, useId, useRef, useState } from 'react'

/**
 * Themed "dropdown" (no native &lt;select&gt;) to match the app.
 * @param {object} props
 * @param {string} props.id — optional, stable id
 * @param {string} props.label — small caps label above
 * @param {string[]} props.options — option values; first is often "All"
 * @param {string} props.value
 * @param {(v: string) => void} props.onChange
 */
export function MenuSelect({ id, label, options, value, onChange }) {
  const genId = useId()
  const listId = id || `menu-select-${genId.replace(/:/g, '')}`
  const [open, setOpen] = useState(false)
  const wrapRef = useRef(null)
  const selectedLabel = options.includes(value) ? value : options[0] ?? '—'

  const close = useCallback(() => setOpen(false), [])

  useEffect(() => {
    if (!open) return
    const onDoc = (e) => {
      if (wrapRef.current && !wrapRef.current.contains(/** @type {Node} */ (e.target))) {
        setOpen(false)
      }
    }
    const onKey = (e) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDoc)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <div className="space-y-2" ref={wrapRef}>
      {label ? (
        <p className="text-center font-body text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-champagne/60">
          {label}
        </p>
      ) : null}
      <div className="relative">
        <button
          type="button"
          id={listId}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-controls={`${listId}-listbox`}
          onClick={() => setOpen((o) => !o)}
          className="flex h-[48px] w-full items-center justify-between gap-2 rounded-[12px] border border-champagne/15 bg-champagne/[0.07] px-3 font-body text-[0.9rem] text-champagne transition-colors hover:border-champagne/25"
        >
          <span className="min-w-0 truncate text-left">{selectedLabel}</span>
          {open ? (
            <ChevronUp className="h-4 w-4 shrink-0 text-gold/80" strokeWidth={2} />
          ) : (
            <ChevronDown className="h-4 w-4 shrink-0 text-gold/80" strokeWidth={2} />
          )}
        </button>
        {open ? (
          <ul
            id={`${listId}-listbox`}
            role="listbox"
            aria-labelledby={listId}
            className="absolute left-0 right-0 z-[100] mt-1 max-h-60 list-none overflow-y-auto rounded-[12px] border border-champagne/20 bg-midnight-2 py-1 shadow-lg shadow-black/40 [-webkit-overflow-scrolling:touch]"
          >
            {options.map((opt) => {
              const isActive = opt === value
              return (
                <li key={opt} role="option" aria-selected={isActive}>
                  <button
                    type="button"
                    className={`flex w-full px-3 py-2.5 text-left font-body text-[0.9rem] transition-colors ${
                      isActive
                        ? 'bg-gold/15 text-gold'
                        : 'text-champagne hover:bg-champagne/10'
                    }`}
                    onClick={() => {
                      onChange(opt)
                      close()
                    }}
                  >
                    {opt}
                  </button>
                </li>
              )
            })}
          </ul>
        ) : null}
      </div>
    </div>
  )
}
