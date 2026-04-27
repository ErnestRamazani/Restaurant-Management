/**
 * Classify a drink product. "unknown" = only show when the guest picks "All".
 * @param {Record<string, unknown>} p
 * @returns {'alcohol' | 'nonalcohol' | 'unknown'}
 */
export function getDrinkAlcoholKind(p) {
  const t = `${p.category || ''} ${p.subcategory || ''} ${p.name || ''}`.toLowerCase()
  if (/non-?alcoh|alcohol-?free|0\.0%|0%\s*abv/.test(t)) return 'nonalcohol'
  if (/\bvirgin\s+(colada|mojito|daquiri|mary)/.test(t)) return 'nonalcohol'
  if (
    /(wine|beers?|champagne|vodka|whisky|whiskey|gin|rum|tequila|cocktail|mojito|sangria|martini|margarita|negroni|stout|lager|ipa|brandy|prosecco|sake|liqueur|schnapps|aperol|vermouth|port|sherry|hard\s*seltzer|mulled|pilsner|porter|daiquiri|old\s*fashioned|manhattan|sidecar)/.test(
      t
    )
  ) {
    return 'alcohol'
  }
  if (/(cider|ales?|lagers?)/.test(t) && !/non-?alcoh|apple\s*juice/.test(t)) return 'alcohol'
  if (
    /(juice|coffee|tea|soda|cola|water|milk|smoothie|lemonade|espresso|latte|mocha|frapp|soft|tonic|energy|hot\s*choc|mocktail)/.test(t)
  ) {
    return 'nonalcohol'
  }
  return 'unknown'
}

/**
 * @param {Record<string, unknown>} p
 * @param {'All' | 'Alcohol' | 'Non-alcohol'} pick
 */
export function productMatchesDrinkAlcohol(p, pick) {
  if (pick === 'All') return true
  const k = getDrinkAlcoholKind(p)
  if (pick === 'Alcohol') return k === 'alcohol'
  if (pick === 'Non-alcohol') return k === 'nonalcohol'
  return true
}
