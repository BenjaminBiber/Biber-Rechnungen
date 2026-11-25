import { Link } from '@tanstack/react-router'
import type { DashboardKennzahl, OffenEintrag } from '@/api/typen'
import { formatDatum, formatEuro } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

/** „Offen & überfällig“: zwei Summenfelder und die offenen Belege nach Fälligkeit. */
export function OffenKarte({
  offen,
  ueberfaellig,
  eintraege,
}: {
  offen: DashboardKennzahl
  ueberfaellig: DashboardKennzahl
  eintraege: OffenEintrag[]
}) {
  return (
    <DashboardKarte
      titel="Offen & überfällig"
      zusatz={
        <Link to="/belege" search={{ ansicht: 'Offen' } as never}>
          Alle offenen
        </Link>
      }
    >
      <div className="flex flex-wrap gap-2.5">
        <div className="grow rounded-[10px] bg-[var(--color-flaeche)] p-3">
          <div className="gedaempft text-xs">Offen</div>
          <div className="num whitespace-nowrap text-left text-lg font-semibold">
            {offen.anzahl} · {formatEuro(offen.betrag ?? 0)}
          </div>
        </div>
        <div className="grow rounded-[10px] bg-[var(--color-rot-flaeche)] p-3 text-[var(--color-rot-text)]">
          <div className="text-xs">Überfällig</div>
          <div className="num whitespace-nowrap text-left text-lg font-semibold">
            {ueberfaellig.anzahl} · {formatEuro(ueberfaellig.betrag ?? 0)}
          </div>
        </div>
      </div>
      {eintraege.map((e) => (
        <Link
          key={e.belegId}
          to="/belege/$belegId"
          params={{ belegId: e.belegId }}
          className="flex items-center gap-2.5 border-t border-[var(--color-linie)] py-2 text-sm text-text no-underline hover:text-text"
        >
          <span className="min-w-0 flex-1">
            <span className="block truncate">{e.korrespondent ?? 'Unbekannter Korrespondent'}</span>
            {e.faelligkeitsdatum && (
              <span
                className="text-xs"
                style={{ color: e.ueberfaellig ? 'var(--color-rot-text)' : 'var(--color-gedaempft-stark)' }}
              >
                {e.ueberfaellig ? 'überfällig seit ' : 'fällig '}
                {formatDatum(e.faelligkeitsdatum)}
              </span>
            )}
          </span>
          <span className="num font-medium">{e.brutto != null ? formatEuro(e.brutto) : '–'}</span>
        </Link>
      ))}
      <p className="gedaempft m-0 text-xs">
        „Offen“ = kein Zahlungs-Tag gesetzt. „Überfällig“ = offen und Fälligkeit überschritten.
      </p>
    </DashboardKarte>
  )
}
