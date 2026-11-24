import { istApiFehler } from '@/api/client'
import { exportDateiHerunterladen, exportDateiUrl, useExportLaeufe } from '@/api/export'
import { useToast } from '@/components/toast'
import { formatDatumZeit } from '@/lib/format'
import { exportFehlertext } from './fehler'

/** Tabelle „Letzte Exporte“ mit Download je Lauf; fehlt die Datei auf dem Server (410), erscheint ein Hinweis. */
export function LetzteExporte() {
  const toast = useToast()
  const { data: laeufe = [], isPending } = useExportLaeufe()
  return (
    <section className="karte mt-6 overflow-hidden" aria-labelledby="letzte-exporte">
      <div className="px-5 py-4">
        <h2 id="letzte-exporte" className="m-0 text-base font-semibold text-text">
          Letzte Exporte
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="tabelle">
          <thead>
            <tr>
              <th scope="col">Erstellt</th>
              <th scope="col">Zeitraum</th>
              <th scope="col">Inhalt</th>
              <th scope="col" className="text-right">
                Belege
              </th>
              <th scope="col">Von</th>
              <th scope="col">
                <span className="sr-only">Herunterladen</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {laeufe.map((lauf) => (
              <tr key={lauf.id}>
                <td className="whitespace-nowrap">{formatDatumZeit(lauf.erstelltAm).replace(',', '')}</td>
                <td>{lauf.bezeichnung}</td>
                <td>{lauf.inhalt}</td>
                <td className="text-right tabular-nums">{lauf.anzahlBelege}</td>
                {/* Der Export-Lauf nennt keinen Benutzer. */}
                <td className="gedaempft">—</td>
                <td>
                  <a
                    className="btn btn-klein"
                    href={exportDateiUrl(lauf.id)}
                    download
                    onClick={(e) => {
                      e.preventDefault()
                      exportDateiHerunterladen(lauf.id).catch((f: unknown) =>
                        toast.fehler(
                          istApiFehler(f) && f.status === 410
                            ? exportFehlertext(f, lauf.bezeichnung)
                            : `${lauf.bezeichnung} konnte nicht heruntergeladen werden.`,
                        ),
                      )
                    }}
                    aria-label={`${lauf.bezeichnung} herunterladen`}
                  >
                    Herunterladen
                  </a>
                </td>
              </tr>
            ))}
            {!isPending && laeufe.length === 0 && (
              <tr>
                <td colSpan={6} className="gedaempft">
                  Noch keine Exporte.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}
