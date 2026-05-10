import { AnimatePresence, animate, motion, useMotionValue } from 'framer-motion'
import { useCallback, useEffect, useRef } from 'react'

/** Max squared movement (px²) to count as a tap on the backdrop (not a scroll/drag). */
const BACKDROP_TAP_THRESHOLD_SQ = 24 * 24

function TapBackdrop({ onClose }) {
  const startRef = useRef(/** @type {{ x: number; y: number; pointerId: number } | null} */ (null))

  return (
    <div
      role="presentation"
      className="absolute inset-0 bg-black/70"
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) {
          startRef.current = { x: e.clientX, y: e.clientY, pointerId: e.pointerId }
        }
      }}
      onPointerUp={(e) => {
        if (e.target !== e.currentTarget) return
        const start = startRef.current
        startRef.current = null
        if (!start || start.pointerId !== e.pointerId) return
        const dx = e.clientX - start.x
        const dy = e.clientY - start.y
        if (dx * dx + dy * dy <= BACKDROP_TAP_THRESHOLD_SQ) onClose()
      }}
      onPointerCancel={() => {
        startRef.current = null
      }}
    />
  )
}

export function BottomSheet({ open, onClose, children }) {
  /** The scrollable panel (same node that has translateY). Its scrollTop is authoritative for swipe logic. */
  const sheetRef = useRef(/** @type {HTMLDivElement | null} */ (null))
  const y = useMotionValue(0)

  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  const dismiss = useCallback(() => {
    const h = typeof window !== 'undefined' ? window.innerHeight : 0
    animate(y, h, {
      type: 'spring',
      stiffness: 280,
      damping: 32,
    }).then(() => {
      y.set(0)
      onCloseRef.current()
    })
  }, [y])

  useEffect(() => {
    if (!open) return
    const h = typeof window !== 'undefined' ? window.innerHeight : 0
    y.set(h)
    animate(y, 0, { type: 'spring', stiffness: 320, damping: 32 })
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = ''
    }
  }, [open, y])

  // Swipe down to dismiss when this sheet’s *own* scroll position is at the top (scrollTop === 0).
  // Listeners are on the scrolling motion node so scrollTop matches user scrolling (fixes nested-list issues).
  useEffect(() => {
    const el = sheetRef.current
    if (!el || !open) return

    let startY = 0
    let startScrollTop = 0
    let dragging = false
    let decided = false

    /** @param {TouchEvent} e */
    function onTouchStart(e) {
      if (e.touches.length !== 1) return
      startY = e.touches[0].clientY
      startScrollTop = el.scrollTop
      dragging = false
      decided = false
    }

    /** @param {TouchEvent} e */
    function onTouchMove(e) {
      if (e.touches.length !== 1) return

      const dy = e.touches[0].clientY - startY

      if (!decided) {
        if (Math.abs(dy) < 8) return
        decided = true
        if (dy > 0 && startScrollTop <= 0) {
          dragging = true
        } else {
          return
        }
      }

      if (dragging) {
        e.preventDefault()
        y.set(Math.max(0, dy))
      }
    }

    function onTouchEnd() {
      if (!dragging) return
      dragging = false
      decided = false

      const currentY = y.get()
      if (currentY > 80) {
        dismiss()
      } else {
        animate(y, 0, { type: 'spring', stiffness: 400, damping: 36 })
      }
    }

    el.addEventListener('touchstart', onTouchStart, { passive: true })
    el.addEventListener('touchmove', onTouchMove, { passive: false })
    el.addEventListener('touchend', onTouchEnd)
    el.addEventListener('touchcancel', onTouchEnd)

    return () => {
      el.removeEventListener('touchstart', onTouchStart)
      el.removeEventListener('touchmove', onTouchMove)
      el.removeEventListener('touchend', onTouchEnd)
      el.removeEventListener('touchcancel', onTouchEnd)
    }
  }, [open, dismiss, y])

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[100] flex flex-col justify-end"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
        >
          <TapBackdrop onClose={dismiss} />

          <motion.div
            ref={sheetRef}
            role="dialog"
            aria-modal="true"
            className="relative z-10 flex max-h-[88svh] flex-col overflow-y-auto overflow-x-hidden overscroll-y-contain rounded-t-3xl bg-midnight-2 shadow-2xl [-webkit-overflow-scrolling:touch]"
            style={{ y }}
          >
            <div className="sticky top-0 z-10 flex shrink-0 flex-col items-center bg-midnight-2 px-4 pb-2 pt-3" aria-hidden>
              <div className="h-1 w-10 rounded-full bg-champagne/20" />
            </div>

            <div className="flex flex-col">{children}</div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
