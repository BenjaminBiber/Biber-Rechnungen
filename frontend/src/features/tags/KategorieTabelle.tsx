import type { Kategorie } from '@/api/typen'
import { formatEuro } from '@/lib/format'
import { matchingKurztext } from '@/lib/matching'

/** Tabelle des Reiters „Kategorien“ mit Belegen und Summe im Jahr. */
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
        {kategorien.map((k) => (
          <tr key={k.id} className={k.id === gewaehlt ? 'bg-[var(--color-akzent-zart)]' : undefined}>
            <td>
              <span className="inline-flex items-center gap-2 font-medium">
                <span aria-hidden="true" className="size-2.5 rounded-[2px]" style={{ background: k.farbe }} />
                {k.name}
              </span>
            </td>
            <td className={`text-[13px] ${k.euerZeile ? '' : 'text-gedaempft'}`}>{k.euerZeile || 'nicht gesetzt'}</td>
            <td className="text-[13px] text-gedaempft-stark">{matchingKurztext(k.matching)}</td>
            <td className="num">{k.anzahlBelege}</td>
            <td className="num font-semibold">{formatEuro(k.summeBrutto)}</td>
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
        ))}
      </tbody>
    </table>
  )
}
