import { AnimatePresence, motion } from 'framer-motion'
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
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

export default function App() {
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
