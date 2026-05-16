import { formatUsd } from './format'

/**
 * @param {object} receipt
 * @param {string} [restaurantName]
 */
export function buildGuestReceiptHtml(receipt, restaurantName = 'Restaurant') {
  const code = String(receipt.confirmationCode ?? '').trim()
  const orderCode = String(receipt.orderCode ?? '').trim()
  const title = String(restaurantName ?? 'Restaurant').trim() || 'Restaurant'
  const lines = Array.isArray(receipt.lines) ? receipt.lines : []
  const lineRows = lines
    .map(
      (l) => `<tr>
  <td style="padding:4px 8px 4px 0;text-align:right;white-space:nowrap;">${l.quantity}</td>
  <td style="padding:4px 8px;">${escapeHtml(l.name)}</td>
  <td style="padding:4px 0;text-align:right;white-space:nowrap;">${formatUsd(l.unitPrice)}</td>
  <td style="padding:4px 0 4px 8px;text-align:right;white-space:nowrap;">${formatUsd(l.lineTotal)}</td>
</tr>`,
    )
    .join('')

  const deliveryBlock =
    receipt.fulfillment === 'Delivery'
      ? `<p style="margin:8px 0 0;font-size:13px;"><strong>Delivery</strong><br/>
${receipt.customerName ? `Customer: ${escapeHtml(receipt.customerName)}<br/>` : ''}
${receipt.phone ? `Phone: ${escapeHtml(receipt.phone)}<br/>` : ''}
${receipt.address ? `Address: ${escapeHtml(receipt.address)}<br/>` : ''}
${receipt.instructions ? `Notes: ${escapeHtml(receipt.instructions)}` : ''}</p>`
      : `<p style="margin:8px 0 0;font-size:13px;"><strong>Pickup</strong>${receipt.customerName ? `<br/>Customer: ${escapeHtml(receipt.customerName)}` : ''}</p>`

  const codeBlock = code
    ? `<div class="code">${escapeHtml(code)}</div><div class="code-label">CONFIRMATION CODE</div>`
    : ''

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Order ${escapeHtml(code || orderCode)}</title>
  <style>
    body { font-family: "Segoe UI", system-ui, sans-serif; background:#111; color:#f5f0e6; margin:0; padding:24px; }
    .ticket { max-width:380px; margin:0 auto; background:#1a1a1f; border:1px solid #c8a84c55; border-radius:16px; padding:24px; }
    .code { font-size:42px; font-weight:700; letter-spacing:0.2em; text-align:center; color:#e8c96a; margin:12px 0 4px; font-family: Consolas, monospace; }
    .code-label { text-align:center; font-size:11px; letter-spacing:0.25em; color:#a89b7a; margin-bottom:16px; }
    table { width:100%; border-collapse:collapse; font-size:13px; margin-top:12px; }
    th { text-align:left; font-size:11px; color:#a89b7a; border-bottom:1px solid #333; padding-bottom:6px; }
    .totals { margin-top:16px; font-size:13px; }
    .totals div { display:flex; justify-content:space-between; margin:4px 0; }
    .grand { font-size:18px; font-weight:700; color:#e8c96a; margin-top:8px; }
    @media print { body { background:#fff; color:#000; } .ticket { border-color:#ccc; background:#fff; } .code { color:#000; } }
  </style>
</head>
<body>
  <div class="ticket">
    <h1 style="margin:0;font-size:20px;text-align:center;">${escapeHtml(title)}</h1>
    ${codeBlock}
    <p style="text-align:center;font-size:12px;color:#a89b7a;margin:0;">${escapeHtml(receipt.placedAtLabel ?? '')}</p>
    ${orderCode ? `<p style="text-align:center;font-size:12px;margin:8px 0 0;">Order ref: ${escapeHtml(orderCode)}</p>` : ''}
    ${deliveryBlock}
    <table>
      <thead><tr>
        <th style="width:32px;text-align:right;">Qty</th>
        <th>Item</th>
        <th style="text-align:right;">P.U</th>
        <th style="text-align:right;">Total</th>
      </tr></thead>
      <tbody>${lineRows}</tbody>
    </table>
    <div class="totals">
      <div><span>Subtotal</span><span>${formatUsd(receipt.subtotal)}</span></div>
      <div><span>Tax</span><span>${formatUsd(receipt.tax)}</span></div>
      <div><span>Service</span><span>${formatUsd(receipt.service)}</span></div>
      ${receipt.deliveryFee > 0 ? `<div><span>Delivery</span><span>${formatUsd(receipt.deliveryFee)}</span></div>` : ''}
      <div class="grand"><span>Grand total</span><span>${formatUsd(receipt.grandTotal)}</span></div>
    </div>
    <p style="margin-top:20px;font-size:12px;text-align:center;color:#a89b7a;">Show this code at pickup or to the driver.</p>
  </div>
</body>
</html>`
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

/** @param {object} receipt @param {string} [restaurantName] */
export function downloadGuestReceiptHtml(receipt, restaurantName) {
  const html = buildGuestReceiptHtml(receipt, restaurantName)
  const blob = new Blob([html], { type: 'text/html;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const code = String(receipt.confirmationCode ?? 'order').trim() || 'order'
  const a = document.createElement('a')
  a.href = url
  a.download = `ticket-${code}.html`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
