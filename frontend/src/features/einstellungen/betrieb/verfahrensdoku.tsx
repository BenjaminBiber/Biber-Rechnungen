import { useVerfahrensdoku, verfahrensdokuPdfUrl } from '@/api/verfahrensdoku'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatDatum } from '@/lib/format'
import { Zeile } from '../Zeile'

/** Bereich „Verfahrensdokumentation“ (Einstellungen.dc.html → `s.doku`). */
export function VerfahrensdokuBereich() {
  const { data, isError, error } = useVerfahrensdoku()
  const nichtGefunden = isError && (error as { status?: number }).status === 404

  return (
    <div className="flex flex-col gap-3.5">
      <h2 className="m-0 text-lg font-semibold text-text">Verfahrensdokumentation</h2>
      <p className="gedaempft m-0 text-sm">
        Liegt versioniert im Repository unter docs/verfahrensdokumentation/. Technische Abschnitte werden mit jeder
        Version aktualisiert.
      </p>
      {data ? (
        <div>
          <Zeile
            titel={`Version ${data.version}`}
            beschreibung={
              `Stand ${formatDatum(data.stand)}` +
              (data.offeneStellen > 0 ? ` · enthält noch ${data.offeneStellen} offene [ERGÄNZEN]-Stellen` : '')
            }
          >
            <a className="btn btn-klein" href={verfahrensdokuPdfUrl()} target="_blank" rel="noopener noreferrer">
              <Icon name="picture_as_pdf" />
              Als PDF anzeigen
            </a>
          </Zeile>
          <Zeile titel="Software-Version" beschreibung={data.softwareVersion}>
            <Chip ton="grau">aktuell</Chip>
          </Zeile>
        </div>
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {nichtGefunden ? 'Verfahrensdokumentation nicht gefunden' : error.message}
        </p>
      ) : (
        <p className="gedaempft m-0 text-sm">Lädt …</p>
      )}
    </div>
  )
}
