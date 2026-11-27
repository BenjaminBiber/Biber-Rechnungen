import { Link } from '@tanstack/react-router'
import type { SkontoUebersicht } from '@/api/typen'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

/** Tage von `heute` (lokales Datum) bis zum Datum „JJJJ-MM-TT“. */
function tageBis(iso: string, heute: Date): number {
  const [j, m, t] = iso.split('-').map(Number)
  const von = Date.UTC(heute.getFullYear(), heute.getMonth(), heute.getDate())
  return Math.round((Date.UTC(j, m - 1, t) - von) / 86_400_000)
}

/** „nur noch heute“, „bis morgen“ oder „bis TT.MM.JJJJ“. */
function fristText(iso: string, heute: Date): string {
  const tage = tageBis(iso, heute)
  if (tage <= 0) return 'nur noch heute'
  if (tage === 1) return 'bis morgen'
  return `bis ${formatDatum(iso)}`
}

/** „Skonto“: offene Rechnungen mit laufender Skontofrist, die nächste Frist zuerst; endet sie bis morgen, orange. */
export function SkontoKarte({ skonto, heute }: { skonto: SkontoUebersicht; heute: Date }) {
  return (
    <DashboardKarte titel="Skonto">
      <div className="flex flex-wrap gap-2.5">
        <div className="grow rounded-[10px] bg-[var(--color-flaeche)] p-3">
          <div className="gedaempft text-xs">Mit laufender Frist</div>
          <div className="num whitespace-nowrap text-left text-lg font-semibold">
            {skonto.anzahl} {skonto.anzahl === 1 ? 'Rechnung' : 'Rechnungen'}
          </div>
        </div>
        <div className="grow rounded-[10px] bg-[var(--color-gruen-flaeche)] p-3 text-[var(--color-gruen-text)]">
          <div className="text-xs">Mögliche Ersparnis</div>
          <div className="num whitespace-nowrap text-left text-lg font-semibold">{formatEuro(skonto.ersparnis)}</div>
        </div>
      </div>
      {skonto.eintraege.length === 0 && (
        <p className="gedaempft m-0 text-sm">Keine offenen Rechnungen mit laufender Skontofrist.</p>
      )}
      {skonto.eintraege.map((e) => {
        const dringend = tageBis(e.bis, heute) <= 1
        return (
          <Link
            key={e.belegId}
            to="/belege/$belegId"
            params={{ belegId: e.belegId }}
            className="flex items-center gap-2.5 border-t border-[var(--color-linie)] py-2 text-sm text-text no-underline hover:text-text"
          >
            <span className="min-w-0 flex-1">
              <span className="block truncate">{e.korrespondent ?? 'Unbekannter Korrespondent'}</span>
              <span
                className="text-xs"
                style={{ color: dringend ? 'var(--color-orange-text)' : 'var(--color-gedaempft-stark)' }}
              >
                {formatZahl(e.prozent)} % {fristText(e.bis, heute)}
              </span>
            </span>
            <span className="flex shrink-0 flex-col items-end">
              <span className="num font-medium text-[var(--color-gruen-text)]">
                {e.betrag != null ? `−${formatEuro(e.betrag)}` : '–'}
              </span>
              {e.brutto != null && <span className="num gedaempft text-xs">von {formatEuro(e.brutto)}</span>}
            </span>
          </Link>
        )
      })}
      <p className="gedaempft m-0 text-xs">Nur offene Rechnungen, deren Skontofrist heute oder später endet.</p>
    </DashboardKarte>
  )
}
