import { Link } from 'react-router-dom'
import { GoldDivider } from '../ui/GoldDivider'

export function ReservationScreen({ config }) {
  const name = config?.restaurantName ? String(config.restaurantName) : 'Elite Restaurant'
  const phone = config ? String(config.phone ?? config.Phone ?? '').trim() : ''
  const address = config ? String(config.address ?? config.Address ?? '').trim() : ''

  return (
    <main className="relative min-h-[100svh] overflow-hidden bg-midnight px-6 py-8 text-champagne">
      <div className="pointer-events-none absolute -left-1/4 top-0 h-[60vw] w-[60vw] rounded-full bg-[rgba(200,168,76,0.04)] blur-3xl" />
      <div className="pointer-events-none absolute -right-1/4 bottom-0 h-[40vw] w-[40vw] rounded-full bg-[rgba(237,232,220,0.02)] blur-3xl" />

      <section className="relative z-10 mx-auto flex min-h-[calc(100svh-4rem)] max-w-md flex-col justify-center">
        <Link
          to="/"
          className="mb-8 inline-flex min-h-[44px] items-center self-start font-body text-xs font-bold uppercase tracking-[0.18em] text-gold/80 transition hover:text-gold"
        >
          Back
        </Link>

        <div className="rounded-[2rem] border border-champagne/10 bg-midnight-2/80 p-6 shadow-[0_22px_70px_rgba(0,0,0,0.35)]">
          <p className="font-body text-[0.68rem] font-bold uppercase tracking-[0.28em] text-gold/80">
            Reservations
          </p>
          <h1
            className="mt-3 font-display text-4xl italic leading-tight text-champagne"
            style={{ fontFamily: '"Playfair Display", serif' }}
          >
            Reserve your table
          </h1>
          <GoldDivider className="my-5" />

          <p className="font-body text-[0.95rem] leading-relaxed text-champagne/80">
            To reserve your table at {name}, please contact us directly by phone or visit us during service hours.
          </p>

          {phone ? (
            <a
              href={`tel:${phone.replace(/\s/g, '')}`}
              className="mt-6 flex min-h-[52px] items-center justify-center rounded-sm border-2 border-gold/45 bg-gold/5 px-6 py-3 font-body text-sm font-bold uppercase tracking-[0.16em] text-gold transition hover:border-gold hover:bg-[var(--gold-dim)]"
            >
              Call {phone}
            </a>
          ) : (
            <p className="mt-6 rounded-2xl border border-gold/15 bg-gold/5 px-4 py-3 font-body text-sm text-champagne/65">
              Reservation contact is set in the restaurant back office.
            </p>
          )}

          {address ? (
            <p className="mt-5 font-body text-sm leading-relaxed text-champagne/60">{address}</p>
          ) : null}
        </div>
      </section>
    </main>
  )
}
