import { AnimatePresence, motion } from 'framer-motion'
import { Bell, Sparkles } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { callServer } from '../../utils/api'
import { CALL_SERVER_REASONS, CallServerReasonSheet } from './CallServerReasonSheet'

const spring = { type: 'spring', stiffness: 420, damping: 28 }
const COOLDOWN_MS = 60_000

function cooldownStorageKey(tableId) {
  return `elite_call_server_until_${tableId}`
}

/** @param {number} tableId */
function readCooldownUntil(tableId) {
  if (typeof window === 'undefined') return 0
  const raw = window.sessionStorage.getItem(cooldownStorageKey(tableId))
  const n = raw != null ? Number(raw) : 0
  return Number.isFinite(n) && n > Date.now() ? n : 0
}

/** @param {number} tableId */
function writeCooldown(tableId) {
  if (typeof window === 'undefined') return
  window.sessionStorage.setItem(cooldownStorageKey(tableId), String(Date.now() + COOLDOWN_MS))
}

/**
 * @param {unknown} res
 * @returns {{ name: string | null; reasonLabel: string | null }}
 */
function parseSuccess(res) {
  const name = res?.serverName ?? res?.ServerName
  const parsedName = name != null && String(name).trim() ? String(name).trim() : null
  if (parsedName) return { name: parsedName, reasonLabel: null }
  const msg = res?.message ?? res?.Message
  if (typeof msg === 'string') {
    const m = msg.match(/^We notified (.+)\.$/i)
    if (m?.[1]) return { name: m[1].trim(), reasonLabel: null }
  }
  return { name: null, reasonLabel: null }
}

function reasonLabelForCode(code) {
  return CALL_SERVER_REASONS.find((r) => r.code === code)?.label ?? null
}

/**
 * Fixed FAB for QR table guests — notifies assigned server via SignalR.
 * @param {number} tableId
 * @param {string} [className] extra positioning tweaks per screen
 */
export function CallServerButton({ tableId, className = '' }) {
  const [sheetOpen, setSheetOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [successLine, setSuccessLine] = useState(/** @type {string | null} */ (null))
  const [error, setError] = useState(/** @type {string | null} */ (null))
  const [cooldownUntil, setCooldownUntil] = useState(() => readCooldownUntil(tableId))
  const [now, setNow] = useState(() => Date.now())

  const cooldownSecLeft = cooldownUntil > now ? Math.ceil((cooldownUntil - now) / 1000) : 0
  const onCooldown = cooldownSecLeft > 0

  useEffect(() => {
    setCooldownUntil(readCooldownUntil(tableId))
  }, [tableId])

  useEffect(() => {
    if (!onCooldown) return undefined
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [onCooldown, cooldownUntil])

  useEffect(() => {
    if (!successLine) return undefined
    const id = window.setTimeout(() => setSuccessLine(null), 2500)
    return () => window.clearTimeout(id)
  }, [successLine])

  useEffect(() => {
    if (!error) return undefined
    const id = window.setTimeout(() => setError(null), 3200)
    return () => window.clearTimeout(id)
  }, [error])

  const submitReason = useCallback(
    async (reasonCode) => {
      setSheetOpen(false)
      setBusy(true)
      setError(null)
      const pickedLabel = reasonLabelForCode(reasonCode)
      try {
        const res = await callServer(tableId, reasonCode)
        const { name } = parseSuccess(res)
        const who = name ?? 'your server'
        const need = pickedLabel ? ` — ${pickedLabel}` : ''
        setSuccessLine(`We notified ${who}${need}.`)
        writeCooldown(tableId)
        const until = Date.now() + COOLDOWN_MS
        setCooldownUntil(until)
        setNow(Date.now())
      } catch (e) {
        const raw = e instanceof Error ? e.message : ''
        if (/wait\s+\d+\s+second/i.test(raw)) {
          const m = raw.match(/(\d+)\s+second/i)
          const sec = m ? Number(m[1]) : 60
          if (Number.isFinite(sec) && sec > 0) {
            const until = Date.now() + sec * 1000
            if (typeof window !== 'undefined') {
              window.sessionStorage.setItem(cooldownStorageKey(tableId), String(until))
            }
            setCooldownUntil(until)
            setNow(Date.now())
          }
        }
        setError(raw && raw.length < 80 ? raw : 'Could not reach your server.')
      } finally {
        setBusy(false)
      }
    },
    [tableId],
  )

  return (
    <>
      <button
        type="button"
        disabled={busy || onCooldown}
        onClick={() => {
          if (onCooldown) return
          setSheetOpen(true)
        }}
        className={`fixed z-[45] flex min-h-[44px] items-center gap-2 rounded-full border border-gold/45 bg-midnight-2/95 px-4 py-2.5 font-body text-[0.72rem] font-bold uppercase tracking-[0.12em] text-gold shadow-[0_8px_28px_rgba(0,0,0,0.45)] backdrop-blur-sm transition hover:border-gold/70 hover:bg-midnight-3 disabled:cursor-not-allowed disabled:opacity-50 bottom-[max(5.5rem,env(safe-area-inset-bottom))] right-4 sm:bottom-6 sm:right-6 ${className}`}
        aria-label={onCooldown ? `Call your server — available in ${cooldownSecLeft} seconds` : 'Call your server'}
      >
        <Bell className="h-4 w-4 shrink-0" aria-hidden />
        <span>{onCooldown ? `Wait ${cooldownSecLeft}s` : 'Call your Server'}</span>
      </button>

      <CallServerReasonSheet
        open={sheetOpen}
        busy={busy}
        onSelect={submitReason}
        onClose={() => {
          if (!busy) setSheetOpen(false)
        }}
      />

      <AnimatePresence>
        {successLine ? (
          <motion.div
            key="call-server-success"
            role="status"
            aria-live="polite"
            initial={{ opacity: 0, scale: 0.82, y: 12 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.9, y: 8 }}
            transition={spring}
            className="pointer-events-none fixed inset-x-0 z-[46] flex justify-center px-4 bottom-[max(9.5rem,env(safe-area-inset-bottom))] sm:bottom-[max(7.5rem,env(safe-area-inset-bottom))]"
          >
            <div className="flex max-w-[min(22rem,92vw)] items-center gap-3 rounded-2xl border border-gold/40 bg-gradient-to-br from-midnight-2 via-midnight-2 to-midnight-3 px-4 py-3 shadow-[0_12px_40px_rgba(0,0,0,0.5),0_0_24px_rgba(200,168,76,0.18)]">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-gold/35 bg-gold/15 text-gold">
                <Sparkles className="h-5 w-5" aria-hidden />
              </span>
              <p className="font-display text-base italic leading-snug text-champagne sm:text-lg">
                {successLine}
              </p>
            </div>
          </motion.div>
        ) : null}
      </AnimatePresence>

      <AnimatePresence>
        {error ? (
          <motion.div
            key="call-server-error"
            role="alert"
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 6 }}
            transition={{ duration: 0.2 }}
            className="fixed bottom-[max(9.5rem,env(safe-area-inset-bottom))] right-4 z-[46] max-w-[min(16rem,88vw)] rounded-xl border border-red-500/35 bg-midnight-2/95 px-3 py-2 text-center font-body text-xs text-red-200/95 shadow-lg sm:right-6"
          >
            {error}
          </motion.div>
        ) : null}
      </AnimatePresence>
    </>
  )
}
