import { Link } from '@tanstack/react-router'
import { useBelegNachbarn } from '@/api/belege'
import type { BelegDetail, BelegeAbfrage } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { ansichtNamen } from './gemeinsam'

const pfeil =
  'inline-flex size-8 items-center justify-center rounded-md text-gedaempft-stark no-underline hover:bg-hover disabled:opacity-40'

/** „3 von 12 in Inbox“ mit Vor/Zurück – nur, wenn der Beleg aus der Liste geöffnet wurde. */
function Blaettern({ belegId, aus }: { belegId: string; aus: BelegeAbfrage }) {
  const { data } = useBelegNachbarn(belegId, aus)
  if (!data || data.position < 1) return null
  const ansicht = aus.ansicht ? ` in ${ansichtNamen[aus.ansicht]}` : ''
  const ziel = (id: string, label: string, icon: string) => (
    <Link to="/belege/$belegId" params={{ belegId: id }} search={{ aus }} aria-label={label} className={pfeil}>
      <Icon name={icon} />
    </Link>
  )
  return (
    <span className="gedaempft flex items-center gap-1">
      {data.position} von {data.gesamt}
      {ansicht}
      {data.vorherId ? (
        ziel(data.vorherId, 'Vorheriger Beleg', 'chevron_left')
      ) : (
        <button type="button" disabled aria-label="Vorheriger Beleg" className={`${pfeil} border-0 bg-transparent`}>
          <Icon name="chevron_left" />
        </button>
      )}
      {data.naechsteId ? (
        ziel(data.naechsteId, 'Nächster Beleg', 'chevron_right')
      ) : (
        <button type="button" disabled aria-label="Nächster Beleg" className={`${pfeil} border-0 bg-transparent`}>
          <Icon name="chevron_right" />
        </button>
      )}
    </span>
  )
}

/** „Belege / <Ansicht> / <Rechnungsnr.>“ und Blättern in der Liste, aus der der Beleg kam. */
export function Brotkrumen({ beleg, aus }: { beleg: BelegDetail; aus?: BelegeAbfrage }) {
  const ansicht = aus?.ansicht ? ansichtNamen[aus.ansicht] : null
  return (
    <nav aria-label="Brotkrumen" className="flex flex-wrap items-center justify-between gap-2 text-[13px]">
      <span>
        <Link to="/belege" search={(aus ?? {}) as never} className="no-underline">
          Belege
        </Link>{' '}
        <span className="gedaempft">/ {ansicht && `${ansicht} /`}</span>{' '}
        <span className="mono text-xs">{beleg.rechnungsnummer ?? beleg.titel}</span>
      </span>
      {aus && <Blaettern belegId={beleg.id} aus={aus} />}
    </nav>
  )
}
