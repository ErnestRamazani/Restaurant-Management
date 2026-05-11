import { AnimatePresence, motion } from 'framer-motion'
import { ArrowLeft, Loader2, ShoppingBag, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMenu } from '../../hooks/useMenu'
import { useOnlineOrderCart } from '../../hooks/useOnlineOrderCart'
import { submitOnlineOrder } from '../../utils/api'
import { resolveApiAssetUrl } from '../../utils/apiClient'
import { formatUsd } from '../../utils/format'
import { computeTotalsWithDelivery } from '../../utils/totals'
import { ConfirmScreen } from '../screens/ConfirmScreen'
import { ProductSheet } from '../screens/ProductSheet'
import { BottomSheet } from '../ui/BottomSheet'
import { ErrorScreen } from '../ui/ErrorScreen'
import { GoldDivider } from '../ui/GoldDivider'
import { LoadingScreen } from '../ui/LoadingScreen'
import { ProductCard } from '../ui/ProductCard'
import { QuantityControl } from '../ui/QuantityControl'

const spring = { type: 'spring', stiffness: 300, damping: 34 }

function OnlineOrderCheckoutSheet({ open, onClose, cart, onSuccess }) {
  const [name, setName] = useState('')
  const [fulfillment, setFulfillment] = useState(/** @type {'Pickup' | 'Delivery'} */ ('Pickup'))
  const [address, setAddress] = useState('')
  const [instructions, setInstructions] = useState('')
  const [notes, setNotes] = useState('')
  const [allergyNotes, setAllergyNotes] = useState('')
  const [paymentMethod, setPaymentMethod] = useState('Cash')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const merch = cart.subtotal
  const deliveryFee = fulfillment === 'Delivery' ? Math.round(merch * 0.2 * 100) / 100 : 0
  const totals = useMemo(
    () => computeTotalsWithDelivery(merch, cart.taxPercent, cart.servicePercent, deliveryFee),
    [merch, cart.taxPercent, cart.servicePercent, deliveryFee],
  )

  const canSend =
    name.trim().length > 0 &&
    cart.lines.length > 0 &&
    (fulfillment !== 'Delivery' || address.trim().length >= 5)

  const send = async () => {
    setErr('')
    if (!canSend) return
    setBusy(true)
    try {
      const payload = {
        customerName: name.trim(),
        fulfillmentMode: fulfillment,
        deliveryAddress: fulfillment === 'Delivery' ? address.trim() : null,
        deliveryInstructions: fulfillment === 'Delivery' && instructions.trim() ? instructions.trim() : null,
        paymentMethod,
        paymentTiming: 'Deferred',
        notes: notes.trim() || null,
        allergyNotes: allergyNotes.trim() || null,
        items: cart.lines.map((l) => ({
          productId: l.product.id,
          quantity: l.quantity,
          unitPrice: Number(l.product.price),
        })),
      }
      const res = await submitOnlineOrder(payload)
      cart.clearCart()
      onSuccess({
        label: `Online · ${fulfillment}`,
        message: `Order ${res?.orderCode ?? res?.OrderCode ?? ''} received. We'll confirm payment and prep time.`,
        orderCode: res?.orderCode ?? res?.OrderCode ?? '',
      })
      onClose()
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Could not place order')
    } finally {
      setBusy(false)
    }
  }

  return (
    <BottomSheet open={open} onClose={onClose}>
      <div className="px-1 pb-2">
        <p className="text-center font-body text-[0.66rem] font-bold uppercase tracking-[0.24em] text-gold/80">
          Checkout
        </p>
        <h2 className="mt-2 text-center font-display text-xl italic text-champagne">Almost there</h2>

        {err ? (
          <p className="mt-3 rounded-xl border border-red-500/25 bg-red-500/10 px-3 py-2 text-center font-body text-xs text-red-200">
            {err}
          </p>
        ) : null}

        <label className="mt-4 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          Your name
        </label>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={60}
          className="mt-2 h-11 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />

        <p className="mt-4 font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          Fulfillment
        </p>
        <div className="mt-2 flex gap-2">
          {['Pickup', 'Delivery'].map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setFulfillment(/** @type {'Pickup' | 'Delivery'} */ (m))}
              className={`flex-1 rounded-xl border py-2.5 font-body text-xs font-bold uppercase tracking-wider ${
                fulfillment === m
                  ? 'border-gold/50 bg-gold/15 text-gold'
                  : 'border-champagne/15 text-champagne/60'
              }`}
            >
              {m}
            </button>
          ))}
        </div>

        {fulfillment === 'Delivery' ? (
          <>
            <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
              Delivery address
            </label>
            <textarea
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              rows={3}
              maxLength={500}
              className="mt-2 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 py-2 font-body text-sm text-champagne outline-none focus:border-gold/50"
            />
            <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
              Instructions (optional)
            </label>
            <input
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
              className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
            />
          </>
        ) : null}

        <p className="mt-4 font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          Pay with (intent)
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {['Cash', 'Card', 'MobileMoney'].map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setPaymentMethod(m)}
              className={`rounded-full border px-3 py-1.5 font-body text-[0.68rem] font-bold uppercase tracking-wider ${
                paymentMethod === m
                  ? 'border-gold/50 bg-gold/15 text-gold'
                  : 'border-champagne/15 text-champagne/55'
              }`}
            >
              {m === 'MobileMoney' ? 'Mobile money' : m}
            </button>
          ))}
        </div>

        <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
          Order notes (optional)
        </label>
        <input
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />
        <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
          Allergies (optional)
        </label>
        <input
          value={allergyNotes}
          onChange={(e) => setAllergyNotes(e.target.value)}
          className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />

        {cart.lines.length > 0 ? (
          <div className="mt-5">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">Your order</p>
                <p className="mt-1 font-body text-[0.7rem] text-champagne/50">
                  {cart.totalItems} item{cart.totalItems === 1 ? '' : 's'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => cart.clearCart()}
                className="shrink-0 rounded-xl border border-champagne/20 px-2.5 py-1.5 font-body text-[0.65rem] font-bold uppercase tracking-[0.12em] text-champagne/70 transition hover:border-red-500/35 hover:bg-red-500/10 hover:text-red-200"
              >
                Clear cart
              </button>
            </div>
            <ul className="mt-2 max-h-[min(40vh,14rem)] space-y-2 overflow-y-auto overscroll-contain pr-0.5 [-webkit-overflow-scrolling:touch]">
              {cart.lines.map((l) => {
                const unit = Number(l.product.price)
                const lineTotal = unit * l.quantity
                const pid = l.product.id
                return (
                  <li
                    key={String(pid)}
                    className="rounded-xl border border-champagne/10 border-l-[3px] border-l-gold bg-midnight-3 px-3 py-2.5"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="line-clamp-2 min-w-0 flex-1 font-display text-[0.88rem] font-semibold leading-snug text-champagne">
                        {String(l.product.name ?? '')}
                      </h3>
                      <span className="shrink-0 font-mono text-sm font-medium text-gold">{formatUsd(lineTotal)}</span>
                    </div>
                    <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                      <p className="font-body text-[0.72rem] text-champagne/55">
                        <span className="tabular-nums">{formatUsd(unit)}</span> each
                      </p>
                      <div className="flex items-center gap-1.5">
                        <QuantityControl
                          variant="compact"
                          value={l.quantity}
                          onMinus={() => cart.adjustLineQuantity(pid, -1)}
                          onPlus={() => cart.adjustLineQuantity(pid, 1)}
                          disablePlus={l.quantity >= 20}
                        />
                        <button
                          type="button"
                          onClick={() => cart.removeItem(pid)}
                          className="inline-flex h-[32px] w-[32px] shrink-0 touch-manipulation items-center justify-center rounded-full border border-champagne/20 bg-midnight-2 text-champagne/70 transition hover:border-red-500/40 hover:bg-red-500/10 hover:text-red-300 active:scale-95"
                          aria-label={`Remove ${String(l.product.name ?? 'item')} from cart`}
                        >
                          <Trash2 className="h-3.5 w-3.5" strokeWidth={2.25} />
                        </button>
                      </div>
                    </div>
                  </li>
                )
              })}
            </ul>
          </div>
        ) : null}

        <GoldDivider className="my-4" />
        <div className="space-y-1.5 font-body text-sm text-champagne/80">
          <div className="flex justify-between">
            <span>Subtotal</span>
            <span className="font-mono text-gold">{formatUsd(totals.subtotal)}</span>
          </div>
          <div className="flex justify-between">
            <span>Tax</span>
            <span className="font-mono">{formatUsd(totals.tax)}</span>
          </div>
          <div className="flex justify-between">
            <span>Service</span>
            <span className="font-mono">{formatUsd(totals.service)}</span>
          </div>
          <div className="flex justify-between">
            <span>Delivery fee</span>
            <span className="font-mono">{formatUsd(totals.deliveryFee)}</span>
          </div>
          <div className="flex justify-between border-t border-champagne/10 pt-2 font-semibold text-champagne">
            <span>Total</span>
            <span className="font-mono text-gold">{formatUsd(totals.grand)}</span>
          </div>
        </div>

        <button
          type="button"
          disabled={!canSend || busy}
          onClick={() => void send()}
          className="mt-5 flex h-12 w-full items-center justify-center gap-2 rounded-2xl bg-gold font-body text-sm font-extrabold uppercase tracking-[0.1em] text-black transition hover:brightness-105 disabled:opacity-50"
        >
          {busy ? <Loader2 className="h-5 w-5 animate-spin" /> : null}
          {busy ? 'Sending…' : 'Place order'}
        </button>
      </div>
    </BottomSheet>
  )
}

