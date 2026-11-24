import { Link } from '@tanstack/react-router'
import type { DiVerbrauch } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatZahl } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

/** Ab diesem Anteil des Monatslimits wird der Verbrauch orange dargestellt. */
export const DI_WARNSCHWELLE = 80

/** „Document Intelligence“: Seitenverbrauch des Monats gegen das Limit. */
export function DiVerbrauchKarte({ verbrauch }: { verbrauch: DiVerbrauch }) {
  const warnung = verbrauch.prozent >= DI_WARNSCHWELLE
  const farbe = warnung ? 'var(--color-orange-text)' : 'var(--color-akzent)'
  return (
    <DashboardKarte
      titel="Document Intelligence"
      zusatz={<Link to="/einstellungen/document-intelligence">Einstellungen</Link>}
    >
      <div className="num text-left text-lg font-semibold" style={{ color: warnung ? farbe : undefined }}>
        {formatZahl(verbrauch.seiten, 0)} / {formatZahl(verbrauch.limit, 0)} Seiten · {verbrauch.prozent} %
      </div>
      <div
        className="h-2 rounded bg-[var(--color-linie)]"
        role="meter"
        aria-label="Verbrauch Document Intelligence"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={verbrauch.prozent}
      >
        <span
          className="block h-2 rounded"
          data-testid="di-balken"
          data-warnung={warnung}
          style={{ width: `${Math.min(100, verbrauch.prozent)}%`, background: farbe }}
        />
      </div>
      {warnung && <p className="m-0 text-xs text-[var(--color-orange-text)]">Warnschwelle erreicht</p>}
      {verbrauch.pausiert && (
        <p className="m-0 flex items-center gap-1.5 text-sm text-[var(--color-orange-text)]">
          <Icon name="pause_circle" />
          Warteschlange pausiert
          {verbrauch.wartend > 0 && ` · ${verbrauch.wartend} wartend`}
        </p>
      )}
    </DashboardKarte>
  )
}
