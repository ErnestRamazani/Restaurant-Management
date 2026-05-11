import { useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import { getCategoryColor } from '../../utils/placeholders'
import { formatUsd } from '../../utils/format'
import { productIsAvailable } from '../../utils/availability'
import { QuantityControl } from './QuantityControl'

function previewComposition(composition) {
  if (!composition || !String(composition).trim()) return ''
  const parts = String(composition)
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
  if (parts.length === 0) return ''
  if (parts.length <= 3) return parts.join(', ')
  return `${parts.slice(0, 3).join(', ')}…`
}

function stopBub(e) {
  e.stopPropagation()
  e.nativeEvent?.stopImmediatePropagation?.()
}

export function ProductCard({ product, qty, onOpen, onAdd, onMinus, onPlus, onRemoveLine }) {
  const available = productIsAvailable(product)
  const cat = product.category || ''
  const sub = product.subcategory || 'General'
  const ph = getCategoryColor(cat)
  const initial = (product.name || '?').trim().charAt(0) || '?'
  const desc = product.description ? String(product.description) : ''
  const comp = product.composition ? String(product.composition) : ''
  const preview = previewComposition(comp)
  const photoUrl = product.photoUrl ? String(product.photoUrl) : ''
  const [imgErr, setImgErr] = useState(false)
  const showPhoto = photoUrl && !imgErr

  const open = () => {
    onOpen(product)
  }

  return (
    <article className="group mb-3 overflow-hidden rounded-2xl border border-champagne/10 bg-midnight-2 shadow-[0_4px_20px_rgba(0,0,0,0.3)]">
      {/* Dim menu content while unavailable; CTA is a sibling so “Out of order” stays true red (no grayscale/opacity on it). */}
      <div className={!available ? 'opacity-60 grayscale' : ''}>
        <div
          className="relative block w-full cursor-pointer text-left"
          onClick={open}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              open()
            }
          }}
          role="button"
          tabIndex={0}
          aria-label={
            available
              ? `View details, ${String(product.name ?? '')}`
              : `View details, ${String(product.name ?? '')} (out of order, not available to add)`
          }
        >
          <div className="relative h-[200px] w-full overflow-hidden">
            {photoUrl ? (
              <img
                src={photoUrl}
                alt=""
                className={showPhoto ? 'h-full w-full object-cover' : 'hidden'}
                onError={() => setImgErr(true)}
              />
            ) : null}
            <div
              className={`absolute inset-0 flex flex-col items-center justify-center ${showPhoto ? 'hidden' : 'flex'}`}
              style={{ background: ph }}
            >
              <span
                className="font-display text-[5rem] italic leading-none text-white/[0.15]"
                style={{ fontFamily: '"Playfair Display", serif' }}
              >
                {initial}
              </span>
              <span className="mt-2 text-[0.7rem] font-medium uppercase tracking-[0.15em] text-white/30">{sub}</span>
            </div>
            {qty > 0 && (
              <span
                className={`pointer-events-none absolute top-3 flex h-[26px] min-w-[26px] items-center justify-center rounded-full bg-gold px-1.5 font-mono text-[0.8rem] font-black text-black shadow-md ${!available ? 'left-3' : 'right-3'}`}
              >
                {qty}
              </span>
            )}
            <div
              className="pointer-events-none absolute inset-0 bg-gradient-to-b from-transparent from-40% to-[rgba(15,25,35,0.9)] to-100%"
              aria-hidden
            />
            <div className="pointer-events-none absolute bottom-3 left-3 rounded bg-black/30 px-2 py-0.5 font-body text-[0.65rem] font-semibold uppercase tracking-[0.15em] text-gold">
              {sub}
            </div>
          </div>
        </div>

        <div className="px-4 pt-3.5">
          <div className="flex cursor-pointer items-start justify-between gap-2" onClick={open}>
            <h3 className="line-clamp-2 max-w-[70%] font-display text-[1.05rem] font-semibold leading-snug text-champagne">
              {product.name}
            </h3>
            <span
              className="shrink-0 bg-gradient-to-r from-gold via-gold-light to-gold bg-[length:200%_100%] font-mono text-[1rem] font-medium text-transparent price-shimmer group-hover:animate-shimmer"
              style={{ WebkitBackgroundClip: 'text', backgroundClip: 'text' }}
            >
              {formatUsd(product.price)}
            </span>
          </div>
          {desc ? (
            <p
              onClick={open}
              className="mt-1 line-clamp-2 cursor-pointer font-body text-[0.8rem] font-light text-[var(--text-muted)]"
            >
              {desc}
            </p>
          ) : null}
        </div>
      </div>

      {/* Same row as before: Details (dimmed when unavailable) | action (never dimmed so red stays vivid). */}
      <div
        className="relative z-10 mt-3 flex min-h-[44px] items-end justify-between gap-2 px-4 pb-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className={`min-w-0 flex-1 ${!available ? 'opacity-60 grayscale' : ''}`}>
          {preview ? (
            <p
              onClick={open}
              className="cursor-pointer font-body text-[0.72rem] text-champagne/35"
            >
              {preview.replace(/…$/, '')}
              <span className="text-gold/80"> Details</span>
            </p>
          ) : (
            <span
              onClick={open}
              className="block cursor-pointer font-body text-[0.72rem] text-champagne/30"
            >
              <span className="text-gold/80">Details</span>
            </span>
          )}
        </div>
        <div
          className="flex shrink-0 items-center gap-1.5"
          onPointerDown={(e) => e.stopPropagation()}
          onClick={(e) => e.stopPropagation()}
        >
          {qty === 0 ? (
            available ? (
              <button
                type="button"
                onClick={(e) => {
                  stopBub(e)
                  onAdd(product)
                }}
                className="flex h-10 w-10 min-h-[40px] min-w-[40px] touch-manipulation select-none items-center justify-center rounded-full bg-gold text-black shadow-md active:scale-95"
                aria-label="Add to order"
              >
                <Plus className="h-5 w-5" strokeWidth={3} />
              </button>
            ) : (
              <span
                className="flex min-h-10 min-w-[7.5rem] select-none items-center justify-center rounded-full border-2 border-red-600 bg-red-600/10 px-3 font-body text-[0.75rem] font-extrabold uppercase tracking-[0.06em]"
                style={{ color: '#dc2626' }}
                aria-label="Not available — out of order"
              >
                Out of order
              </span>
            )
          ) : (
            <>
              <QuantityControl
                variant="compact"
                value={qty}
                onMinus={onMinus}
                onPlus={onPlus}
                disablePlus={!available || qty >= 20}
              />
              {typeof onRemoveLine === 'function' ? (
                <button
                  type="button"
                  onClick={(e) => {
                    stopBub(e)
                    onRemoveLine(product)
                  }}
                  className="inline-flex h-[32px] w-[32px] min-h-[32px] min-w-[32px] touch-manipulation select-none items-center justify-center rounded-full border border-champagne/20 bg-midnight-2 text-champagne/70 transition hover:border-red-500/40 hover:bg-red-500/10 hover:text-red-300 active:scale-95"
                  aria-label="Remove line from cart"
                >
                  <Trash2 className="h-3.5 w-3.5" strokeWidth={2.25} />
                </button>
              ) : null}
            </>
          )}
        </div>
      </div>
    </article>
  )
}
