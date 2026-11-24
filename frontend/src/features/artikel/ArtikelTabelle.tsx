import { Link } from '@tanstack/react-router'
import type { ArtikelListeneintrag } from '@/api/typen'
import { formatDatum, formatEuro } from '@/lib/format'
import { AenderungChip } from './AenderungChip'

const kopf = 'px-3 py-2 text-left text-xs font-semibold tracking-wide text-gedaempft uppercase'
const zelle = 'px-3 py-2.5 align-middle'

/**
 * Artikeltabelle: Auswahl (nur mit Bearbeiten-Recht), Artikel, Korrespondent, Stammartikel, Käufe,
 * letzter Einzelpreis, Grundpreis, „Seit 1. Kauf“ und Zuletzt.
 */
export function ArtikelTabelle({
  eintraege,
  gewaehlt,
  onUmschalten,
}: {
  eintraege: ArtikelListeneintrag[]
  gewaehlt?: string[]
  onUmschalten?: (artikel: ArtikelListeneintrag) => void
}) {
  const mitAuswahl = gewaehlt !== undefined && onUmschalten !== undefined
  return (
    <div className="karte overflow-x-auto">
      <table className="w-full border-collapse text-sm">
        <thead className="border-b border-rand">
          <tr>
            {mitAuswahl && (
              <th className={`${kopf} w-10`}>
                <span className="sr-only">Auswahl</span>
              </th>
            )}
            <th className={kopf}>Artikel</th>
            <th className={kopf}>Korrespondent</th>
            <th className={kopf}>Stammartikel</th>
            <th className={`${kopf} text-right`}>Käufe</th>
            <th className={`${kopf} text-right`}>Letzter Einzelpreis</th>
            <th className={`${kopf} text-right`}>Grundpreis</th>
            <th className={kopf}>Seit 1. Kauf</th>
            <th className={kopf}>Zuletzt</th>
          </tr>
        </thead>
        <tbody>
          {eintraege.map((a) => (
            <tr key={a.id} className="border-b border-rand last:border-b-0 hover:bg-hover">
              {mitAuswahl && (
                <td className={zelle}>
                  <input
                    type="checkbox"
                    aria-label={`${a.name} auswählen`}
                    checked={gewaehlt.includes(a.id)}
                    onChange={() => onUmschalten(a)}
                    className="size-4 cursor-pointer accent-[var(--akzent)]"
                  />
                </td>
              )}
              <td className={`${zelle} font-medium`}>
                <Link
                  to="/artikel/$artikelId"
                  params={{ artikelId: a.id }}
                  className="text-text no-underline hover:underline"
                >
                  {a.name}
                </Link>
              </td>
              <td className={zelle}>{a.korrespondent.name}</td>
              <td className={zelle}>
                {a.stammartikel ? (
                  <Link
                    to="/artikel/stammartikel/$stammartikelId"
                    params={{ stammartikelId: a.stammartikel.id }}
                    className="text-akzent-text no-underline hover:underline"
                  >
                    {a.stammartikel.name}
                  </Link>
                ) : (
                  <span className="gedaempft">nicht zugeordnet</span>
                )}
              </td>
              <td className={`${zelle} text-right tabular-nums`}>{a.kaeufe}</td>
              <td className={`${zelle} text-right whitespace-nowrap tabular-nums`}>
                {a.letzterEinzelpreis != null ? (
                  <>
                    {formatEuro(a.letzterEinzelpreis)}
                    {a.einheit && <span className="gedaempft"> / {a.einheit}</span>}
                  </>
                ) : (
                  '—'
                )}
              </td>
              <td className={`${zelle} text-right whitespace-nowrap tabular-nums`}>
                {a.grundpreis != null ? (
                  <>
                    {formatEuro(a.grundpreis)}
                    {a.grundpreisEinheit && <span className="gedaempft"> / {a.grundpreisEinheit}</span>}
                  </>
                ) : (
                  <span className="gedaempft">—</span>
                )}
              </td>
              <td className={zelle}>
                <AenderungChip prozent={a.aenderungSeitErstkauf} />
              </td>
              <td className={`${zelle} whitespace-nowrap`}>{a.zuletztGekauft ? formatDatum(a.zuletztGekauft) : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
