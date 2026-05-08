import { AnimatePresence, motion } from 'framer-motion'
import { ChefHat, CreditCard, MonitorCog, ShieldCheck, Utensils } from 'lucide-react'
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { BrowserRouter, Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import { useCart } from './hooks/useCart'
import { useMenu } from './hooks/useMenu'
import { useTable } from './hooks/useTable'
import { CartScreen } from './components/screens/CartScreen'
import { ConfirmScreen } from './components/screens/ConfirmScreen'
import { HeroScreen } from './components/screens/HeroScreen'
import { MenuScreen } from './components/screens/MenuScreen'
import { ProductSheet } from './components/screens/ProductSheet'
import { ReservationScreen } from './components/screens/ReservationScreen'
import { ConfirmDialog } from './components/ui/ConfirmDialog'
import { ErrorScreen } from './components/ui/ErrorScreen'
import { LoadingScreen } from './components/ui/LoadingScreen'
import { validateStaffLoginCode } from './utils/api'

const spring = { type: 'spring', stiffness: 300, damping: 34 }
/** @internal history.state key for in-app back (cart → menu → hero) */
const H = 'elite'

function portalHref(path) {
  if (window.location.port === '5173') {
    return `http://${window.location.hostname}:8080${path}`
  }

  return path
}

function HubHome() {
  const cards = [
    { to: '/', title: 'Consumer Menu', desc: 'Guests scan, browse, and send orders.', icon: Utensils },
    { to: '/server/index.html', title: 'Server', desc: 'Take table orders and manage pickup.', icon: MonitorCog, external: true },
    { to: '/cashier.html', title: 'Cashier', desc: 'Release, complete, and manage checks.', icon: CreditCard, external: true },
    { title: 'Kitchen', desc: 'Kitchen web workspace is coming soon.', icon: ChefHat, disabled: true },
    { title: 'Admin', desc: 'Cloud admin workspace placeholder.', icon: ShieldCheck, disabled: true },
  ]

  return (
    <main className="min-h-[100svh] bg-midnight px-5 py-8 text-champagne">
      <section className="mx-auto flex max-w-4xl flex-col gap-8">
        <div className="text-center">
          <p className="font-body text-xs font-bold uppercase tracking-[0.28em] text-gold/80">EliteRestaurant Cloud</p>
          <h1 className="mt-3 font-display text-4xl italic text-champagne">Choose your workspace</h1>
          <p className="mx-auto mt-3 max-w-2xl font-body text-sm leading-relaxed text-champagne/65">
            One online entry point for guests and staff. Staff areas require sign-in; the consumer menu remains public.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          {cards.map(({ to, title, desc, icon: Icon, external, disabled }) => {
            const content = (
              <>
                <Icon className="h-8 w-8 text-gold" />
                <h2 className="mt-5 font-display text-2xl italic text-champagne">{title}</h2>
                <p className="mt-2 font-body text-sm leading-relaxed text-champagne/60">{desc}</p>
              </>
            )
            const className = `rounded-3xl border border-champagne/10 bg-midnight-2 p-5 shadow-[0_10px_30px_rgba(0,0,0,0.28)] transition ${disabled ? 'cursor-not-allowed opacity-55' : 'hover:border-gold/50 hover:bg-midnight-3'}`
            if (disabled) {
              return <div key={title} className={className} aria-disabled="true">{content}</div>
            }
            return external ? (
              <a key={to} href={portalHref(to)} className={className}>{content}</a>
            ) : (
              <Link key={to} to={to} className={className}>{content}</Link>
            )
          })}
        </div>
      </section>
    </main>
  )
}

function ReservationPage() {
  const { config, loading, error, refetch } = useMenu()

  if (loading) {
    return <LoadingScreen />
  }
  if (error) {
    return <ErrorScreen message={error} onRetry={refetch} />
  }

  return <ReservationScreen config={config} />
}

function CustomerMenuApp() {
  const navigate = useNavigate()
  const { tableId: tableIdFromUrl, hadInvalidTableParam } = useTable()
  const { config, products, loading, error, refetch } = useMenu()
  const cart = useCart(config)
  const [screen, setScreen] = useState(/** @type {'hero' | 'menu' | 'cart' | 'confirm'} */ ('hero'))
  const [sheetProduct, setSheetProduct] = useState(/** @type {Record<string, unknown> | null} */ (null))
  const [manualTableId, setManualTableId] = useState(/** @type {number | null} */ (null))
  const [staffLoginOpen, setStaffLoginOpen] = useState(false)
  const [staffLoginCode, setStaffLoginCode] = useState('')
  const [staffLoginError, setStaffLoginError] = useState('')
  const [staffLoginBusy, setStaffLoginBusy] = useState(false)
  const [orderRef, setOrderRef] = useState(
    /** @type {{ label: string; message: string; estimatedPrepMinutes: number | null }} */ ({
      label: '',
      message: '',
      estimatedPrepMinutes: null,
    }),
  )
  const historyInited = useRef(false)
  const lastSheetProduct = useRef(/** @type {Record<string, unknown> | null} */ (null))

  useLayoutEffect(() => {
    if (typeof window === 'undefined' || loading || error) return
    if (historyInited.current) return
    historyInited.current = true
    history.replaceState({ [H]: 'hero' }, '', window.location.href)
  }, [loading, error])

  const goMenu = useCallback(() => {
    setScreen('menu')
    history.pushState({ [H]: 'menu' }, '', window.location.href)
  }, [])

  const goCart = useCallback(() => {
    setScreen('cart')
    history.pushState({ [H]: 'cart' }, '', window.location.href)
  }, [])

  const onMenuBack = useCallback(() => {
    history.back()
  }, [])

  const onCartBack = useCallback(() => {
    history.back()
  }, [])

  const onOrderSuccess = useCallback((res) => {
    const est = res?.estimatedPrepMinutes ?? res?.EstimatedPrepMinutes
    const n = est != null && est !== '' ? Number(est) : NaN
    setOrderRef({
      label: res?.label != null ? String(res.label) : '',
      message: res?.message != null ? String(res.message) : '',
      estimatedPrepMinutes: Number.isFinite(n) && n > 0 ? Math.round(n) : null,
    })
    setScreen('confirm')
    history.pushState({ [H]: 'confirm' }, '', window.location.href)
  }, [])

  const onOrderMore = useCallback(() => {
    window.history.go(-2)
  }, [])

  const onBackToStart = useCallback(() => {
    window.history.go(-3)
  }, [])

  const closeProductSheet = useCallback(() => {
    const st = history.state
    if (st && typeof st === 'object' && 'sheet' in st) {
      history.back()
    } else {
      setSheetProduct(null)
    }
  }, [])

  const closeStaffLogin = useCallback(() => {
    if (staffLoginBusy) return
    setStaffLoginOpen(false)
    setStaffLoginCode('')
    setStaffLoginError('')
  }, [staffLoginBusy])

  const submitStaffLogin = useCallback(async () => {
    const code = staffLoginCode.trim()
    if (!code) {
      setStaffLoginError('Enter the staff passcode.')
      return
    }

    setStaffLoginBusy(true)
    setStaffLoginError('')
    try {
      await validateStaffLoginCode(code)
      setStaffLoginOpen(false)
      setStaffLoginCode('')
      navigate('/login')
    } catch (error) {
      setStaffLoginError(error instanceof Error ? error.message : 'Incorrect staff passcode.')
    } finally {
      setStaffLoginBusy(false)
    }
  }, [navigate, staffLoginCode])

  useEffect(() => {
    if (typeof window === 'undefined' || loading || error) return
    if (sheetProduct && !lastSheetProduct.current) {
      history.pushState({ sheet: 1 }, '', window.location.href)
    }
    lastSheetProduct.current = sheetProduct
  }, [sheetProduct, loading, error])

  useEffect(() => {
    if (typeof window === 'undefined' || loading || error) return
    const onPop = (/** @type {PopStateEvent} */ e) => {
      setSheetProduct(null)

      const s = e.state && /** @type {any} */ (e.state)[H]
      if (s === 'hero' || s === 'menu' || s === 'cart' || s === 'confirm') {
        setScreen(s)
      }
    }
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [loading, error])

  if (loading) {
    return <LoadingScreen />
  }
  if (error) {
    return <ErrorScreen message={error} onRetry={refetch} />
  }

  return (
    <>
      <div className="min-h-[100svh]">
        <AnimatePresence mode="wait">
          {screen === 'hero' && (
            <motion.div
              key="hero"
              className="min-h-[100svh]"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={spring}
            >
              <HeroScreen
                config={config}
                onEnterMenu={goMenu}
                onReservation={() => navigate('/reservation')}
                onStaffLogin={() => setStaffLoginOpen(true)}
              />
            </motion.div>
          )}

          {screen === 'menu' && (
            <motion.div
              key="menu"
              className="min-h-[100svh]"
              initial={{ opacity: 0, x: 32 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -32 }}
              transition={spring}
            >
              <MenuScreen
                config={config}
                products={products}
                onBack={onMenuBack}
                onOpenProduct={setSheetProduct}
                cart={cart}
                onViewCart={goCart}
              />
            </motion.div>
          )}

          {screen === 'cart' && (
            <CartScreen
              key="cart"
              cart={cart}
              tableIdFromUrl={tableIdFromUrl}
              hadInvalidTableParam={hadInvalidTableParam}
              manualTableId={manualTableId}
              setManualTableId={setManualTableId}
              onBack={onCartBack}
              onSuccess={onOrderSuccess}
            />
          )}

          {screen === 'confirm' && (
            <ConfirmScreen
              key="confirm"
              label={orderRef.label}
              message={orderRef.message}
              estimatedPrepMinutes={orderRef.estimatedPrepMinutes}
              onOrderMore={onOrderMore}
              onBackToStart={onBackToStart}
            />
          )}
        </AnimatePresence>
      </div>

      <ProductSheet
        product={sheetProduct}
        open={sheetProduct != null}
        onClose={closeProductSheet}
        cart={cart}
      />

      <ConfirmDialog
        open={cart.sectionConflict != null}
        title="Switch order type?"
        confirmLabel="Continue"
        cancelLabel="Cancel"
        onConfirm={cart.confirmSectionSwitch}
        onCancel={cart.cancelSectionSwitch}
      >
        {cart.sectionConflict?.message ?? ''}
      </ConfirmDialog>

      {staffLoginOpen ? (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/60 px-4 pb-6 backdrop-blur-sm sm:items-center sm:pb-0">
          <div className="w-full max-w-sm rounded-3xl border border-champagne/10 bg-midnight-2 p-5 text-champagne shadow-[0_22px_70px_rgba(0,0,0,0.45)]">
            <div className="text-center">
              <p className="font-body text-[0.66rem] font-bold uppercase tracking-[0.24em] text-gold/80">Staff access</p>
              <h2 className="mt-2 font-display text-2xl italic">Enter passcode</h2>
              <p className="mt-2 font-body text-sm leading-relaxed text-champagne/60">
                This area is for restaurant staff only.
              </p>
            </div>

            <label className="mt-5 block font-body text-xs font-bold uppercase tracking-[0.16em] text-champagne/55" htmlFor="staffLoginCode">
              Passcode
            </label>
            <input
              id="staffLoginCode"
              value={staffLoginCode}
              onChange={(e) => {
                setStaffLoginCode(e.target.value)
                setStaffLoginError('')
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault()
                  submitStaffLogin()
                }
                if (e.key === 'Escape') {
                  closeStaffLogin()
                }
              }}
              type="password"
              autoFocus
              className="mt-2 h-12 w-full rounded-2xl border border-gold/25 bg-black/20 px-4 text-center font-body text-lg font-semibold tracking-[0.25em] text-champagne outline-none transition focus:border-gold focus:ring-2 focus:ring-gold/20"
            />
            {staffLoginError ? (
              <p className="mt-3 rounded-xl border border-red-500/20 bg-red-500/10 px-3 py-2 text-center font-body text-xs font-semibold text-red-200">
                {staffLoginError}
              </p>
            ) : null}

            <div className="mt-5 grid grid-cols-2 gap-3">
              <button
                type="button"
                onClick={closeStaffLogin}
                disabled={staffLoginBusy}
                className="h-11 rounded-xl border border-champagne/10 font-body text-sm font-bold text-champagne/70 transition hover:border-champagne/25 hover:text-champagne disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={submitStaffLogin}
                disabled={staffLoginBusy}
                className="h-11 rounded-xl bg-gold font-body text-sm font-extrabold uppercase tracking-[0.08em] text-black transition hover:brightness-105 disabled:opacity-60"
              >
                {staffLoginBusy ? 'Checking...' : 'Unlock'}
              </button>
            </div>
          </div>
        </div>
      ) : null}

    </>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<CustomerMenuApp />} />
        <Route path="/order/*" element={<CustomerMenuApp />} />
        <Route path="/reservation" element={<ReservationPage />} />
        <Route path="/login" element={<HubHome />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
