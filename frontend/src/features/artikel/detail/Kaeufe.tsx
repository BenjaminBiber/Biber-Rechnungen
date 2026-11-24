import { Link } from '@tanstack/react-router'
import type { Kauf } from '@/api/typen'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'

/** Karte „Käufe“ mit Link auf den Beleg je Kauf. */
export function Kaeufe({ kaeufe }: { kaeufe: Kauf[] }) {
  return (
    <section className="karte overflow-x-auto" aria-labelledby="kaeufe-titel">
      <h2 id="kaeufe-titel" className="m-0 px-5 pt-4 pb-3 text-base font-semibold">
        Käufe
      </h2>
      <table className="tabelle" aria-labelledby="kaeufe-titel">
        <thead>
          <tr>
            <th scope="col">Datum</th>
            <th scope="col">Korrespondent</th>
            <th scope="col">Beleg</th>
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
          {kaeufe.map((k) => (
            <tr key={`${k.belegId}-${k.datum}-${k.korrespondent.id}`}>
              <td className="num">{formatDatum(k.datum)}</td>
              <td>{k.korrespondent.name}</td>
              <td>
                <Link to="/belege/$belegId" params={{ belegId: k.belegId }} className="mono">
                  {k.rechnungsnummer ?? 'Beleg'}
                </Link>
              </td>
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
