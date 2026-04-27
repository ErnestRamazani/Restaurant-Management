import { formatUsd } from '../../utils/format'
import { GoldDivider } from './GoldDivider'

export function PriceBreakdown({ subtotal, tax, service, grandTotal, taxPercent, servicePercent }) {
  return (
    <div className="rounded-[14px] border border-champagne/10 bg-midnight-2 px-4 py-4">
      <div className="flex justify-between font-body text-[0.85rem] text-[var(--text-muted)]">
        <span>Subtotal</span>
        <span className="font-mono font-medium text-champagne">{formatUsd(subtotal)}</span>
      </div>
      <div className="mt-2 flex justify-between font-body text-[0.85rem] text-[var(--text-muted)]">
        <span>Tax ({taxPercent}%)</span>
        <span className="font-mono font-medium text-champagne">{formatUsd(tax)}</span>
      </div>
      <div className="mt-2 flex justify-between font-body text-[0.85rem] text-[var(--text-muted)]">
        <span>Service ({servicePercent}%)</span>
        <span className="font-mono font-medium text-champagne">{formatUsd(service)}</span>
      </div>
      <GoldDivider className="my-2.5" />
      <div className="flex justify-between">
        <span className="font-body text-base font-bold text-champagne">Grand total</span>
        <span className="font-mono text-[1.4rem] font-bold text-gold">{formatUsd(grandTotal)}</span>
      </div>
      <p className="mt-2 font-body text-[0.72rem] italic text-[var(--text-muted)]">
        Discounts, if applicable, will be applied by your server.
      </p>
    </div>
  )
}
