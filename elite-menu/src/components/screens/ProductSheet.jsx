import { motion } from 'framer-motion'
import { Check } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { isDrinkProduct } from '../../utils/menuKind'
import { getCategoryColor } from '../../utils/placeholders'
import { productIsAvailable } from '../../utils/availability'
import { formatUsd } from '../../utils/format'
import { BottomSheet } from '../ui/BottomSheet'
import { GoldDivider } from '../ui/GoldDivider'
import { QuantityControl } from '../ui/QuantityControl'

export function ProductSheet({ product, open, onClose, cart }) {
  const { t } = useTranslation()
  const [sheetQty, setSheetQty] = useState(1)
  const [flash, setFlash] = useState(false)
  const [imgErr, setImgErr] = useState(false)

  useEffect(() => {
    setImgErr(false)
  }, [product?.id])

  useEffect(() => {
    if (!product || !open) return
    const q = cart.getItemQty(product.id)
    setSheetQty(q > 0 ? q : 1)
  }, [product?.id, open])

  if (!product) return null

  const inCart = cart.getItemQty(product.id) > 0
  const minStep = inCart ? 0 : 1

  const sub = product.subcategory || t('guest.general.generalCategory')
  const ph = getCategoryColor(product.category)
  const initial = (product.name || '?').trim().charAt(0) || '?'
  const photoUrl = product.photoUrl ? String(product.photoUrl) : ''
  const showPhoto = photoUrl && !imgErr
  const available = productIsAvailable(product)

  const lineTotal = Number(product.price) * sheetQty
  const isDrink = isDrinkProduct(product)
  const descText = product.description ? String(product.description).trim() : ''
  const compText = product.composition ? String(product.composition).trim() : ''
  const descFallback = t('guest.product.descFallback')
  const compFallback = isDrink ? t('guest.product.compFallbackDrink') : t('guest.product.compFallbackFood')

  const addLine = () => {
    if (!available) return
    if (sheetQty === 0) {
      cart.removeItem(product.id)
      onClose()
      return
    }
    cart.removeItem(product.id)
    cart.addProductBatch(product, sheetQty)
    setFlash(true)
    window.setTimeout(() => {
      setFlash(false)
      onClose()
    }, 320)
  }

  return (
    <BottomSheet open={open} onClose={onClose}>
      <div className="relative">
        <div className="relative h-[260px] w-full overflow-hidden">
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
            <span className="font-display text-7xl italic text-white/15">{initial}</span>
          </div>
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-transparent from-[40%] to-[rgba(15,25,35,0.96)] to-100%" />
          <div className="absolute bottom-4 left-5 rounded bg-black/40 px-2 py-1 font-body text-[0.65rem] font-semibold uppercase tracking-[0.15em] text-gold">
            {sub}
          </div>
        </div>

        <div className="px-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-2">
          <h2 className="font-display text-2xl italic font-semibold leading-tight text-champagne">{product.name}</h2>
          <div className="mt-2 flex items-center justify-between">
            <span className="font-mono text-xl font-medium text-gold">{formatUsd(product.price)}</span>
            <div className="flex items-center gap-2">
              <span className={`h-2 w-2 rounded-full ${available ? 'bg-emerald-500' : 'bg-red-600'}`} />
              {available ? (
                <span className="font-body text-[0.78rem] font-medium text-champagne/80">{t('guest.product.available')}</span>
              ) : (
                <span
                  className="font-body text-[0.8rem] font-bold uppercase tracking-[0.06em] text-red-600"
                  style={{ color: '#dc2626' }}
                >
                  {t('guest.product.outOfOrder')}
                </span>
              )}
            </div>
          </div>

          <GoldDivider className="my-4" />

          <section className="mb-4">
            <h3 className="mb-2 font-body text-[0.7rem] font-bold uppercase tracking-[0.15em] text-gold">
              {t('guest.product.description')}
            </h3>
            <p
              className={`font-body text-[0.9rem] font-light leading-relaxed ${descText ? 'text-champagne' : 'italic text-[var(--text-muted)]'}`}
            >
              {descText || descFallback}
            </p>
          </section>

          <section className="mb-4">
            <h3 className="mb-2 font-body text-[0.7rem] font-bold uppercase tracking-[0.15em] text-gold">
              {isDrink ? t('guest.product.composition') : t('guest.product.ingredients')}
            </h3>
            {compText ? (
              <div className="flex flex-wrap gap-2">
                {String(product.composition)
                  .split(',')
                  .map((s) => s.trim())
                  .filter(Boolean)
                  .map((ing) => (
                    <span
                      key={ing}
                      className="inline-flex items-center gap-1 rounded-full border border-champagne/15 bg-champagne/[0.06] px-3 py-1 font-body text-[0.78rem] text-champagne"
                    >
                      <span className="text-gold">·</span>
                      {ing}
                    </span>
                  ))}
              </div>
            ) : (
              <p className="font-body text-[0.9rem] font-light italic leading-relaxed text-[var(--text-muted)]">{compFallback}</p>
            )}
            <p className="mt-3 font-body text-[0.75rem] italic text-[var(--text-muted)]">
              {t('guest.product.allergyHint')}
            </p>
          </section>

          <GoldDivider className="my-4" />

          <div className="flex flex-wrap items-center justify-between gap-4">
            <QuantityControl
              value={sheetQty}
              onMinus={() => setSheetQty((n) => Math.max(minStep, n - 1))}
              onPlus={() => setSheetQty((n) => Math.min(20, n + 1))}
              disablePlus={!available}
            />
            <motion.button
              type="button"
              whileTap={{ scale: 0.96 }}
              disabled={!available}
              onClick={addLine}
              className={`flex min-h-[50px] min-w-[140px] items-center justify-center rounded-xl px-4 font-body text-[0.9rem] font-extrabold uppercase tracking-[0.08em] shadow-[0_4px_16px_rgba(0,0,0,0.2)] disabled:cursor-not-allowed disabled:opacity-40 ${
                sheetQty === 0 && inCart
                  ? 'bg-red-500/20 text-red-200 ring-1 ring-red-500/30'
                  : 'bg-gold text-black shadow-[0_4px_16px_rgba(200,168,76,0.3)]'
              }`}
            >
              {flash ? (
                <Check className="h-6 w-6" strokeWidth={3} />
              ) : sheetQty === 0 && inCart ? (
                <>{t('guest.product.removeFromOrder')}</>
              ) : (
                <>{t('guest.product.addWithPrice', { price: formatUsd(lineTotal) })}</>
              )}
            </motion.button>
          </div>
        </div>
      </div>
    </BottomSheet>
  )
}
