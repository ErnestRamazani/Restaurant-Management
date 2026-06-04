import { AnimatePresence, motion } from 'framer-motion'
import { ChevronDown, Loader2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchTables, submitDraft } from '../../utils/api'
import { inferOrderKindFromLines } from '../../utils/menuKind'
import { formatUsd } from '../../utils/format'
import { tableServerName } from '../../utils/tables'
import { BottomSheet } from '../ui/BottomSheet'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { GoldDivider } from '../ui/GoldDivider'
import { PriceBreakdown } from '../ui/PriceBreakdown'
import { QuantityControl } from '../ui/QuantityControl'

export function CartScreen({
  cart,
  guestOrderMode = 'browse',
  tableIdFromUrl,
  hadInvalidTableParam = false,
  manualTableId,
  setManualTableId,
  onBack,
  onSuccess,
}) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [notes, setNotes] = useState('')
  const [allergyNotes, setAllergyNotes] = useState('')
  const [openNotes, setOpenNotes] = useState(false)
  const [openAllergy, setOpenAllergy] = useState(false)
  const [tables, setTables] = useState(/** @type {any[]} */ ([]))
  const [loadingTables, setLoadingTables] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [clearShake, setClearShake] = useState(0)
  const [clearDialogOpen, setClearDialogOpen] = useState(false)
  const [tablePickerOpen, setTablePickerOpen] = useState(false)

  const effectiveTable = tableIdFromUrl ?? manualTableId

  useEffect(() => {
    let cancel = false
    ;(async () => {
      setLoadingTables(true)
      try {
        const t = await fetchTables()
        if (!cancel) setTables(Array.isArray(t) ? t : [])
      } catch {
        if (!cancel) setTables([])
      } finally {
        if (!cancel) setLoadingTables(false)
      }
    })()
    return () => {
      cancel = true
    }
  }, [])

  const canSend =
    name.trim().length > 0 && cart.lines.length > 0 && effectiveTable != null && effectiveTable > 0

  const send = async () => {
    setError('')
    if (!canSend) return
    setSubmitting(true)
    try {
      const orderKind = inferOrderKindFromLines(cart.lines)
      const payload = {
        tableId: effectiveTable,
        customerName: name.trim(),
        orderKind,
        items: cart.lines.map((l) => ({
          productId: l.product.id,
          productName: String(l.product.name),
          quantity: l.quantity,
          unitPrice: Number(l.product.price),
        })),
        notes: notes.trim() || null,
        allergyNotes: allergyNotes.trim() || null,
      }
      const res = await submitDraft(payload)
      cart.clearCart()
      onSuccess(res)
    } catch (e) {
      setError(e instanceof Error ? e.message : t('guest.cart.sendFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  const clearOrder = () => {
    if (cart.lines.length === 0) {
      setClearShake((s) => s + 1)
      return
    }
    setClearDialogOpen(true)
  }

  const tableLabel = useMemo(
    () =>
      effectiveTable && tables.length
        ? tables.find((t) => Number(t.id) === effectiveTable)
        : null,
    [effectiveTable, tables]
  )

  const manualTableLabel = useMemo(
    () =>
      manualTableId && tables.length
        ? tables.find((t) => Number(t.id) === manualTableId)
        : null,
    [manualTableId, tables]
  )

  return (
    <motion.div
      className="flex min-h-[100svh] flex-col bg-midnight"
      initial={{ x: '100%' }}
      animate={{ x: 0 }}
      exit={{ x: '100%' }}
      transition={{ type: 'spring', stiffness: 300, damping: 32 }}
    >
      <header className="flex h-14 shrink-0 items-center border-b border-champagne/10 bg-midnight-2 px-2">
        <button
          type="button"
          onClick={onBack}
          className="flex h-11 min-w-[44px] items-center justify-center text-champagne"
          aria-label={t('guest.general.back')}
        >
          ←
        </button>
        <h2 className="flex-1 text-center font-display text-lg font-semibold text-champagne">
          {guestOrderMode === 'online' ? t('guest.cart.yourOnlineOrder') : t('guest.cart.yourOrder')}
        </h2>
        <div className="w-11" aria-hidden />
      </header>

      {guestOrderMode === 'online' ? (
        <div className="mx-4 mt-3 rounded-xl border border-amber-400/35 bg-amber-500/10 px-3 py-2.5">
          <p className="font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-amber-100/90">
            {t('guest.cart.pickupDeliveryTitle')}
          </p>
          <p className="mt-1 font-body text-[0.72rem] leading-snug text-amber-50/90">
            {t('guest.cart.pickupDeliveryHint')}
          </p>
        </div>
      ) : null}

      <div className="border border-gold/25 bg-[var(--gold-dim)] mx-4 mt-3 flex flex-col gap-2 rounded-[10px] px-3 py-2.5">
        <p className="font-body text-[0.7rem] font-semibold uppercase tracking-[0.12em] text-gold/90">
          {t('guest.cart.yourTable')}
        </p>
        {hadInvalidTableParam ? (
          <p className="font-body text-[0.78rem] leading-snug text-amber-200/90">
            {t('guest.cart.invalidTableParam')}
          </p>
        ) : null}
        <div className="min-w-0">
          {tableIdFromUrl ? (
            loadingTables && !tableLabel ? (
              <span className="text-sm text-[var(--text-muted)]">{t('guest.cart.resolvingTable')}</span>
            ) : (
              <div className="min-w-0">
                <span className="block font-body text-[0.88rem] font-semibold leading-snug text-champagne">
                  {tableLabel?.name
                    ? t('guest.general.tableCodeName', {
                        code: tableLabel?.tableCode ?? String(tableIdFromUrl),
                        name: tableLabel.name,
                      })
                    : t('guest.general.tableCode', {
                        code: tableLabel?.tableCode ?? String(tableIdFromUrl),
                      })}
                </span>
                {tableServerName(tableLabel) ? (
                  <span className="mt-0.5 block font-body text-[0.8rem] text-champagne/75">
                    {t('guest.general.serverLine', { name: tableServerName(tableLabel) })}
                  </span>
                ) : null}
              </div>
            )
          ) : loadingTables ? (
            <span className="text-sm text-[var(--text-muted)]">{t('guest.cart.loadingTables')}</span>
          ) : tables.length === 0 ? (
            <span className="text-sm text-red-300/80">{t('guest.cart.noTables')}</span>
          ) : (
            <button
              type="button"
              onClick={() => setTablePickerOpen(true)}
              className="flex min-h-[48px] w-full items-center justify-between gap-2 rounded-lg border border-champagne/20 bg-midnight-2 px-3 py-2.5 text-left font-body text-[0.9rem] text-champagne transition-colors active:bg-champagne/[0.06]"
            >
              <span className="min-w-0 flex-1 leading-snug">
                {manualTableId && manualTableLabel ? (
                  <span className="block w-full min-w-0">
                    <span className="block">
                      {t('guest.general.tableCodeName', {
                        code: manualTableLabel.tableCode,
                        name: manualTableLabel.name,
                      })}
                    </span>
                    {tableServerName(manualTableLabel) ? (
                      <span className="mt-0.5 block text-[0.8rem] text-champagne/75">
                        {t('guest.general.serverLine', { name: tableServerName(manualTableLabel) })}
                      </span>
                    ) : null}
                  </span>
                ) : (
                  t('guest.cart.chooseTable')
                )}
              </span>
              <ChevronDown className="h-5 w-5 shrink-0 text-gold/70" strokeWidth={2.5} aria-hidden />
            </button>
          )}
        </div>
      </div>

      <div className="mt-2 px-4">
        <label className="mb-1.5 block font-body text-[0.72rem] font-semibold uppercase tracking-[0.12em] text-gold">
          {t('guest.cart.yourName')}
        </label>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('guest.cart.namePlaceholder')}
          maxLength={60}
          className="h-[46px] w-full rounded-[10px] border border-champagne/15 bg-champagne/[0.06] px-3.5 font-body text-[0.95rem] text-champagne placeholder:text-champagne/35 focus:border-gold/50 focus:outline-none"
        />
      </div>

      <div className="mt-4 flex-1 overflow-y-auto px-4">
        <p className="mb-2.5 font-body text-[0.78rem] text-[var(--text-muted)]">
          {t('guest.cart.itemsInOrder', { count: cart.totalItems })}
        </p>
        <AnimatePresence initial={false}>
          {cart.lines.map((l) => (
            <motion.div
              key={l.product.id}
              layout
              initial={{ opacity: 0, x: -24 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: '-100%' }}
              className="mb-2 rounded-xl border border-champagne/10 border-l-[3px] border-l-gold bg-midnight-3 p-3.5"
            >
              <div className="flex justify-between gap-2">
                <h3 className="line-clamp-2 max-w-[65%] font-display text-[0.95rem] font-semibold text-champagne">
                  {l.product.name}
                </h3>
                <span className="shrink-0 font-mono text-base font-medium text-gold">
                  {formatUsd(Number(l.product.price) * l.quantity)}
                </span>
              </div>
              <p className="mt-1 font-body text-[0.78rem] text-[var(--text-muted)]">
                {formatUsd(l.product.price)} {t('guest.general.each')}
              </p>
              <div className="mt-2.5 flex items-center justify-between">
                <QuantityControl
                  variant="compact"
                  value={l.quantity}
                  onMinus={() => cart.adjustLineQuantity(l.product.id, -1)}
                  onPlus={() => cart.adjustLineQuantity(l.product.id, 1)}
                />
                <motion.button
                  type="button"
                  whileTap={{ scale: 0.9 }}
                  onClick={() => cart.removeItem(l.product.id)}
                  className="flex h-7 w-7 items-center justify-center rounded-full border border-red-500/20 bg-red-500/10 text-red-500"
                  aria-label={t('guest.general.remove')}
                >
                  ×
                </motion.button>
              </div>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>

      <div className="mt-2 space-y-2 px-4">
        <button
          type="button"
          onClick={() => setOpenNotes(!openNotes)}
          className="flex w-full items-center justify-between py-2 font-body text-sm text-champagne/70"
        >
          <span>{openNotes ? t('guest.cart.noteOpen') : t('guest.cart.noteClosed')}</span>
        </button>
        {openNotes ? (
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            placeholder={t('guest.cart.notePlaceholder')}
            className="w-full rounded-[10px] border border-champagne/15 bg-champagne/[0.06] p-3 font-body text-champagne"
          />
        ) : null}
        <button
          type="button"
          onClick={() => setOpenAllergy(!openAllergy)}
          className="flex w-full items-center justify-between py-2 font-body text-sm text-champagne/70"
        >
          <span>{openAllergy ? t('guest.cart.allergyOpen') : t('guest.cart.allergyClosed')}</span>
        </button>
        {openAllergy ? (
          <>
            <textarea
              value={allergyNotes}
              onChange={(e) => setAllergyNotes(e.target.value)}
              rows={2}
              placeholder={t('guest.cart.allergyPlaceholder')}
              className="w-full rounded-[10px] border border-champagne/15 bg-champagne/[0.06] p-3 font-body text-champagne"
            />
            <p className="text-[0.72rem] text-red-400/70">{t('guest.cart.allergyWarning')}</p>
          </>
        ) : null}
      </div>

      {cart.totalItems > 0 && cart.estimatedPrepMinutes > 0 ? (
        <div className="mx-4 mt-4 rounded-[10px] border border-gold/30 bg-gold/10 px-3.5 py-3">
          <p className="font-body text-[0.65rem] font-semibold uppercase tracking-[0.14em] text-gold/90">
            {t('guest.cart.estPrepTitle')}
          </p>
          <p className="mt-1 font-body text-[0.95rem] font-semibold text-champagne">
            {t('guest.cart.estPrepMinutes', { minutes: cart.estimatedPrepMinutes })}
          </p>
          <p className="mt-1 font-body text-[0.7rem] leading-snug text-[var(--text-muted)]">
            {t('guest.cart.estPrepDisclaimer')}
          </p>
        </div>
      ) : null}

      <div className="mt-4 px-4">
        <PriceBreakdown
          subtotal={cart.subtotal}
          tax={cart.tax}
          service={cart.service}
          grandTotal={cart.grandTotal}
          taxPercent={cart.taxPercent}
          servicePercent={cart.servicePercent}
        />
      </div>

      {error ? (
        <div className="mx-4 mt-3 rounded-[10px] border border-red-500/25 bg-red-500/10 px-3.5 py-2.5 font-body text-[0.85rem] text-red-200">
          {error}
        </div>
      ) : null}

      <div className="sticky bottom-0 z-20 mt-auto border-t border-champagne/10 bg-midnight/95 px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3 backdrop-blur-sm">
        <div className="mb-2.5 flex flex-col gap-2.5">
          <motion.button
            key={clearShake}
            type="button"
            onClick={clearOrder}
            animate={clearShake > 0 ? { x: [0, -5, 5, -5, 5, 0] } : { x: 0 }}
            transition={{ duration: 0.4 }}
            className="min-h-[48px] w-full rounded-xl border-2 border-red-500/60 bg-red-500/[0.12] px-4 font-body text-[0.9rem] font-bold uppercase tracking-[0.1em] text-red-300 shadow-[0_0_20px_rgba(239,68,68,0.12)] transition-colors active:bg-red-500/20"
          >
            {t('guest.cart.clearOrder')}
          </motion.button>
          <button
            type="button"
            disabled={!canSend || submitting}
            onClick={send}
            className="flex h-14 w-full items-center justify-center gap-2 rounded-[14px] bg-gradient-to-br from-gold to-[#A87820] font-body text-base font-extrabold uppercase tracking-[0.08em] text-black shadow-[0_6px_24px_rgba(200,168,76,0.3)] disabled:cursor-not-allowed disabled:bg-champagne/10 disabled:text-[var(--text-muted)] disabled:shadow-none"
          >
            {submitting ? (
              <>
                <Loader2 className="h-5 w-5 animate-spin" />
                {t('guest.cart.sending')}
              </>
            ) : (
              t('guest.cart.sendToServer')
            )}
          </button>
        </div>
      </div>

      <ConfirmDialog
        open={clearDialogOpen}
        title={t('guest.cart.clearTitle')}
        confirmLabel={t('guest.cart.clearConfirm')}
        cancelLabel={t('guest.cart.clearCancel')}
        danger
        onConfirm={() => {
          cart.clearCart()
          setClearDialogOpen(false)
        }}
        onCancel={() => setClearDialogOpen(false)}
      >
        {t('guest.cart.clearBody')}
      </ConfirmDialog>

      <BottomSheet open={tablePickerOpen} onClose={() => setTablePickerOpen(false)}>
        <div className="px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-0">
          <h2 className="font-display text-2xl italic text-champagne">{t('guest.cart.selectTableTitle')}</h2>
          <GoldDivider className="my-3" />
          <p className="mb-3 font-body text-[0.8rem] text-[var(--text-muted)]">
            {t('guest.cart.selectTableHint')}
          </p>
          <ul className="space-y-1 pb-2" role="listbox">
            {tables.map((table) => {
              const id = Number(table.id)
              const selected = manualTableId === id
              return (
                <li key={id}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={selected}
                    onClick={() => {
                      setManualTableId(id)
                      setTablePickerOpen(false)
                    }}
                    className={`flex min-h-[52px] w-full flex-col items-start justify-center gap-0.5 rounded-xl border px-4 py-2.5 text-left font-body text-[0.9rem] transition-colors ${
                      selected
                        ? 'border-gold/50 bg-gold/10 text-gold'
                        : 'border-champagne/10 bg-champagne/[0.04] text-champagne active:bg-champagne/[0.08]'
                    }`}
                  >
                    <span>
                      <span className="font-mono text-[0.85rem] font-semibold text-gold/90">T{table.tableCode}</span>
                      <span className="text-champagne/90"> — {table.name}</span>
                    </span>
                    {tableServerName(table) ? (
                      <span className="text-[0.78rem] text-champagne/60">
                        {t('guest.general.serverLine', { name: tableServerName(table) })}
                      </span>
                    ) : null}
                  </button>
                </li>
              )
            })}
          </ul>
          <button
            type="button"
            onClick={() => setTablePickerOpen(false)}
            className="mt-4 w-full min-h-[48px] rounded-xl border border-champagne/20 font-body text-sm font-semibold text-champagne/80"
          >
            {t('common.close')}
          </button>
        </div>
      </BottomSheet>
    </motion.div>
  )
}
