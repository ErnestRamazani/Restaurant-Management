import { AnimatePresence, motion } from 'framer-motion'
import { ChefHat, CreditCard, MonitorCog, Utensils } from 'lucide-react'
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { BrowserRouter, Link, Navigate, Route, Routes } from 'react-router-dom'
import { useCart } from './hooks/useCart'
import { useMenu } from './hooks/useMenu'
import { useTable } from './hooks/useTable'
import { CartScreen } from './components/screens/CartScreen'
import { ConfirmScreen } from './components/screens/ConfirmScreen'
import { HeroScreen } from './components/screens/HeroScreen'
import { MenuScreen } from './components/screens/MenuScreen'
import { ProductSheet } from './components/screens/ProductSheet'
import { ConfirmDialog } from './components/ui/ConfirmDialog'
import { ErrorScreen } from './components/ui/ErrorScreen'
import { LoadingScreen } from './components/ui/LoadingScreen'

const spring = { type: 'spring', stiffness: 300, damping: 34 }
/** @internal history.state key for in-app back (cart → menu → hero) */
const H = 'elite'

function HubHome() {
  const cards = [
    { to: '/order', title: 'Consumer Menu', desc: 'Guests scan, browse, and send orders.', icon: Utensils },
    { to: '/server/', title: 'Server', desc: 'Take table orders and manage pickup.', icon: MonitorCog, external: true },
    { to: '/cashier', title: 'Cashier', desc: 'Release, complete, and manage checks.', icon: CreditCard },
    { to: '/kitchen', title: 'Kitchen', desc: 'View kitchen queue and ready orders.', icon: ChefHat },
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
          {cards.map(({ to, title, desc, icon: Icon, external }) => {
            const content = (
              <>
                <Icon className="h-8 w-8 text-gold" />
                <h2 className="mt-5 font-display text-2xl italic text-champagne">{title}</h2>
                <p className="mt-2 font-body text-sm leading-relaxed text-champagne/60">{desc}</p>
              </>
            )
            const className = 'rounded-3xl border border-champagne/10 bg-midnight-2 p-5 shadow-[0_10px_30px_rgba(0,0,0,0.28)] transition hover:border-gold/50 hover:bg-midnight-3'
            return external ? (
              <a key={to} href={to} className={className}>{content}</a>
            ) : (
              <Link key={to} to={to} className={className}>{content}</Link>
            )
          })}
        </div>
      </section>
    </main>
  )
}

function StaffRoutePlaceholder({ title, legacyHref }) {
  return (
    <main className="flex min-h-[100svh] items-center justify-center bg-midnight px-5 text-champagne">
      <section className="w-full max-w-lg rounded-3xl border border-champagne/10 bg-midnight-2 p-6 text-center shadow-[0_10px_30px_rgba(0,0,0,0.28)]">
        <p className="font-body text-xs font-bold uppercase tracking-[0.24em] text-gold/80">Cloud Web Hub</p>
        <h1 className="mt-3 font-display text-3xl italic">{title}</h1>
        <p className="mt-3 font-body text-sm leading-relaxed text-champagne/65">
          This route is reserved for the responsive React staff interface. The existing portal remains available during migration.
        </p>
        {legacyHref ? (
          <a
            href={legacyHref}
            className="mt-6 inline-flex min-h-11 items-center justify-center rounded-xl bg-gold px-5 font-body text-sm font-extrabold uppercase tracking-[0.08em] text-black"
          >
            Open current portal
          </a>
        ) : null}
        <div className="mt-5">
          <Link to="/" className="font-body text-sm font-semibold text-gold/90">Back to hub</Link>
        </div>
      </section>
    </main>
  )
}

function CustomerMenuApp() {
  const { tableId: tableIdFromUrl, hadInvalidTableParam } = useTable()
  const { config, products, loading, error, refetch } = useMenu()
  const cart = useCart(config)
  const [screen, setScreen] = useState(/** @type {'hero' | 'menu' | 'cart' | 'confirm'} */ ('hero'))
  const [sheetProduct, setSheetProduct] = useState(/** @type {Record<string, unknown> | null} */ (null))
  const [manualTableId, setManualTableId] = useState(/** @type {number | null} */ (null))
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
              <HeroScreen config={config} onEnterMenu={goMenu} />
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
    </>
  )
}

function HubOrMenu() {
  const params = new URLSearchParams(window.location.search)
  const table = params.get('table')
  return table ? <CustomerMenuApp /> : <HubHome />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HubOrMenu />} />
        <Route path="/order/*" element={<CustomerMenuApp />} />
        <Route path="/server/*" element={<StaffRoutePlaceholder title="Server" legacyHref="/server/" />} />
        <Route path="/cashier/*" element={<StaffRoutePlaceholder title="Cashier" legacyHref="/cashier.html" />} />
        <Route path="/kitchen/*" element={<StaffRoutePlaceholder title="Kitchen" />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
