import type { Kategorie } from '@/api/typen'
import { formatEuro } from '@/lib/format'
import { kategorieBaum } from '@/lib/kategorien'
import { matchingKurztext } from '@/lib/matching'

/** Tabelle des Reiters „Kategorien“ mit Belegen und Summe im Jahr; Unterkategorien eingerückt unter ihrer Hauptkategorie. */
export function KategorieTabelle({
  kategorien,
  jahr,
  gewaehlt,
  onBearbeiten,
}: {
  kategorien: Kategorie[]
  jahr: number
  gewaehlt?: string
  onBearbeiten?: (id: string) => void
}) {
  // Hauptkategorien zählen ihre Unterkategorien mit.
  const kennzahlen = (k: Kategorie) => {
    const unter = kategorien.filter((u) => u.elternId === k.id)
    return {
      anzahl: unter.reduce((s, u) => s + u.anzahlBelege, k.anzahlBelege),
      summe: unter.reduce((s, u) => s + u.summeBrutto, k.summeBrutto),
      inklusive: unter.length > 0 ? 'inklusive Unterkategorien' : undefined,
    }
  }

  return (
    <table className="tabelle min-w-[640px]">
      <thead>
        <tr>
          <th scope="col">Kategorie</th>
          <th scope="col">EÜR-Zeile</th>
          <th scope="col">Matching</th>
          <th scope="col" className="num">
            Belege {jahr}
          </th>
          <th scope="col" className="num">
            Summe {jahr}
          </th>
          {onBearbeiten && (
            <th scope="col">
              <span className="sr-only">Bearbeiten</span>
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {kategorieBaum(kategorien).map(({ kategorie: k, ebene }) => {
          const { anzahl, summe, inklusive } = kennzahlen(k)
          const geerbt = ebene > 0 && !k.euerZeile ? kategorien.find((e) => e.id === k.elternId)?.euerZeile : null
          return (
            <tr key={k.id} className={k.id === gewaehlt ? 'bg-[var(--color-akzent-zart)]' : undefined}>
              <td>
                <span className={`inline-flex items-center gap-2 ${ebene > 0 ? 'pl-4' : 'font-medium'}`}>
                  {ebene > 0 && (
                    <span aria-hidden="true" className="text-gedaempft">
                      └
                    </span>
                  )}
                  <span aria-hidden="true" className="size-2.5 rounded-[2px]" style={{ background: k.farbe }} />
                  {k.name}
                </span>
              </td>
              <td className={`text-[13px] ${k.euerZeile ? '' : 'text-gedaempft'}`}>
                {k.euerZeile || (geerbt ? `${geerbt} (geerbt)` : 'nicht gesetzt')}
              </td>
              <td className="text-[13px] text-gedaempft-stark">{matchingKurztext(k.matching)}</td>
              <td className="num" title={inklusive}>
                {anzahl}
              </td>
              <td className="num font-semibold" title={inklusive}>
                {formatEuro(summe)}
              </td>
              {onBearbeiten && (
                <td className="text-right">
                  <button
                    type="button"
                    className="btn btn-klein"
                    aria-label={`${k.name} bearbeiten`}
                    onClick={() => onBearbeiten(k.id)}
                  >
                    Bearbeiten
                  </button>
                </td>
              )}
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
