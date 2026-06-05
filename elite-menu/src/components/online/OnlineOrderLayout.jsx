import { useCallback, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, useNavigate } from 'react-router-dom'
import { useMenu } from '../../hooks/useMenu'
import { useOnlineOrderCart } from '../../hooks/useOnlineOrderCart'
import { ErrorScreen } from '../ui/ErrorScreen'
import { LoadingScreen } from '../ui/LoadingScreen'
import { OnlineOrderConfirmScreen } from './OnlineOrderConfirmScreen'

/**
 * @typedef {{
 *   cart: ReturnType<typeof useOnlineOrderCart>;
 *   config: Record<string, unknown>;
 *   products: Record<string, unknown>[];
 *   restaurantName: string;
 *   completeOrder: (res: {
 *     label: string;
 *     message: string;
 *     orderCode?: string;
 *     confirmationCode?: string;
 *     receipt?: Record<string, unknown>;
 *   }) => void;
 * }} OnlineOrderOutletContext
 */

export function OnlineOrderLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { config, products, loading, error, refetch } = useMenu()
  const cart = useOnlineOrderCart(config)
  const [confirm, setConfirm] = useState(
    /** @type {null | { label: string; message: string; orderCode?: string; confirmationCode?: string; receipt?: Record<string, unknown>; estimatedPrepMinutes: number | null }} */ (
      null
    )
  )

  const restaurantName =
    config?.restaurantName != null && String(config.restaurantName).trim()
      ? String(config.restaurantName).trim()
      : t('guest.general.restaurant')

  const completeOrder = useCallback(
    (res) => {
      const n =
        cart.estimatedPrepMinutes > 0 ? Math.round(Number(cart.estimatedPrepMinutes)) : null
      setConfirm({
        label: res.label,
        message: res.message,
        orderCode: res.orderCode,
        confirmationCode: res.confirmationCode,
        receipt: res.receipt,
        estimatedPrepMinutes: Number.isFinite(n) && n > 0 ? n : null,
      })
    },
    [cart.estimatedPrepMinutes],
  )

  const outletContext = useMemo(
    () =>
      /** @type {OnlineOrderOutletContext} */ ({
        cart,
        config: config ?? {},
        products: Array.isArray(products) ? products : [],
        restaurantName,
        completeOrder,
      }),
    [cart, config, products, restaurantName, completeOrder],
  )

  if (loading) return <LoadingScreen />
  if (error) return <ErrorScreen message={error} onRetry={refetch} />
  if (!config) return <ErrorScreen message={t('guest.general.missingConfig')} onRetry={refetch} />

  if (confirm) {
    return (
      <OnlineOrderConfirmScreen
        confirmationCode={confirm.confirmationCode}
        receipt={confirm.receipt}
        restaurantName={restaurantName}
        label={confirm.label}
        estimatedPrepMinutes={confirm.estimatedPrepMinutes}
        onOrderMore={() => setConfirm(null)}
        onBackToStart={() => {
          setConfirm(null)
          navigate('/')
        }}
      />
    )
  }

  return <Outlet context={outletContext} />
}