export function OnlineOrderApp() {
  const navigate = useNavigate()
  const { config, products, loading, error, refetch } = useMenu()
  const cart = useOnlineOrderCart(config)
  const [sheetProduct, setSheetProduct] = useState(/** @type {Record<string, unknown> | null} */ (null))
  const [checkoutOpen, setCheckoutOpen] = useState(false)
  const [confirm, setConfirm] = useState(
    /** @type {null | { label: string; message: string; estimatedPrepMinutes: number | null }} */ (null),
  )

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

  if (loading) return <LoadingScreen />
  if (error) return <ErrorScreen message={error} onRetry={refetch} />
  if (!config) return <ErrorScreen message="Missing menu configuration." onRetry={refetch} />

  if (confirm) {
    return (
      <ConfirmScreen
        label={confirm.label}
        message={confirm.message}
        estimatedPrepMinutes={confirm.estimatedPrepMinutes}
        onOrderMore={() => {
          setConfirm(null)
        }}
        onBackToStart={() => navigate('/')}
      />
    )
  }

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
        <div className="w-11" />
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
            onClick={() => setCheckoutOpen(true)}
            className="h-12 shrink-0 rounded-2xl bg-gold px-4 font-body text-xs font-extrabold uppercase tracking-[0.12em] text-black disabled:opacity-40 sm:px-5"
          >
            Checkout
          </button>
        </div>
      </div>

      <ProductSheet product={sheetProduct} open={sheetProduct != null} onClose={() => setSheetProduct(null)} cart={cart} />

      <OnlineOrderCheckoutSheet
        open={checkoutOpen}
        onClose={() => setCheckoutOpen(false)}
        cart={cart}
        onSuccess={(res) => {
          const n =
            cart.estimatedPrepMinutes > 0 ? Math.round(Number(cart.estimatedPrepMinutes)) : null
          setConfirm({
            label: res.label,
            message: res.message,
            estimatedPrepMinutes: Number.isFinite(n) && n > 0 ? n : null,
          })
        }}
      />
    </div>
  )
}
