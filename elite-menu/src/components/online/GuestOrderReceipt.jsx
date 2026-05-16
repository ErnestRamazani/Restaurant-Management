import { Download } from 'lucide-react'
import { formatUsd } from '../../utils/format'
import { downloadGuestReceiptPdf } from '../../utils/guestReceiptPdf'

/**
 * @param {object} props
 * @param {object} props.receipt
 * @param {string} [props.restaurantName]
 */
export function GuestOrderReceipt({ receipt, restaurantName }) {
  if (!receipt) return null

  const code = String(receipt.confirmationCode ?? '').trim()
  const lines = Array.isArray(receipt.lines) ? receipt.lines : []

  return (
    <div className="w-full max-w-[340px] rounded-2xl border border-champagne/15 bg-midnight-2 px-4 py-4 text-left shadow-[0_12px_40px_rgba(0,0,0,0.25)]">
      {code ? (
        <div className="mb-4 text-center">
          <p className="font-body text-[0.65rem] font-semibold uppercase tracking-[0.22em] text-gold/80">
            Confirmation code
          </p>
          <p className="mt-2 font-mono text-[2.25rem] font-bold leading-none tracking-[0.18em] text-gold">
            {code}
          </p>
          <p className="mt-2 font-body text-[0.68rem] text-champagne/55">
            Screenshot this code — show it at pickup or to the driver.
          </p>
        </div>
      ) : null}

      <p className="text-center font-body text-[0.7rem] text-champagne/50">{receipt.placedAtLabel}</p>
      {receipt.orderCode ? (
        <p className="mt-1 text-center font-mono text-[0.68rem] text-champagne/45">Ref: {receipt.orderCode}</p>
      ) : null}

      <p className="mt-3 font-body text-xs text-champagne/75">
        <span className="font-semibold text-gold/90">{receipt.fulfillment}</span>
        {receipt.customerName ? ` · ${receipt.customerName}` : ''}
      </p>
      {receipt.phone ? (
        <p className="mt-1 font-body text-xs text-champagne/60">Phone: {receipt.phone}</p>
      ) : null}
      {receipt.fulfillment === 'Delivery' && receipt.address ? (
        <p className="mt-1 font-body text-xs leading-snug text-champagne/60">Address: {receipt.address}</p>
      ) : null}

      <div className="mt-3 border-t border-champagne/10 pt-3">
        {lines.map((line) => (
          <div
            key={`${line.name}-${line.quantity}-${line.unitPrice}`}
            className="mb-2 flex justify-between gap-2 font-body text-xs"
          >
            <span className="text-champagne/85">
              {line.quantity}× {line.name}
            </span>
            <span className="shrink-0 font-mono text-champagne/70">{formatUsd(line.lineTotal)}</span>
          </div>
        ))}
      </div>

      <div className="mt-2 space-y-1 border-t border-champagne/10 pt-3 font-body text-xs text-champagne/65">
        <div className="flex justify-between">
          <span>Subtotal</span>
          <span className="font-mono">{formatUsd(receipt.subtotal)}</span>
        </div>
        <div className="flex justify-between">
          <span>Tax</span>
          <span className="font-mono">{formatUsd(receipt.tax)}</span>
        </div>
        <div className="flex justify-between">
          <span>Service</span>
          <span className="font-mono">{formatUsd(receipt.service)}</span>
        </div>
        {receipt.deliveryFee > 0 ? (
          <div className="flex justify-between">
            <span>Delivery</span>
            <span className="font-mono">{formatUsd(receipt.deliveryFee)}</span>
          </div>
        ) : null}
        <div className="flex justify-between pt-1 font-semibold text-gold">
          <span>Grand total</span>
          <span className="font-mono">{formatUsd(receipt.grandTotal)}</span>
        </div>
      </div>

      <button
        type="button"
        onClick={() => downloadGuestReceiptHtml(receipt, restaurantName)}
        className="mt-4 flex min-h-[40px] w-full items-center justify-center gap-2 rounded-xl border border-gold/35 bg-gold/10 font-body text-[0.72rem] font-bold uppercase tracking-[0.12em] text-gold hover:bg-gold/15"
      >
        <Download className="h-4 w-4" aria-hidden />
        Download PDF ticket
      </button>
    </div>
  )
}
