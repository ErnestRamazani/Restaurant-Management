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
 * @param {number} y
 * @param {string} text
 * @param {{ size?: number; style?: 'normal' | 'bold' | 'italic'; color?: [number, number, number]; align?: 'left' | 'center' | 'right'; maxW?: number; tight?: boolean }} [opts]
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
 * @param {object} ticket
 * @param {string} [restaurantName]
 */
export async function downloadGuestReservationPdf(ticket, restaurantName = 'Restaurant') {
  const { jsPDF } = await import('jspdf')
  const code = String(ticket.confirmationCode ?? '').trim()
  const title = String(restaurantName ?? 'Restaurant').trim() || 'Restaurant'

  const estHeight = 320 + (ticket.userNotes ? 24 : 0) + (code ? 52 : 0)
  const doc = new jsPDF({ unit: 'pt', format: [PAGE_W, Math.max(360, estHeight)], compress: true })
  const pageH = doc.internal.pageSize.getHeight()

  setFill(doc, C.page)
  doc.rect(0, 0, PAGE_W, pageH, 'F')

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
  doc.text('TABLE RESERVATION TICKET', PAGE_W / 2, 36, { align: 'center' })

  let y = headerH + 16

  if (code) {
    const boxPad = 12
    const codeLabelH = 10
    const codeH = 28
    const boxH = boxPad * 2 + codeLabelH + codeH + 4

    setFill(doc, C.panel)
    setStroke(doc, C.panelBorder)
    doc.setLineWidth(1)
    doc.roundedRect(MARGIN, y, CONTENT_W, boxH, 6, 6, 'FD')

    setText(doc, C.goldDark)
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(7)
    doc.text('CONFIRMATION CODE', PAGE_W / 2, y + boxPad + 5, { align: 'center' })

    doc.setFontSize(22)
    doc.text(code, PAGE_W / 2, y + boxPad + codeLabelH + 20, { align: 'center' })

    y += boxH + 14
  }

  if (ticket.bookedAtLabel) {
    y = writeText(doc, y, ticket.bookedAtLabel, { size: 8, align: 'center', color: C.muted, tight: true })
  }

  y += 6
  drawGoldRule(doc, y)
  y += 14

  setFill(doc, C.rowAlt)
  const blockH = 56 + (ticket.userNotes ? 18 : 0)
  doc.roundedRect(MARGIN, y, CONTENT_W, blockH, 4, 4, 'F')

  y += 14
  const guestLine = ticket.guestName ? `${ticket.guestName}` : 'Guest'
  y = writeText(doc, y, guestLine, { size: 9.5, style: 'bold', color: C.text, tight: true })
  if (ticket.phone) {
    y = writeText(doc, y, `Tel ${ticket.phone}`, { size: 8, color: C.muted, tight: true })
  }
  if (ticket.arrivalLabel) {
    y = writeText(doc, y, `Arrival · ${ticket.arrivalLabel}`, { size: 8.5, color: C.text, tight: true })
  }
  if (ticket.endLabel) {
    y = writeText(doc, y, `Until · ${ticket.endLabel}`, { size: 8, color: C.muted, tight: true })
  }
  if (ticket.tableLabel) {
    y = writeText(doc, y, `Table · ${ticket.tableLabel}`, { size: 8.5, color: C.text, tight: true })
  }
  if (ticket.partySize != null) {
    const n = Number(ticket.partySize)
    y = writeText(doc, y, `Party · ${n} guest${n === 1 ? '' : 's'}`, { size: 8, color: C.muted, tight: true })
  }
  if (ticket.userNotes) {
    y = writeText(doc, y, `Notes · ${ticket.userNotes}`, { size: 7.5, color: C.muted, tight: true })
  }

  y += 16
  writeText(doc, y, 'Present this ticket at arrival. We hold your table for the reserved time.', {
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

  const filename = code ? `reservation-${code}.pdf` : 'reservation-ticket.pdf'
  doc.save(filename)
}
