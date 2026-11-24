import type { BelegListe } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatEuro } from '@/lib/format'
import { cn } from '@/lib/utils'
import { gruppeGrau } from './bausteine'

/**
 * Seitenzahlen mit Auslassung: erste, letzte und die Nachbarn der aktuellen Seite,
 * z. B. Seite 1 von 21 → 1 2 3 … 21. Eine einzelne Lücke wird als Zahl gezeigt, nicht als „…“.
 */
export function seitenListe(aktuell: number, gesamt: number): (number | '…')[] {
  const seiten = new Set([1, gesamt, aktuell - 1, aktuell, aktuell + 1])
  if (aktuell <= 3) [2, 3].forEach((s) => seiten.add(s))
  if (aktuell >= gesamt - 2) [gesamt - 2, gesamt - 1].forEach((s) => seiten.add(s))
  const sortiert = [...seiten].filter((s) => s >= 1 && s <= gesamt).sort((a, b) => a - b)
  const ergebnis: (number | '…')[] = []
  sortiert.forEach((s, i) => {
    const vorher = sortiert[i - 1]
    if (vorher !== undefined && s - vorher === 2) ergebnis.push(s - 1)
    else if (vorher !== undefined && s - vorher > 2) ergebnis.push('…')
    ergebnis.push(s)
  })
  return ergebnis
}

/** „248 Belege · Seite 1 von 21 · Summe 20.845,00 €“ und die Seitennavigation. */
export function Zusammenfassung({ liste, onSeite }: { liste: BelegListe; onSeite: (seite: number) => void }) {
  const seiten = Math.max(1, Math.ceil(liste.gesamt / liste.seitengroesse))
  const seite = Math.min(liste.seite, seiten)
  const anzahl = liste.gesamt === 1 ? '1 Beleg' : `${liste.gesamt} Belege`

  return (
    <div className="flex flex-wrap items-center justify-between gap-2.5 text-sm">
      <span>
        {anzahl} · Seite {seite} von {seiten}
        <span className="gedaempft"> · Summe {formatEuro(liste.summeBrutto)}</span>
      </span>
      {seiten > 1 && (
        <nav aria-label="Seiten" className={gruppeGrau}>
          <button
            type="button"
            aria-label="Erste Seite"
            disabled={seite <= 1}
            onClick={() => onSeite(1)}
            className="!w-8 !px-0"
          >
            <Icon name="first_page" />
          </button>
          <button
            type="button"
            aria-label="Vorherige Seite"
            disabled={seite <= 1}
            onClick={() => onSeite(seite - 1)}
            className="!w-8 !px-0"
          >
            <Icon name="chevron_left" />
          </button>
          {seitenListe(seite, seiten).map((s, i) =>
            s === '…' ? (
              <button key={`luecke-${i}`} type="button" disabled aria-hidden="true" className="!w-8 !px-0">
                …
              </button>
            ) : (
              <button
                key={s}
                type="button"
                aria-label={`Seite ${s}`}
                aria-current={s === seite ? 'page' : undefined}
                onClick={() => onSeite(s)}
                className={cn('!w-8 !px-0', s === seite && '!bg-akzent font-semibold !text-auf-akzent')}
              >
                {s}
              </button>
            ),
          )}
          <button
            type="button"
            aria-label="Nächste Seite"
            disabled={seite >= seiten}
            onClick={() => onSeite(seite + 1)}
            className="!w-8 !px-0"
          >
            <Icon name="chevron_right" />
          </button>
          <button
            type="button"
            aria-label="Letzte Seite"
            disabled={seite >= seiten}
            onClick={() => onSeite(seiten)}
            className="!w-8 !px-0"
          >
            <Icon name="last_page" />
          </button>
        </nav>
      )}
    </div>
  )
}
