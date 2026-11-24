import { Link } from '@tanstack/react-router'
import type { ZuPruefenEintrag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { DashboardKarte } from './gemeinsam'

/** Liste der Belege, deren Prüfung fehlgeschlagen ist. */
export function ZuPruefenKarte({ anzahl, eintraege }: { anzahl: number; eintraege: ZuPruefenEintrag[] }) {
  return (
    <DashboardKarte
      titel={
        <>
          Zu prüfen{' '}
          <span className="ml-1.5">
            <Chip ton="orange">{anzahl}</Chip>
          </span>
        </>
      }
      zusatz={
        <Link to="/belege" search={{ ansicht: 'ZuPruefen' } as never}>
          Liste öffnen
        </Link>
      }
    >
      {eintraege.length === 0 && <p className="gedaempft m-0 text-sm">Nichts zu prüfen.</p>}
      {eintraege.map((e) => (
        <Link
          key={e.belegId}
          to="/belege/$belegId"
          params={{ belegId: e.belegId }}
          className="flex gap-2.5 border-t border-[var(--color-linie)] py-2.5 text-text no-underline hover:text-text"
        >
          <span className="text-[var(--color-orange-text)]">
            <Icon name="report" />
          </span>
          <span className="flex min-w-0 flex-col gap-0.5">
            <span className="text-sm font-medium">
              {e.korrespondent ?? 'Unbekannter Korrespondent'}{' '}
              <span className="mono gedaempft">{e.rechnungsnummer}</span>
            </span>
            <span className="text-[13px] text-[var(--color-orange-text)]">{e.grund}</span>
          </span>
        </Link>
      ))}
    </DashboardKarte>
  )
}
