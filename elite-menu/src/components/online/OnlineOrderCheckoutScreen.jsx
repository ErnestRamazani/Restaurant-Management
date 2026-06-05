import { motion } from 'framer-motion'
import { ArrowLeft, Loader2, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { submitOnlineOrder } from '../../utils/api'
import { formatUsd } from '../../utils/format'
import { formatRestaurantDateTime } from '../../utils/restaurantDateTime'
import { computeTotalsWithDelivery } from '../../utils/totals'
import { GoldDivider } from '../ui/GoldDivider'
import { QuantityControl } from '../ui/QuantityControl'

/** @param {Record<string, unknown>} product */
function resolveProductId(product) {
  const raw = product?.id ?? product?.Id
  const n = Number(raw)
  return Number.isFinite(n) && n > 0 ? n : 0
}

/** @param {{ name: string; fulfillment: string; phone: string; address: string; cart: { lines: unknown[] } }} fields */
function getCheckoutValidationMessage(t, { name, fulfillment, phone, address, cart }) {
  if (!cart.lines.length) return t('guest.online.validationEmptyCart')
  if (!name.trim()) return t('guest.online.validationName')
  if (phone.trim().length < 5) return t('guest.online.validationPhone')
  if (fulfillment === 'Delivery' && address.trim().length < 5) {
    return t('guest.online.validationAddress')
  }
  const missingId = cart.lines.some((/** @type {{ product: Record<string, unknown> }} */ l) =>
    resolveProductId(l.product) <= 0,
  )
  if (missingId) return t('guest.online.validationProductIds')
  return ''
}

export function OnlineOrderCheckoutScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { cart, completeOrder, config } = /** @type {import('./OnlineOrderLayout').OnlineOrderOutletContext} */ (
    useOutletContext()
  )

  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [fulfillment, setFulfillment] = useState(/** @type {'Pickup' | 'Delivery'} */ ('Pickup'))
  const [address, setAddress] = useState('')
  const [instructions, setInstructions] = useState('')
  const [notes, setNotes] = useState('')
  const [allergyNotes, setAllergyNotes] = useState('')
  const [paymentMethod, setPaymentMethod] = useState('Cash')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')
  const [attemptedSubmit, setAttemptedSubmit] = useState(false)

  useEffect(() => {
    if (cart.lines.length === 0) {
      navigate('/order-online', { replace: true })
    }
  }, [cart.lines.length, navigate])

  useEffect(() => {
    window.scrollTo(0, 0)
    document.documentElement.scrollTop = 0
    document.body.scrollTop = 0
  }, [])

  const merch = cart.subtotal
  const deliveryFee = fulfillment === 'Delivery' ? Math.round(merch * 0.2 * 100) / 100 : 0
  const totals = useMemo(
    () => computeTotalsWithDelivery(merch, cart.taxPercent, cart.servicePercent, deliveryFee),
    [merch, cart.taxPercent, cart.servicePercent, deliveryFee],
  )

  const validationMessage = getCheckoutValidationMessage(t, {
    name,
    fulfillment,
    phone,
    address,
    cart,
  })
  const canSend = validationMessage.length === 0

  const send = async () => {
    setErr('')
    setAttemptedSubmit(true)
    if (!canSend) {
      setErr(validationMessage || t('guest.online.completeFields'))
      document.getElementById('online-checkout-error')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
      return
    }
    setBusy(true)
    try {
      const payload = {
        customerName: name.trim(),
        fulfillmentMode: fulfillment,
        customerPhone: phone.trim(),
        deliveryAddress: fulfillment === 'Delivery' ? address.trim() : null,
        deliveryInstructions: fulfillment === 'Delivery' && instructions.trim() ? instructions.trim() : null,
        paymentMethod,
        paymentTiming: 'Deferred',
        notes: notes.trim() || null,
        allergyNotes: allergyNotes.trim() || null,
        items: cart.lines.map((l) => ({
          productId: resolveProductId(l.product),
          quantity: l.quantity,
          unitPrice: Number(l.product.price ?? l.product.Price ?? 0),
        })),
      }
      const res = await submitOnlineOrder(payload)
      const orderCode = res?.orderCode ?? res?.OrderCode ?? ''
      const confirmationCode = res?.confirmationCode ?? res?.ConfirmationCode ?? ''
      const placedAt = new Date()
      const receipt = {
        confirmationCode,
        orderCode,
        fulfillment,
        customerName: name.trim(),
        phone: phone.trim(),
        address: fulfillment === 'Delivery' ? address.trim() : '',
        instructions: fulfillment === 'Delivery' ? instructions.trim() : '',
        paymentMethod,
        placedAtLabel: formatRestaurantDateTime(placedAt, config, {
          dateStyle: 'medium',
          timeStyle: 'short',
        }),
        lines: cart.lines.map((l) => {
          const unitPrice = Number(l.product.price ?? l.product.Price ?? 0)
          return {
            quantity: l.quantity,
            name: String(l.product.name ?? l.product.Name ?? t('guest.online.genericItem')),
            unitPrice,
            lineTotal: unitPrice * l.quantity,
          }
        }),
        subtotal: totals.subtotal,
        tax: totals.tax,
        service: totals.service,
        deliveryFee: totals.deliveryFee,
        grandTotal: totals.grand,
      }
      cart.clearCart()
      completeOrder({
        label:
          fulfillment === 'Delivery' ? t('guest.online.labelDelivery') : t('guest.online.labelPickup'),
        message: confirmationCode
          ? t('guest.online.receivedWithCode')
          : t('guest.online.receivedWithOrder', { orderCode }),
        orderCode,
        confirmationCode,
        receipt,
      })
      navigate('/order-online', { replace: true })
    } catch (e) {
      setErr(e instanceof Error ? e.message : t('guest.online.placeFailed'))
      document.getElementById('online-checkout-error')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    } finally {
      setBusy(false)
    }
  }

  if (cart.lines.length === 0) {
    return null
  }

  return (
    <motion.div
      className="flex h-[100dvh] max-h-[100dvh] flex-col overflow-hidden bg-midnight text-champagne"
      initial={{ opacity: 0, x: 24 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: 24 }}
      transition={{ type: 'spring', stiffness: 300, damping: 32 }}
    >
      <header className="z-20 flex h-14 shrink-0 items-center gap-2 border-b border-champagne/10 bg-midnight/95 px-3 backdrop-blur-md">
        <button
          type="button"
          onClick={() => navigate('/order-online')}
          className="flex h-11 min-w-[44px] items-center justify-center rounded-xl text-champagne"
          aria-label={t('guest.online.backToMenuAria')}
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <motion.div className="min-w-0 flex-1 text-center" layout>
          <p className="font-body text-[0.62rem] font-bold uppercase tracking-[0.22em] text-gold/80">
            {t('guest.online.checkout')}
          </p>
          <h1 className="truncate font-display text-lg font-semibold italic">{t('guest.online.almostThere')}</h1>
        </motion.div>
        <div className="w-11" aria-hidden />
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto overscroll-y-contain px-4 pb-4 pt-4 [-webkit-overflow-scrolling:touch]">
        <label className="block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          {t('guest.online.yourName')}
        </label>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={60}
          autoComplete="name"
          className="mt-2 h-11 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />

        <p className="mt-4 font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          {t('guest.online.fulfillment')}
        </p>
        <motion.div className="mt-2 flex gap-2" layout>
          {(['Pickup', 'Delivery']).map((m) => (
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
              {m === 'Delivery' ? t('guest.online.delivery') : t('guest.online.pickup')}
            </button>
          ))}
        </motion.div>

        <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          {t('guest.online.phoneRequired')}
        </label>
        <input
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          inputMode="tel"
          maxLength={40}
          autoComplete="tel"
          placeholder={t('guest.online.phonePlaceholder')}
          className="mt-2 h-11 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />
        {fulfillment === 'Delivery' ? (
          <>
            <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
              {t('guest.online.address')}
            </label>
            <textarea
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              rows={3}
              maxLength={500}
              autoComplete="street-address"
              className="mt-2 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 py-2 font-body text-sm text-champagne outline-none focus:border-gold/50"
            />
            <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
              {t('guest.online.instructionsOptional')}
            </label>
            <input
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
              className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
            />
          </>
        ) : null}

        <p className="mt-4 font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          {t('guest.online.payWithIntent')}
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
              {m === 'MobileMoney'
                ? t('guest.online.mobileMoney')
                : m === 'Card'
                  ? t('guest.online.card')
                  : t('guest.online.cash')}
            </button>
          ))}
        </div>

        <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
          {t('guest.online.orderNotesOptional')}
        </label>
        <input
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />
        <label className="mt-3 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-champagne/50">
          {t('guest.online.allergiesOptional')}
        </label>
        <input
          value={allergyNotes}
          onChange={(e) => setAllergyNotes(e.target.value)}
          className="mt-2 h-10 w-full rounded-xl border border-champagne/15 bg-champagne/[0.06] px-3 font-body text-sm text-champagne outline-none focus:border-gold/50"
        />

        <div className="mt-5">
          <motion.div className="flex items-start justify-between gap-2" layout>
            <motion.div className="min-w-0">
              <p className="font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
                {t('guest.online.yourOrder')}
              </p>
              <p className="mt-1 font-body text-[0.7rem] text-champagne/50">
                {t('guest.cart.itemsInOrder', { count: cart.totalItems })}
              </p>
            </motion.div>
            <button
              type="button"
              onClick={() => cart.clearCart()}
              className="shrink-0 rounded-xl border border-champagne/20 px-2.5 py-1.5 font-body text-[0.65rem] font-bold uppercase tracking-[0.12em] text-champagne/70 transition hover:border-red-500/35 hover:bg-red-500/10 hover:text-red-200"
            >
              {t('guest.online.clearCart')}
            </button>
          </motion.div>
          <ul className="mt-2 space-y-2">
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
                      <span className="tabular-nums">{formatUsd(unit)}</span> {t('guest.general.each')}
                    </p>
                    <motion.div className="flex items-center gap-1.5" layout>
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
                        aria-label={t('guest.online.removeFromCartAria', {
                          name: String(l.product.name ?? t('guest.online.genericItem')),
                        })}
                      >
                        <Trash2 className="h-3.5 w-3.5" strokeWidth={2.25} />
                      </button>
                    </motion.div>
                  </div>
                </li>
              )
            })}
          </ul>
        </div>

        <GoldDivider className="my-4" />
        <div className="space-y-1.5 font-body text-sm text-champagne/80">
          <motion.div className="flex justify-between" layout>
            <span>{t('guest.pricing.subtotal')}</span>
            <span className="font-mono text-gold">{formatUsd(totals.subtotal)}</span>
          </motion.div>
          <div className="flex justify-between">
            <span>{t('guest.pricing.tax', { percent: cart.taxPercent })}</span>
            <span className="font-mono">{formatUsd(totals.tax)}</span>
          </div>
          <div className="flex justify-between">
            <span>{t('guest.pricing.service', { percent: cart.servicePercent })}</span>
            <span className="font-mono">{formatUsd(totals.service)}</span>
          </div>
          <div className="flex justify-between">
            <span>{t('guest.online.deliveryFeeLine')}</span>
            <span className="font-mono">{formatUsd(totals.deliveryFee)}</span>
          </div>
          <div className="flex justify-between border-t border-champagne/10 pt-2 font-semibold text-champagne">
            <span>{t('guest.pricing.grandTotal')}</span>
            <span className="font-mono text-gold">{formatUsd(totals.grand)}</span>
          </div>
        </div>

        {(err || (attemptedSubmit && !canSend && validationMessage)) && !busy ? (
          <motion.div
            id="online-checkout-error"
            className="mt-4 rounded-xl border border-red-500/40 bg-red-500/15 px-3 py-3 text-center font-body text-sm leading-snug text-red-100"
            role="alert"
            layout
          >
            {err || validationMessage}
          </motion.div>
        ) : null}
      </div>

      <div className="z-20 shrink-0 border-t border-champagne/10 bg-midnight-2/95 px-4 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-3 backdrop-blur-md">
        <button
          type="button"
          disabled={busy || cart.lines.length === 0}
          onClick={() => void send()}
          className="flex h-12 w-full items-center justify-center gap-2 rounded-2xl bg-gold font-body text-sm font-extrabold uppercase tracking-[0.1em] text-black transition hover:brightness-105 disabled:opacity-50"
        >
          {busy ? <Loader2 className="h-5 w-5 animate-spin" /> : null}
          {busy ? t('guest.online.sending') : t('guest.online.placeOrder')}
        </button>
      </div>
    </motion.div>
  )
}
