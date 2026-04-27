import { motion } from 'framer-motion'
import {
  Cake,
  Coffee,
  Fish,
  GlassWater,
  LayoutGrid,
  Leaf,
  Utensils,
  UtensilsCrossed,
  Wine,
} from 'lucide-react'

function iconFor(label, sectionKind) {
  if (label === 'All') return sectionKind === 'drink' ? GlassWater : LayoutGrid
  if (label === 'Alcohol' || String(label).toLowerCase() === 'alcohol') return Wine
  if (label === 'Non-alcohol' || String(label).toLowerCase().includes('non-alcohol')) return Coffee
  const s = String(label || '').toLowerCase()
  if (s.includes('main')) return UtensilsCrossed
  if (s.includes('starter') || s.includes('appet')) return Leaf
  if (s.includes('dessert')) return Cake
  if (s.includes('drink') || s.includes('beverage') || s.includes('coffee') || s.includes('juice') || s.includes('mojito') || s.includes('espresso') || s.includes('latte') || s.includes('wine') || s.includes('cocktail')) return GlassWater
  if (s.includes('seafood') || s.includes('fish')) return Fish
  if (s.includes('pizza') || s.includes('pasta') || s.includes('penne') || s.includes('spaghetti')) return UtensilsCrossed
  return sectionKind === 'drink' ? GlassWater : Utensils
}

/** @param {string} raw */
function pillText(raw) {
  const t = String(raw).trim()
  if (/^starter\/?\s*appetizer/i.test(t)) return 'Starters'
  return t
}

export function CategoryBar({ categories, active, onSelect, sectionKind = 'food' }) {
  return (
    <div className="min-h-[78px] shrink-0 border-b border-champagne/10 bg-midnight-2">
      <div className="flex h-full min-h-[78px] gap-2 overflow-x-auto px-3 py-2 [-webkit-overflow-scrolling:touch]">
        {categories.map((cat) => {
          const Icon = iconFor(cat, sectionKind)
          const isActive = active === cat
          const display = pillText(cat)
          return (
            <button
              key={cat}
              type="button"
              title={String(cat)}
              onClick={() => onSelect(cat)}
              className={`relative flex min-w-[4.5rem] max-w-[5.5rem] shrink-0 flex-col items-center justify-center gap-0.5 rounded-2xl px-1.5 py-1.5 transition-colors duration-150 ${
                isActive
                  ? 'border border-gold/50 text-gold'
                  : 'border border-champagne/10 bg-champagne/5 text-[var(--text-muted)]'
              }`}
            >
              {isActive && (
                <motion.div
                  layoutId={`activeSubcatPill-${sectionKind}`}
                  className="absolute inset-0 rounded-2xl bg-[var(--gold-dim)]"
                  transition={{ type: 'spring', stiffness: 400, damping: 35 }}
                />
              )}
              <span className="relative z-10 flex justify-center">
                <Icon className="h-4 w-4 shrink-0" strokeWidth={1.8} />
              </span>
              <span
                className={`relative z-10 w-full min-w-0 break-words px-0.5 text-center text-[0.58rem] uppercase leading-[1.1] tracking-[0.04em] [overflow-wrap:anywhere] ${
                  isActive ? 'font-bold' : 'font-medium'
                }`}
                style={{
                  display: '-webkit-box',
                  WebkitLineClamp: 2,
                  WebkitBoxOrient: 'vertical',
                  overflow: 'hidden',
                }}
              >
                {display}
              </span>
            </button>
          )
        })}
      </div>
    </div>
  )
}
