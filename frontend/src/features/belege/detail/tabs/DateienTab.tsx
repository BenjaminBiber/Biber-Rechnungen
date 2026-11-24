import type { BelegDatei, BelegDetail, DateiRolle } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { formatDatum, formatDatumZeit, formatZahl } from '@/lib/format'

const rollenNamen: Record<DateiRolle, string> = {
  Original: 'Original',
  EingebettetesXml: 'Eingebettetes XML',
  Visualisierung: 'Visualisierung',
  Vorschau: 'Vorschau',
}

function icon(datei: BelegDatei): string {
  if (datei.mimeType === 'application/pdf') return 'picture_as_pdf'
  if (datei.mimeType.startsWith('image/')) return 'image'
  if (/xml/i.test(datei.mimeType)) return 'code'
  if (datei.mimeType === 'text/html') return 'html'
  return 'description'
}

/** Status-Chip: nur das Original trägt eine Prüfsumme, alles andere ist abgeleitet. */
export function dateiStatus(datei: BelegDatei): { text: string; ton: ChipTon } {
  if (datei.rolle !== 'Original') return { text: 'abgeleitet', ton: 'grau' }
  if (datei.pruefsummeOk === true) return { text: 'Prüfsumme ok', ton: 'gruen' }
  if (datei.pruefsummeOk === false) return { text: 'Abweichung', ton: 'rot' }
  return { text: 'noch nicht geprüft', ton: 'grau' }
}

/** „9f2c41d7…a83be41a“ */
export function kurzHash(sha256: string): string {
  return sha256.length > 16 ? `${sha256.slice(0, 8)}…${sha256.slice(-8)}` : sha256
}

export function groesseText(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${formatZahl(bytes / 1024 / 1024, 1)} MB`
}

/** Tab „Dateien“: je Datei eine Karte mit Pfad, Prüfsumme und Download; darunter der Aufbewahrungshinweis. */
export function DateienTab({ beleg }: { beleg: BelegDetail }) {
  const toast = useToast()

  async function kopieren(sha256: string) {
    try {
      await navigator.clipboard.writeText(sha256)
      toast.erfolg('Prüfsumme kopiert')
    } catch {
      toast.fehler('Prüfsumme konnte nicht kopiert werden')
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {beleg.dateien.map((datei) => {
        const status = dateiStatus(datei)
        return (
          <article
            key={datei.id}
            aria-label={rollenNamen[datei.rolle]}
            className="flex flex-col gap-2 rounded-[10px] border border-rand p-3.5"
          >
            <div className="flex flex-wrap items-center gap-2">
              <Icon name={icon(datei)} />
              <strong className="text-sm">{rollenNamen[datei.rolle]}</strong>
              <Chip ton="grau">{datei.mimeType}</Chip>
              <span className="flex-1" />
              <Chip ton={status.ton}>{status.text}</Chip>
            </div>
            <div className="mono break-all">{datei.pfad}</div>
            <div className="gedaempft flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
              <span>
                SHA-256{' '}
                <button
                  type="button"
                  className="mono cursor-pointer border-0 bg-transparent p-0 text-xs text-inherit underline decoration-dotted"
                  title={datei.sha256}
                  aria-label={`SHA-256 ${kurzHash(datei.sha256)} kopieren`}
                  onClick={() => void kopieren(datei.sha256)}
                >
                  {kurzHash(datei.sha256)}
                </button>
              </span>
              <span>{groesseText(datei.groesse)}</span>
              <span>
                abgelegt {formatDatumZeit(datei.abgelegtAm).replace(',', '')} ·{' '}
                {datei.letztePruefungAm
                  ? `geprüft ${formatDatumZeit(datei.letztePruefungAm).replace(',', '')}`
                  : datei.rolle === 'Original'
                    ? 'noch nicht geprüft'
                    : 'kein Original'}
              </span>
              <a href={datei.url} download className="inline-flex items-center gap-1">
                <Icon name="download" />
                Herunterladen
              </a>
            </div>
          </article>
        )
      })}
      <div className="flex gap-2.5 rounded-[10px] bg-flaeche px-3.5 py-3 text-[13px] text-gedaempft-stark">
        <Icon name="lock_clock" />
        <span>
          Aufbewahrung: Löschsperre bis <strong className="text-text">{formatDatum(beleg.loeschsperreBis)}</strong>
          {beleg.rechnungsdatum &&
            ` (${Number(beleg.loeschsperreBis.slice(0, 4)) - Number(beleg.rechnungsdatum.slice(0, 4))} Jahre nach Belegjahr ${beleg.rechnungsdatum.slice(0, 4)})`}
          . Der Ablagepfad bleibt fest, auch wenn Daten korrigiert werden.
        </span>
      </div>
    </div>
  )
}
