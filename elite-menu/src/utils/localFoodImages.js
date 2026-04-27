import cremeBrulee from '../assets/AR-228515-simple-creme-brulee-dessert-dmfs-4x3-821623e7a86548eeb89370ac23d5f251.jpg'
import chickenParmesan from '../assets/easy-chicken-parmesan.jpg'
import filetMignon from '../assets/filets-mignons-sauce-liqueur-d-erable-aux-quatres-poivres-550x550.jpeg'
import chocolateLavaCake from '../assets/molten-lava-cake-feature.jpg'
import genericPlate from '../assets/images.jpg'
import { isDrinkProduct } from './menuKind'

function urlString(url) {
  return typeof url === 'string' ? url : String(url)
}

/**
 * @param {Record<string, unknown>} product
 */
function nameCat(product) {
  return `${String(product.name || '')} ${String(product.category || '')} ${String(product.subcategory || product.Subcategory || '')}`.toLowerCase()
}

/**
 * Local `src/assets` photos for food when the API leaves `photoUrl` empty.
 * @param {Record<string, unknown>} product
 * @returns {string | null}
 */
export function getLocalFoodPhotoUrl(product) {
  if (isDrinkProduct(product)) return null
  if (product?.photoUrl && String(product.photoUrl).trim()) return null

  const n = nameCat(product)

  if (n.includes('lava') || n.includes('molten') || n.includes('souffle') || n.includes('soufflé')) {
    return urlString(chocolateLavaCake)
  }

  if (n.includes('brulee') || n.includes('brûlée') || n.includes('brulée')) {
    return urlString(cremeBrulee)
  }
  if (n.includes('caramel') && n.includes('creme')) {
    return urlString(cremeBrulee)
  }

  if ((n.includes('filet') || n.includes('fillet')) && n.includes('mignon')) {
    return urlString(filetMignon)
  }

  if (
    n.includes('chicken') &&
    (n.includes('parmesan') || n.includes('parmigiana') || n.includes('parm'))
  ) {
    return urlString(chickenParmesan)
  }

  if (n.includes('burger') || n.includes('cheeseburger') || n.includes('bruschetta')) {
    return urlString(genericPlate)
  }

  return null
}

/**
 * @param {Record<string, unknown>} product
 */
export function withLocalFoodPhoto(product) {
  const local = getLocalFoodPhotoUrl(product)
  if (!local) return product
  return { ...product, photoUrl: local }
}
