import { ChevronLeft, Search, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { translateFilterLabel } from '../../utils/i18nLabels'
import { productMatchesCourse } from '../../utils/courseBucket'
import { productMatchesDrinkAlcohol } from '../../utils/drinkAlcoholBucket'
import {
  getMenuKindWithTaxonomy,
  parseMenuTaxonomy,
  productMatchesDrinkTaxonomy,
  productMatchesFoodTaxonomy,
} from '../../utils/menuTaxonomy'
import { sameProductId } from '../../utils/productId'
import { CategoryBar } from '../ui/CategoryBar'
import { CartButton } from '../ui/CartButton'
import { MenuSelect } from '../ui/MenuSelect'
import { ProductCard } from '../ui/ProductCard'

const FOOD_COURSES = Object.freeze(['All', 'Starters', 'Main', 'Dessert'])
const DRINK_ALCOHOL = Object.freeze(['All', 'Alcohol', 'Non-alcohol'])

/**
 * @param {Record<string, unknown>[]} products
 * @param {'food' | 'drink'} section
 * @param {ReturnType<typeof parseMenuTaxonomy>} taxonomy
 */
function productsForSection(products, section, taxonomy) {
  return products.filter((p) => {
    const kind = getMenuKindWithTaxonomy(p, taxonomy)
    return section === 'drink' ? kind === 'drink' : kind === 'food'
  })
}

/**
 * Grouping label: subcategory if set, otherwise top-level category.
 * @param {Record<string, unknown>} p
 */
function groupKey(p) {
  const sub = String(p.subcategory || '').trim()
  if (sub) return sub
  const c = String(p.category || '').trim()
  return c || 'Other'
}

/**
 * @param {Record<string, unknown>[]} inSection
 */
function subcategoryOptionsForSection(inSection) {
  const set = new Set()
  for (const p of inSection) {
    set.add(groupKey(p))
  }
  const rest = [...set]
    .filter((k) => k && k !== 'Other')
    .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
  if (set.has('Other')) rest.push('Other')
  return ['All', ...rest]
}

export function MenuScreen({ config, products, guestOrderMode = 'browse', onBack, onOpenProduct, cart, onViewCart }) {
  const { t } = useTranslation()
  const taxonomy = useMemo(() => parseMenuTaxonomy(config), [config?.menuTaxonomyJson])

  const foodType = useMemo(() => taxonomy?.types?.find((t) => !t.isDrink) ?? null, [taxonomy])
  const drinkType = useMemo(() => taxonomy?.types?.find((t) => t.isDrink) ?? null, [taxonomy])

  const foodTabLabel = foodType?.name?.trim() ? String(foodType.name).trim() : t('guest.menu.food')
  const drinkTabLabel = drinkType?.name?.trim() ? String(drinkType.name).trim() : t('guest.menu.drinks')

  const foodCourseTabs = useMemo(() => {
    if (foodType?.sections?.length) return ['All', ...foodType.sections.map((s) => String(s.name || '').trim()).filter(Boolean)]
    return [...FOOD_COURSES]
  }, [foodType])

  const drinkSecondTabs = useMemo(() => {
    if (drinkType?.sections?.length) return ['All', ...drinkType.sections.map((s) => String(s.name || '').trim()).filter(Boolean)]
    return [...DRINK_ALCOHOL]
  }, [drinkType])

  const [section, setSection] = useState(/** @type {'food' | 'drink'} */ ('food'))
  const [course, setCourse] = useState('All')
  const [drinkAlcohol, setDrinkAlcohol] = useState('All')
  const [subCat, setSubCat] = useState('All')
  const [q, setQ] = useState('')

  const inSection = useMemo(() => productsForSection(products, section, taxonomy), [products, section, taxonomy])

  /** After course (food) or alcohol filter (drinks). */
  const afterMainFilter = useMemo(() => {
    if (section === 'drink') {
      if (drinkType?.sections?.length) return inSection.filter((p) => productMatchesDrinkTaxonomy(p, drinkAlcohol, drinkType))
      return inSection.filter((p) => productMatchesDrinkAlcohol(p, drinkAlcohol))
    }
    if (foodType?.sections?.length) return inSection.filter((p) => productMatchesFoodTaxonomy(p, course, foodType))
    return inSection.filter((p) => productMatchesCourse(p, course))
  }, [inSection, section, course, drinkAlcohol, drinkType, foodType])

  const subOptions = useMemo(() => subcategoryOptionsForSection(afterMainFilter), [afterMainFilter])

  /** If the list of subcategories shrinks, fall back to All for the active filter. */
  const resolvedSub = useMemo(() => {
    if (subCat === 'All') return 'All'
    if (!subOptions.includes(subCat)) return 'All'
    return subCat
  }, [subCat, subOptions])

  const { lines: cartLines } = cart

  const setSectionAndReset = useCallback((next) => {
    setSection(next)
    setCourse('All')
    setDrinkAlcohol('All')
    setSubCat('All')
    setQ('')
  }, [])

  const onCourseSelect = useCallback((next) => {
    setCourse(next)
    setSubCat('All')
  }, [])

  const onDrinkAlcoholSelect = useCallback((next) => {
    setDrinkAlcohol(next)
    setSubCat('All')
  }, [])

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase()
    return afterMainFilter.filter((p) => {
      if (resolvedSub !== 'All' && groupKey(p) !== resolvedSub) return false
      if (!query) return true
      const name = String(p.name || '').toLowerCase()
      const uid = String(p.uniqueId || '').toLowerCase()
      const sub = String(p.subcategory || '').toLowerCase()
      const c = String(p.category || '').toLowerCase()
      return name.includes(query) || uid.includes(query) || sub.includes(query) || c.includes(query)
    })
  }, [afterMainFilter, resolvedSub, q])

  const restaurantTitle = config?.restaurantName?.trim()
    ? String(config.restaurantName).trim()
    : t('guest.general.menuTitle')

  return (
    <div
      className="flex min-h-[100svh] flex-col bg-midnight"
      style={{
        background:
          'radial-gradient(ellipse 80% 60% at 50% 20%, #1c2732 0%, #0f1923 55%, #080e13 100%)',
      }}
    >
      <header className="flex h-14 shrink-0 flex-col border-b border-champagne/10 bg-midnight-2">
        <div className="flex h-14 items-center px-2">
        <button
          type="button"
          onClick={onBack}
          className="flex h-11 min-w-[44px] items-center justify-center text-champagne"
          aria-label={t('guest.general.back')}
        >
          <ChevronLeft className="h-6 w-6" />
        </button>
        <h2 className="line-clamp-2 flex-1 px-1 text-center font-display text-[0.95rem] leading-tight text-champagne sm:text-base">
          {restaurantTitle}
        </h2>
        <div className="w-11" />
        </div>
        {guestOrderMode === 'online' ? (
          <p className="border-t border-amber-500/20 bg-amber-500/10 px-3 py-2 text-center font-body text-[0.62rem] font-semibold uppercase leading-snug tracking-[0.14em] text-amber-100/95">
            {t('guest.menu.onlineBanner')}
          </p>
        ) : null}
      </header>

      <div className="shrink-0 border-b border-champagne/10 bg-midnight-2 px-4 pb-3 pt-2">
        <p className="mb-1.5 text-center font-body text-[0.65rem] font-medium uppercase tracking-[0.2em] text-gold/80">
          {t('guest.menu.orderFromSection')}
        </p>
        <div
          className="flex rounded-2xl border border-champagne/12 bg-midnight-3 p-1"
          role="tablist"
          aria-label={t('guest.menu.foodOrDrinksAria')}
        >
          <button
            type="button"
            role="tab"
            aria-selected={section === 'food'}
            onClick={() => setSectionAndReset('food')}
            className={`min-h-[48px] flex-1 rounded-xl font-body text-[0.9rem] font-bold uppercase tracking-[0.1em] transition-colors ${
              section === 'food'
                ? 'bg-gold/20 text-gold shadow-inner'
                : 'text-champagne/45 hover:text-champagne/75'
            }`}
          >
            {foodTabLabel}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={section === 'drink'}
            onClick={() => setSectionAndReset('drink')}
            className={`min-h-[48px] flex-1 rounded-xl font-body text-[0.9rem] font-bold uppercase tracking-[0.1em] transition-colors ${
              section === 'drink'
                ? 'bg-gold/20 text-gold shadow-inner'
                : 'text-champagne/45 hover:text-champagne/75'
            }`}
          >
            {drinkTabLabel}
          </button>
        </div>
        <p className="mt-1.5 text-center font-body text-[0.7rem] leading-snug text-[var(--text-muted)]">
          {t('guest.menu.browseHint')}
        </p>
      </div>

      {section === 'food' ? (
        <CategoryBar
          categories={foodCourseTabs}
          active={course}
          onSelect={onCourseSelect}
          sectionKind="food"
          translateLabel={(label) => translateFilterLabel(t, label)}
        />
      ) : (
        <CategoryBar
          categories={drinkSecondTabs}
          active={drinkAlcohol}
          onSelect={onDrinkAlcoholSelect}
          sectionKind="drink"
          translateLabel={(label) => translateFilterLabel(t, label)}
        />
      )}

      <div className="shrink-0 border-b border-champagne/10 bg-midnight-2 px-4 pb-3 pt-3">
        <MenuSelect
          label={t('guest.menu.subcategory')}
          options={subOptions}
          value={resolvedSub}
          onChange={setSubCat}
        />
      </div>

      <div className="relative shrink-0 px-4 pb-28 pt-3">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-champagne/35" />
          <input
            id="menu-search"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t('guest.menu.searchPlaceholder')}
            className="h-[42px] w-full rounded-[10px] border border-champagne/10 bg-champagne/[0.06] pl-10 pr-10 font-body text-[0.9rem] text-champagne placeholder:text-champagne/30"
            aria-label={t('guest.menu.searchAria')}
          />
          {q ? (
            <button
              type="button"
              className="absolute right-2 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center text-champagne/50"
              onClick={() => setQ('')}
              aria-label={t('guest.menu.clearSearchAria')}
            >
              <X className="h-4 w-4" />
            </button>
          ) : null}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-4 pb-28">
        {filtered.map((p) => {
          const pid = p.id
          const qty = cartLines.find((l) => sameProductId(l.product.id, pid))?.quantity ?? 0
          return (
            <div key={`${p.id}-${section}`}>
              <ProductCard
                key={qty > 0 ? `line-${p.id}` : `add-${p.id}`}
                product={p}
                qty={qty}
                onOpen={onOpenProduct}
                onAdd={cart.addItem}
                onMinus={() => cart.adjustLineQuantity(pid, -1)}
                onPlus={() => cart.addItem(p)}
              />
            </div>
          )
        })}
        {filtered.length === 0 ? (
          <p className="py-12 text-center font-body text-sm text-[var(--text-muted)]">
            {t('guest.menu.emptyResults')}
          </p>
        ) : null}
      </div>

      <CartButton totalItems={cart.totalItems} grandTotal={cart.grandTotal} onClick={onViewCart} />
    </div>
  )
}
