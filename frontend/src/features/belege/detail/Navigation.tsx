import { Link } from '@tanstack/react-router'
import { useBelegNachbarn } from '@/api/belege'
import type { BelegeAbfrage } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { ansichtNamen } from './gemeinsam'

const pfeil =
  'inline-flex size-6 items-center justify-center rounded-md text-gedaempft-stark no-underline hover:bg-hover disabled:opacity-40'

/** Zurück zur Belegliste – mit den Filtern der Liste, aus der der Beleg geöffnet wurde. */
export function ZurueckKnopf({ aus }: { aus?: BelegeAbfrage }) {
  const ziel = aus?.ansicht ? ansichtNamen[aus.ansicht] : 'Belege'
  return (
    <Link
      to="/belege"
      search={(aus ?? {}) as never}
      aria-label={`Zurück zu ${ziel}`}
      title={`Zurück zu ${ziel}`}
      className="inline-flex size-8 flex-none items-center justify-center rounded-md text-gedaempft-stark no-underline hover:bg-hover hover:text-text"
    >
      <Icon name="arrow_back" className="text-[22px]" />
    </Link>
  )
}

/** „3 von 12 in Inbox“ mit Vor/Zurück – nur, wenn der Beleg aus der Liste geöffnet wurde. */
export function Blaettern({ belegId, aus }: { belegId: string; aus: BelegeAbfrage }) {
  const { data } = useBelegNachbarn(belegId, aus)
  if (!data || data.position < 1) return null
  const ansicht = aus.ansicht ? ` in ${ansichtNamen[aus.ansicht]}` : ''
  const ziel = (id: string, label: string, icon: string) => (
    <Link to="/belege/$belegId" params={{ belegId: id }} search={{ aus }} aria-label={label} className={pfeil}>
      <Icon name={icon} />
    </Link>
  )
  return (
    <nav aria-label="Blättern" className="gedaempft flex items-center gap-1 text-[13px] whitespace-nowrap">
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
    </nav>
  )
}
