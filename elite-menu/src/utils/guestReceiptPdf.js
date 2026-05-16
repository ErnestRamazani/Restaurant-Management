import { formatUsd } from './format'

const PAGE_W = 300
const MARGIN = 18
const CONTENT_W = PAGE_W - MARGIN * 2

/** Brand palette (print-friendly warm ticket). */
const C = {
  page: [247, 244, 238],
  header: [15, 25, 35],
  headerText: [237, 232, 220],
  gold: [201, 168, 76],
  goldDark: [140, 110, 40],
  text: [26, 35, 48],
  muted: [108, 102, 94],
  rule: [210, 200, 184],
  panel: [255, 252, 246],
  panelBorder: [201, 168, 76],
  rowAlt: [240, 236, 228],
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {[number, number, number]} rgb
 */
function setFill(doc, rgb) {
  doc.setFillColor(rgb[0], rgb[1], rgb[2])
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {[number, number, number]} rgb
 */
function setStroke(doc, rgb) {
  doc.setDrawColor(rgb[0], rgb[1], rgb[2])
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {[number, number, number]} rgb
 */
function setText(doc, rgb) {
  doc.setTextColor(rgb[0], rgb[1], rgb[2])
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {number} y
 * @param {number} [inset]
 */
function drawGoldRule(doc, y, inset = 0) {
  setStroke(doc, C.gold)
  doc.setLineWidth(0.8)
  doc.line(MARGIN + inset, y, PAGE_W - MARGIN - inset, y)
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {number} y1
 * @param {number} y2
 */
function drawMutedRule(doc, y1, y2) {
  setStroke(doc, C.rule)
  doc.setLineWidth(0.35)
  doc.line(MARGIN, y1, PAGE_W - MARGIN, y2 ?? y1)
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {number} y
 * @param {string} text
 * @param {{ size?: number; style?: 'normal' | 'bold' | 'italic'; color?: [number, number, number]; align?: 'left' | 'center' | 'right'; maxW?: number }} [opts]
 * @returns {number}
 */
function writeText(doc, y, text, opts = {}) {
  const size = opts.size ?? 9
  const style = opts.style ?? 'normal'
  const align = opts.align ?? 'left'
  const maxW = opts.maxW ?? CONTENT_W
  const color = opts.color ?? C.text

  doc.setFontSize(size)
  doc.setFont('helvetica', style)
  setText(doc, color)

  const x = align === 'center' ? PAGE_W / 2 : align === 'right' ? PAGE_W - MARGIN : MARGIN
  const wrapped = doc.splitTextToSize(String(text ?? ''), maxW)
  doc.text(wrapped, x, y, { align })
  return y + wrapped.length * (size * 1.22) + (opts.tight ? 2 : 4)
}

/**
 * @param {import('jspdf').jsPDF} doc
 * @param {number} y
 * @param {string} left
 * @param {string} right
 * @param {{ size?: number; bold?: boolean; color?: [number, number, number] }} [opts]
 * @returns {number}
 */
function writePair(doc, y, left, right, opts = {}) {
  const size = opts.size ?? 8.5
  const style = opts.bold ? 'bold' : 'normal'
  const color = opts.color ?? C.text
  doc.setFontSize(size)
  doc.setFont('helvetica', style)
  setText(doc, color)
  doc.text(left, MARGIN, y)
  doc.text(right, PAGE_W - MARGIN, y, { align: 'right' })
  return y + size * 1.35 + 2
}

/**
 * @param {object} receipt
 * @param {string} [restaurantName]
 */
export async function downloadGuestReceiptPdf(receipt, restaurantName = 'Restaurant') {
  const { jsPDF } = await import('jspdf')
  const code = String(receipt.confirmationCode ?? '').trim()
  const orderCode = String(receipt.orderCode ?? '').trim()
  const title = String(restaurantName ?? 'Restaurant').trim() || 'Restaurant'
  const lines = Array.isArray(receipt.lines) ? receipt.lines : []
  const fulfillment = String(receipt.fulfillment ?? 'Pickup')

  const estHeight =
    340 +
    lines.length * 18 +
    (receipt.phone ? 14 : 0) +
    (fulfillment === 'Delivery' && receipt.address ? 28 : 0) +
    (code ? 52 : 0)

  const doc = new jsPDF({ unit: 'pt', format: [PAGE_W, Math.max(380, estHeight)], compress: true })
  const pageH = doc.internal.pageSize.getHeight()

  setFill(doc, C.page)
  doc.rect(0, 0, PAGE_W, pageH, 'F')

  // Header band
  const headerH = 52
  setFill(doc, C.header)
  doc.rect(0, 0, PAGE_W, headerH, 'F')
  setFill(doc, C.gold)
  doc.rect(0, headerH - 2.5, PAGE_W, 2.5, 'F')

  setText(doc, C.headerText)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(13)
  doc.text(title.toUpperCase(), PAGE_W / 2, 22, { align: 'center' })

  setText(doc, C.gold)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(7)
  doc.text('ONLINE ORDER TICKET', PAGE_W / 2, 36, { align: 'center' })

  let y = headerH + 16

  if (code) {
    const boxPad = 12
    const codeLabelH = 10
    const codeH = 28
    const boxH = boxPad * 2 + codeLabelH + codeH + 4
    const boxX = MARGIN
    const boxW = CONTENT_W

    setFill(doc, C.panel)
    setStroke(doc, C.panelBorder)
    doc.setLineWidth(1)
    doc.roundedRect(boxX, y, boxW, boxH, 6, 6, 'FD')

    setText(doc, C.goldDark)
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(7)
    doc.text('CONFIRMATION CODE', PAGE_W / 2, y + boxPad + 5, { align: 'center' })

    setText(doc, C.goldDark)
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(22)
    doc.text(code, PAGE_W / 2, y + boxPad + codeLabelH + 20, { align: 'center' })

    y += boxH + 14
  }

  if (receipt.placedAtLabel) {
    y = writeText(doc, y, receipt.placedAtLabel, { size: 8, align: 'center', color: C.muted, tight: true })
  }
  if (orderCode) {
    y = writeText(doc, y, `Order ref · ${orderCode}`, {
      size: 7.5,
      align: 'center',
      color: C.muted,
      tight: true,
    })
  }

  y += 6
  drawGoldRule(doc, y)
  y += 14

  // Fulfillment block
  const guestLine = `${fulfillment}${receipt.customerName ? ` · ${receipt.customerName}` : ''}`
  setFill(doc, C.rowAlt)
  const fulfillH = 22 + (receipt.phone ? 12 : 0)
  doc.roundedRect(MARGIN, y, CONTENT_W, fulfillH, 4, 4, 'F')

  y += 14
  y = writeText(doc, y, guestLine, { size: 9.5, style: 'bold', color: C.text, tight: true })
  if (receipt.phone) {
    y = writeText(doc, y, `Tel ${receipt.phone}`, { size: 8, color: C.muted, tight: true })
  }
  y += 8

  if (fulfillment === 'Delivery' && receipt.address) {
    y = writeText(doc, y, receipt.address, { size: 8, color: C.text })
    y += 2
  }
  if (fulfillment === 'Delivery' && receipt.instructions) {
    y = writeText(doc, y, `Notes: ${receipt.instructions}`, { size: 7.5, color: C.muted })
  }

  y += 6
  drawMutedRule(doc, y)
  y += 12

  // Items header
  setFill(doc, C.header)
  doc.rect(MARGIN, y - 8, CONTENT_W, 16, 'F')
  setText(doc, C.gold)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(7)
  doc.text('QTY', MARGIN + 4, y + 2)
  doc.text('ITEM', MARGIN + 28, y + 2)
  doc.text('TOTAL', PAGE_W - MARGIN - 4, y + 2, { align: 'right' })
  y += 16

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    if (i % 2 === 1) {
      setFill(doc, C.rowAlt)
      doc.rect(MARGIN, y - 6, CONTENT_W, 14, 'F')
    }
    const qty = String(line.quantity)
    const name = String(line.name ?? 'Item')
    const total = formatUsd(line.lineTotal)
    setText(doc, C.text)
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(8.5)
    doc.text(qty, MARGIN + 4, y + 2)
    const nameLines = doc.splitTextToSize(name, CONTENT_W - 90)
    doc.text(nameLines[0], MARGIN + 28, y + 2)
    doc.setFont('helvetica', 'bold')
    doc.text(total, PAGE_W - MARGIN - 4, y + 2, { align: 'right' })
    y += nameLines.length > 1 ? 20 : 14
  }

  y += 6
  drawGoldRule(doc, y, 8)
  y += 14

  y = writePair(doc, y, 'Subtotal', formatUsd(receipt.subtotal), { color: C.muted })
  y = writePair(doc, y, 'Tax', formatUsd(receipt.tax), { color: C.muted })
  y = writePair(doc, y, 'Service', formatUsd(receipt.service), { color: C.muted })
  if (Number(receipt.deliveryFee) > 0) {
    y = writePair(doc, y, 'Delivery', formatUsd(receipt.deliveryFee), { color: C.muted })
  }

  y += 4
  setFill(doc, C.header)
  doc.roundedRect(MARGIN, y, CONTENT_W, 26, 4, 4, 'F')
  setText(doc, C.gold)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(9)
  doc.text('GRAND TOTAL', MARGIN + 10, y + 16)
  doc.setFontSize(11)
  doc.text(formatUsd(receipt.grandTotal), PAGE_W - MARGIN - 10, y + 16, { align: 'right' })
  y += 36

  drawMutedRule(doc, y)
  y += 12
  writeText(doc, y, 'Present this ticket at pickup or to your driver.', {
    size: 7.5,
    align: 'center',
    color: C.muted,
    tight: true,
  })
  y += 10
  writeText(doc, y, 'Thank you for dining with us.', {
    size: 7.5,
    align: 'center',
    color: C.goldDark,
    style: 'italic',
    tight: true,
  })

  const filename = code ? `ticket-${code}.pdf` : orderCode ? `ticket-${orderCode}.pdf` : 'order-ticket.pdf'
  doc.save(filename)
}
