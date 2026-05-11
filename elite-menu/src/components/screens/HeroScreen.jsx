import { useState } from 'react'
import { motion } from 'framer-motion'
import { BottomSheet } from '../ui/BottomSheet'
import { GoldDivider } from '../ui/GoldDivider'
import { resolveApiAssetUrl } from '../../utils/apiClient'

function Particles() {
  const dots = Array.from({ length: 10 }, (_, i) => ({
    id: i,
    left: `${8 + ((i * 17) % 84)}%`,
    duration: 8 + (i % 5) * 2.4,
    delay: i * 0.4,
    opacity: 0.12 + (i % 4) * 0.05,
  }))
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden>
      {dots.map((d) => (
        <motion.span
          key={d.id}
          className="absolute h-0.5 w-0.5 rounded-full bg-gold"
          style={{ left: d.left, bottom: '10%' }}
          initial={{ opacity: d.opacity, y: 0 }}
          animate={{ y: [-20, -420], opacity: [d.opacity, 0.05] }}
          transition={{
            duration: d.duration,
            repeat: Infinity,
            ease: 'linear',
            delay: d.delay,
          }}
        />
      ))}
    </div>
  )
}

function FooterLink({ href, children, onSelect }) {
  return (
    <a
      href={href}
      onClick={(e) => {
        e.preventDefault()
        onSelect()
      }}
      className="inline-block min-h-[44px] cursor-pointer py-1.5 font-body text-[0.68rem] font-medium uppercase tracking-[0.14em] text-champagne/45 transition-colors hover:text-champagne/70 active:opacity-80"
    >
      {children}
    </a>
  )
}

function RoyalDivider({ className = '' }) {
  return (
    <div className={`flex w-full items-center justify-center ${className}`} aria-hidden>
      <div className="h-px w-20 bg-gradient-to-r from-transparent via-gold/45 to-gold/20" />
      <span className="mx-2 h-2 w-2 rotate-45 border border-gold/45" />
      <span className="h-1.5 w-1.5 rounded-full bg-gold/55" />
      <span className="mx-2 h-2 w-2 rotate-45 border border-gold/45" />
      <div className="h-px w-20 bg-gradient-to-l from-transparent via-gold/45 to-gold/20" />
    </div>
  )
}

