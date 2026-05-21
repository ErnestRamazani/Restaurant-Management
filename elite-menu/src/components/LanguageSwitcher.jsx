import { Globe } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { setAppLanguage } from '../i18n'

export function LanguageSwitcher({ className = '' }) {
  const { i18n, t } = useTranslation()
  const isFr = i18n.language?.startsWith('fr')

  return (
    <button
      type="button"
      onClick={() => setAppLanguage(isFr ? 'en' : 'fr')}
      className={`inline-flex min-h-[40px] items-center gap-2 rounded-full border border-champagne/15 bg-black/25 px-3 py-1.5 font-body text-[0.68rem] font-bold uppercase tracking-[0.12em] text-champagne/75 transition hover:border-gold/45 hover:text-gold ${className}`}
      title={t('common.switchLanguage')}
      aria-label={t('common.switchLanguage')}
    >
      <Globe className="h-4 w-4 text-gold/90" aria-hidden />
      <span>{isFr ? 'FR' : 'EN'}</span>
    </button>
  )
}
