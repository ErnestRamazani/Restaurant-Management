import { AnimatePresence, animate, motion, useMotionValue } from 'framer-motion'
import { useCallback, useEffect, useRef } from 'react'

export function BottomSheet({ open, onClose, children }) {
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

  // Non-passive touchmove on the scrollable sheet is required so preventDefault()
  // can stop the overflow scroll on mobile (window/pointer path cannot).
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
      if (
        e.target instanceof Element &&
        e.target.closest('button, input, textarea, select, a, [role="button"], [data-no-drag]')
      ) {
        return
      }

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

    const touchStartOpts = { passive: true }
    const touchMoveOpts = { passive: false }
    const touchEndOpts = { passive: true }

    el.addEventListener('touchstart', onTouchStart, touchStartOpts)
    el.addEventListener('touchmove', onTouchMove, touchMoveOpts)
    el.addEventListener('touchend', onTouchEnd, touchEndOpts)
    el.addEventListener('touchcancel', onTouchEnd, touchEndOpts)

    return () => {
      el.removeEventListener('touchstart', onTouchStart, touchStartOpts)
      el.removeEventListener('touchmove', onTouchMove, touchMoveOpts)
      el.removeEventListener('touchend', onTouchEnd, touchEndOpts)
      el.removeEventListener('touchcancel', onTouchEnd, touchEndOpts)
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
          <button
            type="button"
            aria-label="Close"
            className="absolute inset-0 bg-black/70"
            onClick={dismiss}
          />

          <motion.div
            ref={sheetRef}
            role="dialog"
            aria-modal="true"
            className="relative z-10 max-h-[88svh] overflow-y-auto rounded-t-3xl bg-midnight-2 shadow-2xl"
            style={{ y }}
          >
            <div className="flex justify-center pb-1 pt-3">
              <div className="h-1 w-10 rounded-full bg-champagne/20" />
            </div>

            {children}
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