function formatWebsiteHref(raw) {
  const s = String(raw ?? '').trim()
  if (!s) return ''
  if (/^https?:\/\//i.test(s)) return s
  return `https://${s}`
}

export function HeroScreen({ config, onEnterMenu, onOrderOnline, onReservation, onStaffLogin }) {
  const [info, setInfo] = useState(/** @type {null | 'about' | 'contact' | 'notes'} */ (null))

  const tagline =
    config?.tagline && String(config.tagline).trim()
      ? String(config.tagline).trim()
      : 'Cuisine moderne · Kinshasa'
  const name = config?.restaurantName ? String(config.restaurantName) : 'Elite Restaurant'
  const logoUrl = config?.logoUrl ? String(config.logoUrl) : ''
  const logoSrc = logoUrl ? resolveApiAssetUrl(logoUrl) : ''
  const phone = config ? String(config.phone ?? config.Phone ?? '').trim() : ''
  const address = config ? String(config.address ?? config.Address ?? '').trim() : ''
  const website = config ? String(config.websiteDomain ?? config.WebsiteDomain ?? '').trim() : ''
  const socialMedia = config ? String(config.socialMedia ?? config.SocialMedia ?? '').trim() : ''
  const taxLegal = config ? String(config.taxIdLegalInfo ?? config.TaxIdLegalInfo ?? '').trim() : ''

  const websiteHref = formatWebsiteHref(website)

  return (
    <div className="relative flex min-h-[100svh] flex-col overflow-hidden bg-midnight">
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[60vw] w-[60vw] rounded-full bg-[rgba(200,168,76,0.04)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[40vw] w-[40vw] rounded-full bg-[rgba(237,232,220,0.02)] blur-3xl" />
      <Particles />

      <div className="relative z-10 flex min-h-0 flex-1 flex-col px-6 pb-[max(1.25rem,env(safe-area-inset-bottom))] pt-[max(0.75rem,env(safe-area-inset-top))]">
        <motion.div
          className="flex flex-col items-center"
          initial={{ opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2, duration: 0.55 }}
        >
          {logoUrl ? (
            <motion.img
              src={logoSrc}
              alt=""
              className="mb-1 h-auto w-full max-w-[min(100%,32rem)] object-contain"
              style={{ maxHeight: 'min(300px, 55vw)', minHeight: 'min(150px, 28vw)' }}
              animate={{ scale: [1, 1.018, 1] }}
              transition={{ duration: 3.2, repeat: Infinity, ease: 'easeInOut' }}
            />
          ) : null}
          <h1
            className="-mt-4 text-center font-display text-[clamp(1.65rem,6.5vw,2.75rem)] italic leading-[1.12] tracking-[0.04em] text-champagne"
            style={{ fontFamily: '"Playfair Display", serif' }}
          >
            {name}
          </h1>
        </motion.div>

        <motion.div
          className="mt-2 flex flex-col items-center"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.45 }}
        >
          <motion.div
            className="w-full"
            initial={{ scaleX: 0 }}
            animate={{ scaleX: 1 }}
            transition={{ delay: 0.45, duration: 0.45 }}
          >
            <RoyalDivider />
          </motion.div>
          <p className="mt-3 max-w-[min(100%,22rem)] text-center font-body text-[0.78rem] font-light uppercase leading-relaxed tracking-[0.18em] text-[var(--text-muted)]">
            {tagline}
          </p>
        </motion.div>

        <div className="min-h-[5rem] flex-1" aria-hidden />

        <motion.div
          className="mb-4 flex w-full max-w-[min(100%,22rem)] flex-col items-stretch gap-3 sm:max-w-xl"
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.65, duration: 0.4 }}
        >
          <button
            type="button"
            onClick={onEnterMenu}
            className="relative min-h-[56px] w-full border border-gold/50 bg-gold/5 px-6 py-3.5 font-display text-[clamp(0.72rem,3.2vw,0.95rem)] font-semibold uppercase tracking-[0.14em] text-gold shadow-[0_6px_28px_rgba(200,168,76,0.12)] transition-colors hover:border-gold hover:bg-[var(--gold-dim)] active:scale-[0.98] sm:tracking-[0.18em]"
            style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
          >
            <span className="pointer-events-none absolute left-[-3px] top-[-3px] h-3 w-3 border-l border-t border-gold/70" />
            <span className="pointer-events-none absolute right-[-3px] top-[-3px] h-3 w-3 border-r border-t border-gold/70" />
            <span className="pointer-events-none absolute bottom-[-3px] left-[-3px] h-3 w-3 border-b border-l border-gold/70" />
            <span className="pointer-events-none absolute bottom-[-3px] right-[-3px] h-3 w-3 border-b border-r border-gold/70" />
            Explore our menu
          </button>
          <div className="flex flex-col gap-3 sm:flex-row sm:gap-4">
            <button
              type="button"
              onClick={onOrderOnline}
              className="min-h-[52px] flex-1 rounded-2xl border border-amber-400/55 bg-amber-500/10 px-5 py-3 font-display text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-amber-100/95 shadow-[0_4px_20px_rgba(251,191,36,0.08)] transition hover:border-amber-300 hover:bg-amber-500/15 active:scale-[0.98] sm:text-[0.7rem]"
              style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
            >
              Order online
            </button>
            <button
              type="button"
              onClick={onReservation}
              className="min-h-[52px] flex-1 rounded-2xl border border-champagne/25 bg-champagne/5 px-5 py-3 font-display text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-champagne/90 transition hover:border-gold/40 hover:text-gold active:scale-[0.98] sm:text-[0.7rem]"
              style={{ fontFamily: '"Cinzel", "Playfair Display", serif' }}
            >
              Reserve a table
            </button>
          </div>
        </motion.div>

        <div className="relative z-20 mt-12 pt-6 sm:mt-14 sm:pt-8">
          <RoyalDivider className="mb-8 sm:mb-10" />
          <nav
            className="mt-20 flex flex-wrap items-center justify-center gap-x-8 gap-y-3"
            aria-label="Footer"
          >
            <FooterLink href="#info-about" onSelect={() => setInfo('about')}>
              About
            </FooterLink>
            <FooterLink href="#info-contact" onSelect={() => setInfo('contact')}>
              Contact
            </FooterLink>
            <FooterLink href="#info-notes" onSelect={() => setInfo('notes')}>
              Notes
            </FooterLink>
          </nav>
          <div className="mt-2 flex flex-col items-center text-center font-body text-[0.65rem] text-champagne/25">
            {onStaffLogin ? (
              <button
                type="button"
                onClick={onStaffLogin}
                className="rounded-full border border-champagne/10 px-3 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-champagne/40 transition hover:border-gold/40 hover:text-gold"
              >
                Staff login
              </button>
            ) : null}
          </div>
        </div>
      </div>

      <BottomSheet open={info !== null} onClose={() => setInfo(null)}>
        <div className="px-5 pb-6 pt-1">
          {info === 'about' ? (
            <>
              <h2 id="info-about" className="font-display text-2xl italic text-champagne">
                About us
              </h2>
              <GoldDivider className="my-3" />
              <p className="font-body text-[0.9rem] leading-relaxed text-champagne/85">
                {name} is dedicated to quality ingredients, thoughtful preparation, and warm hospitality. Our menu
                changes with the best of the season. Scan your table's code to order - your server is always there to
                help with wine, timing, and special requests.
              </p>
            </>
          ) : null}
          {info === 'contact' ? (
            <>
              <h2 id="info-contact" className="font-display text-2xl italic text-champagne">
                Contact
              </h2>
              <GoldDivider className="my-3" />
              {address ? (
                <p className="mb-3 font-body text-[0.9rem] leading-relaxed text-champagne/85">{address}</p>
              ) : (
                <p className="mb-3 font-body text-[0.9rem] text-[var(--text-muted)]">Address is set in the restaurant back office.</p>
              )}
              {phone ? (
                <a
                  href={`tel:${phone.replace(/\s/g, '')}`}
                  className="inline-block min-h-[44px] font-body text-base font-semibold text-gold underline"
                >
                  {phone}
                </a>
              ) : (
                <p className="font-body text-[0.9rem] text-[var(--text-muted)]">Phone is set in the restaurant back office.</p>
              )}
              {websiteHref ? (
                <a
                  href={websiteHref}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-4 inline-block min-h-[44px] font-body text-base font-semibold text-gold underline"
                >
                  {website}
                </a>
              ) : null}
              {socialMedia ? (
                <p className="mt-3 font-body text-[0.9rem] leading-relaxed text-champagne/75">{socialMedia}</p>
              ) : null}
            </>
          ) : null}
          {info === 'notes' ? (
            <>
              <h2 id="info-notes" className="font-display text-2xl italic text-champagne">
                Notes
              </h2>
              <GoldDivider className="my-3" />
              <p className="font-body text-[0.9rem] leading-relaxed text-champagne/85">
                <strong className="text-gold/90">Allergies &amp; diet:</strong> Please list allergies when you send your
                order. Our team reads every request; always confirm with your server on site.
              </p>
              <p className="mt-3 font-body text-[0.9rem] leading-relaxed text-champagne/80">
                <strong className="text-gold/90">Orders:</strong> The kitchen sees your order as a request. Timing may
                vary during busy service.
              </p>
              {taxLegal ? (
                <p className="mt-3 font-body text-[0.85rem] leading-relaxed text-champagne/60">{taxLegal}</p>
              ) : null}
            </>
          ) : null}
          <button
            type="button"
            onClick={() => setInfo(null)}
            className="mt-6 w-full min-h-[48px] rounded-xl border border-gold/30 font-body text-sm font-semibold uppercase tracking-wider text-gold"
          >
            Close
          </button>
        </div>
      </BottomSheet>
    </div>
  )
}
