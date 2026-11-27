import { Link } from '@tanstack/react-router'
import type { Kauf } from '@/api/typen'
import { MerkmalChips } from '@/components/MerkmalChips'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'
import { merkmaleAufteilen } from '@/lib/merkmale'

/**
 * Karte „Käufe“ mit Link auf den Beleg je Kauf. Merkmale, die sich zwischen den Käufen unterscheiden (z. B. die
 * Größe), bekommen je eine Spalte; gleiche stehen einmal über der Tabelle.
 */
export function Kaeufe({ kaeufe }: { kaeufe: Kauf[] }) {
  const { spalten, gemeinsam, werte } = merkmaleAufteilen(kaeufe)
  const gemeinsamTitel = kaeufe.length > 1 ? 'Bei allen Käufen' : 'Merkmale'
  return (
    <section className="karte overflow-x-auto" aria-labelledby="kaeufe-titel">
      <h2 id="kaeufe-titel" className="m-0 px-5 pt-4 pb-3 text-base font-semibold">
        Käufe
      </h2>
      {gemeinsam.length > 0 && (
        <div className="-mt-1 flex flex-wrap items-center gap-2 px-5 pb-3 text-[13px]">
          <span className="gedaempft">{gemeinsamTitel}</span>
          <MerkmalChips merkmale={gemeinsam} label={gemeinsamTitel} />
        </div>
      )}
      <table className="tabelle" aria-labelledby="kaeufe-titel">
        <thead>
          <tr>
            <th scope="col">Datum</th>
            <th scope="col">Korrespondent</th>
            <th scope="col">Beleg</th>
            {spalten.map((s) => (
              <th scope="col" key={s.name}>
                {s.titel}
              </th>
            ))}
            <th scope="col" className="text-right">
              Menge
            </th>
            <th scope="col" className="text-right">
              Einzelpreis
            </th>
            <th scope="col" className="text-right">
              Grundpreis
            </th>
          </tr>
        </thead>
        <tbody>
          {kaeufe.map((k, i) => (
            // Ein Beleg kann denselben Artikel mehrfach enthalten (z. B. zwei Größen).
            <tr key={`${k.belegId}-${i}`}>
              <td className="num">{formatDatum(k.datum)}</td>
              <td>{k.korrespondent.name}</td>
              <td>
                <Link to="/belege/$belegId" params={{ belegId: k.belegId }} className="mono">
                  {k.rechnungsnummer ?? 'Beleg'}
                </Link>
              </td>
              {spalten.map((s) => (
                <td key={s.name}>{werte[i]?.get(s.name) ?? '–'}</td>
              ))}
              <td className="num text-right">{k.menge != null ? `${formatZahl(k.menge)} ${k.einheit ?? ''}` : '–'}</td>
              <td className="num text-right">{k.einzelpreis != null ? formatEuro(k.einzelpreis) : '–'}</td>
              <td className="num text-right">{k.grundpreis != null ? formatEuro(k.grundpreis) : '–'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
