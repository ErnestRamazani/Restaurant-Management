import { AnimatePresence, motion } from 'framer-motion'
import { ArrowLeft, ShoppingBag } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { resolveApiAssetUrl } from '../../utils/apiClient'
import { formatUsd } from '../../utils/format'
import { ProductSheet } from '../screens/ProductSheet'
import { ProductCard } from '../ui/ProductCard'

const spring = { type: 'spring', stiffness: 300, damping: 34 }

export function OnlineOrderMenuScreen() {
  const navigate = useNavigate()
  const { cart, config, products: productsFromContext } = /** @type {import('./OnlineOrderLayout').OnlineOrderOutletContext} */ (
    useOutletContext()
  )
  const products = Array.isArray(productsFromContext) ? productsFromContext : []
  const [sheetProduct, setSheetProduct] = useState(/** @type {Record<string, unknown> | null} */ (null))

  const categories = useMemo(() => {
    const s = new Set()
    for (const p of products) {
      const c = String(p.category ?? 'Menu').trim() || 'Menu'
      s.add(c)
    }
    return ['All', ...Array.from(s).sort((a, b) => a.localeCompare(b))]
  }, [products])

  const [cat, setCat] = useState('All')

  const filtered = useMemo(() => {
    if (cat === 'All') return products
    return products.filter((p) => String(p.category ?? '') === cat)
  }, [products, cat])

  const promoTitle = config?.onlinePromoTitle != null ? String(config.onlinePromoTitle).trim() : ''
  const promoSubtitle = config?.onlinePromoSubtitle != null ? String(config.onlinePromoSubtitle).trim() : ''
  const promoCta =
    config?.onlinePromoCtaLabel != null && String(config.onlinePromoCtaLabel).trim()
      ? String(config.onlinePromoCtaLabel).trim()
      : 'Shop the menu'
  const promoImg =
    config?.onlinePromoImageUrl != null && String(config.onlinePromoImageUrl).trim()
      ? resolveApiAssetUrl(String(config.onlinePromoImageUrl).trim())
      : ''

  return (
    <div className="min-h-[100svh] bg-midnight text-champagne">
      <header className="sticky top-0 z-20 flex h-14 items-center gap-2 border-b border-champagne/10 bg-midnight/95 px-3 backdrop-blur-md">
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="flex h-11 min-w-[44px] items-center justify-center rounded-xl text-champagne"
          aria-label="Back"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <h1 className="flex-1 text-center font-display text-lg font-semibold">Order online</h1>
        <div className="w-11" aria-hidden />
      </header>

      <div className="px-4 pb-28 pt-4">
        {promoTitle || promoImg ? (
          <motion.section
            initial={false}
            animate={{ opacity: 1, y: 0 }}
            transition={spring}
            className="relative mb-6 overflow-hidden rounded-3xl border border-gold/20 bg-midnight-2 shadow-[0_16px_50px_rgba(0,0,0,0.35)]"
          >
            {promoImg ? (
              <div className="relative h-40 w-full sm:h-48">
                <img src={promoImg} alt="" className="h-full w-full object-cover" />
                <div className="absolute inset-0 bg-gradient-to-t from-midnight via-midnight/40 to-transparent" />
              </div>
            ) : null}
            <div className={promoImg ? 'relative -mt-10 px-5 pb-5' : 'px-5 py-5'}>
              {promoTitle ? (
                <h2 className="font-display text-2xl italic leading-tight text-champagne">{promoTitle}</h2>
              ) : null}
              {promoSubtitle ? (
                <p className="mt-2 font-body text-sm leading-relaxed text-champagne/70">{promoSubtitle}</p>
              ) : null}
              <button
                type="button"
                onClick={() => window.scrollTo({ top: 400, behavior: 'smooth' })}
                className="mt-4 inline-flex min-h-[44px] items-center justify-center rounded-2xl bg-gold px-4 font-body text-xs font-extrabold uppercase tracking-[0.14em] text-black"
              >
                {promoCta}
              </button>
            </div>
          </motion.section>
        ) : null}

        <p className="mb-2 font-body text-[0.72rem] font-bold uppercase tracking-[0.14em] text-gold/90">
          Categories
        </p>
        <div className="mb-4 flex gap-2 overflow-x-auto pb-1">
          {categories.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => setCat(c)}
              className={`shrink-0 rounded-full border px-3 py-1.5 font-body text-[0.72rem] font-semibold uppercase tracking-wider ${
                cat === c ? 'border-gold/50 bg-gold/10 text-gold' : 'border-champagne/15 text-champagne/55'
              }`}
            >
              {c}
            </button>
          ))}
        </div>

        <AnimatePresence initial={false} mode="popLayout">
          {filtered.map((p) => (
            <motion.div key={String(p.id)} layout initial={{ opacity: 0.6 }} animate={{ opacity: 1 }}>
              <ProductCard
                product={p}
                qty={cart.getItemQty(p.id)}
                onOpen={setSheetProduct}
                onAdd={() => cart.addItem(p)}
                onMinus={() => cart.decrementLine(p.id)}
                onPlus={() => cart.incrementLine(p.id)}
                onRemoveLine={() => cart.removeItem(p.id)}
              />
            </motion.div>
          ))}
        </AnimatePresence>
      </div>

      <div className="fixed bottom-0 left-0 right-0 z-30 border-t border-champagne/10 bg-midnight-2/95 px-4 py-3 backdrop-blur-md">
        <div className="mx-auto flex max-w-lg items-center gap-2 sm:gap-3">
          <div className="flex min-w-0 flex-1 items-center gap-2">
            <ShoppingBag className="h-5 w-5 shrink-0 text-gold" />
            <div className="min-w-0">
              <p className="truncate font-body text-xs font-semibold text-champagne">
                {cart.totalItems} item{cart.totalItems === 1 ? '' : 's'}
              </p>
              <p className="truncate font-mono text-sm text-gold">{formatUsd(cart.grandTotal)}</p>
            </div>
          </div>
          {cart.lines.length > 0 ? (
            <button
              type="button"
              onClick={() => cart.clearCart()}
              className="shrink-0 rounded-lg px-2 py-1.5 font-body text-[0.62rem] font-bold uppercase tracking-[0.1em] text-champagne/50 underline decoration-champagne/25 underline-offset-2 hover:text-red-300 hover:decoration-red-400/50"
            >
              Clear
            </button>
          ) : null}
          <button
            type="button"
            disabled={cart.lines.length === 0}
            onClick={() => navigate('/order-online/checkout')}
            className="h-12 shrink-0 rounded-2xl bg-gold px-4 font-body text-xs font-extrabold uppercase tracking-[0.12em] text-black disabled:opacity-40 sm:px-5"
          >
            Checkout
          </button>
        </div>
      </div>

      <ProductSheet product={sheetProduct} open={sheetProduct != null} onClose={() => setSheetProduct(null)} cart={cart} />
    </div>
  )
}
