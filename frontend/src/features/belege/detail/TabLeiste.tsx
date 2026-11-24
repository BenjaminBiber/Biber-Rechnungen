import { Link } from '@tanstack/react-router'
import type { BelegDetail } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { fehlgeschlageneBefunde } from './gemeinsam'

const tabKlasse =
  'inline-flex h-[46px] flex-none items-center gap-1.5 whitespace-nowrap border-0 border-b-2 border-transparent px-2.5 text-sm font-medium text-gedaempft-stark no-underline hover:text-text'
const aktivKlasse = 'border-akzent text-text'

/** Tab-Leiste des Beleg-Details; jeder Tab ist eine verschachtelte Route. */
export function TabLeiste({ beleg }: { beleg: BelegDetail }) {
  const befunde = fehlgeschlageneBefunde(beleg).length
  const tabs = [
    { to: '/belege/$belegId', label: 'Daten' },
    { to: '/belege/$belegId/positionen', label: 'Positionen', zaehler: beleg.positionen.length },
    { to: '/belege/$belegId/pruefung', label: 'Prüfung', zaehler: befunde || undefined, orange: true },
    { to: '/belege/$belegId/historie', label: 'Historie' },
    { to: '/belege/$belegId/dateien', label: 'Dateien', zaehler: beleg.dateien.length },
    { to: '/belege/$belegId/dubletten', label: 'Dubletten' },
  ] as const

  return (
    <nav aria-label="Belegdaten" className="flex gap-0.5 overflow-x-auto border-b border-rand px-3">
      {tabs.map((tab) => (
        <Link
          key={tab.to}
          to={tab.to}
          params={{ belegId: beleg.id }}
          search={(alt: Record<string, unknown>) => ({ aus: alt.aus }) as never}
          activeOptions={{ exact: true, includeSearch: false }}
          className={tabKlasse}
          activeProps={{ className: aktivKlasse, 'aria-current': 'page' }}
        >
          {tab.label}
          {'zaehler' in tab && tab.zaehler !== undefined && (
            <Chip ton={'orange' in tab ? 'orange' : 'grau'}>
              {tab.zaehler}
              {'orange' in tab && <span className="sr-only"> fehlgeschlagen</span>}
            </Chip>
          )}
        </Link>
      ))}
    </nav>
  )
}
