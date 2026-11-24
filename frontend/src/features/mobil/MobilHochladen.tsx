import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { erlaubteEndungen, formatGroesse } from '@/features/hochladen/pruefung'
import { Verfolgung } from '@/features/hochladen/UploadListe'
import { useUploadWarteschlange, type UploadEintrag } from '@/features/hochladen/uploadWarteschlange'
import { formatDatum } from '@/lib/format'
import { useOnline } from '@/pwa/useOnline'

/** Mobile Ansicht „Hochladen“ (MobilUpload.dc.html): Offline-Banner, Dateiauswahl, Warteschlange. */
export function MobilHochladen() {
  const online = useOnline()
  const { darfBearbeiten } = useRolle()
  const { eintraege, hinzufuegen, anmeldungNoetig } = useUploadWarteschlange()
  const wartend = eintraege.filter((e) => e.status === 'wartet').length
  const sendend = eintraege.filter((e) => e.status === 'laedt').length

  return (
    <div className="flex flex-col">
      <h1 className="sr-only">Hochladen</h1>
      {!online && (
        <div role="status" className="flex items-center gap-2.5 bg-akzent px-4 py-2.5 text-[13px] text-auf-akzent">
          <Icon name="cloud_off" className="text-orange-text" />
          <span className="flex-1">Offline – Uploads werden gesendet, sobald du wieder im LAN bist.</span>
        </div>
      )}
      <div className="flex flex-col gap-3.5 p-4">
        {anmeldungNoetig && wartend > 0 && (
          <p className="hinweis m-0" role="status">
            Bitte anmelden, um {wartend} wartende Uploads zu senden
          </p>
        )}
        {darfBearbeiten ? (
          <DateiAuswahl onDateien={hinzufuegen} />
        ) : (
          <p className="hinweis m-0">Mit deiner Rolle kannst du keine Belege hochladen.</p>
        )}
        {eintraege.length > 0 && (
          <section className="karte px-3.5 py-1.5" aria-labelledby="warteschlange-titel">
            <div className="flex items-center justify-between pt-2.5 pb-1.5">
              <h2 id="warteschlange-titel" className="m-0 text-[15px] font-semibold">
                Warteschlange
              </h2>
              <span className="gedaempft text-[13px]">{zaehlerText(wartend, sendend)}</span>
            </div>
            <ul className="m-0 list-none p-0">
              {eintraege.map((e) => (
                <Zeile key={e.id} eintrag={e} />
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  )
}

/** „3 warten“ bzw. „1 sendet“ – wartende haben Vorrang. */
export function zaehlerText(wartend: number, sendend: number): string {
  if (wartend) return wartend === 1 ? '1 wartet' : `${wartend} warten`
  if (sendend) return sendend === 1 ? '1 sendet' : `${sendend} senden`
  return ''
}

/** Große Auswahlfläche – ohne `capture`: kein Kamera-Scan, nur Dateien. */
function DateiAuswahl({ onDateien }: { onDateien: (dateien: File[]) => void }) {
  return (
    <label className="relative flex cursor-pointer flex-col items-center gap-2 rounded-[16px] border-2 border-dashed border-akzent/40 bg-akzent-zart px-4 py-7 text-center focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-akzent">
      <span className="flex h-14 w-14 items-center justify-center text-akzent-text">
        <Icon name="cloud_upload" className="text-[30px]" />
      </span>
      <strong className="text-base">Dateien auswählen</strong>
      <span className="gedaempft text-[13px]">mehrere gleichzeitig möglich</span>
      <span className="flex flex-wrap justify-center gap-1.5">
        <span className="chip chip-grau">PDF</span>
        <span className="chip chip-grau">ZUGFeRD / Factur-X</span>
        <span className="chip chip-grau">XRechnung (XML)</span>
        <span className="chip chip-grau">PNG · JPG · TIFF</span>
      </span>
      <input
        type="file"
        multiple
        accept={erlaubteEndungen.join(',')}
        aria-label="Dateien auswählen"
        className="absolute h-px w-px opacity-0"
        onChange={(e) => {
          const dateien = Array.from(e.target.files ?? [])
          e.target.value = ''
          if (dateien.length) onDateien(dateien)
        }}
      />
    </label>
  )
}

type Darstellung = { icon: string; farbe: string; status: string }

const wartet: Darstellung = { icon: 'schedule', farbe: 'text-orange-text', status: 'wartet' }
const sendet: Darstellung = { icon: 'progress_activity', farbe: 'text-akzent-text', status: 'sendet' }
const uebertragen: Darstellung = { icon: 'task_alt', farbe: 'text-gruen-text', status: 'übertragen' }

function darstellung(e: UploadEintrag): Darstellung {
  switch (e.status) {
    case 'wartet':
      return wartet
    case 'laedt':
      return sendet
    case 'verarbeitung':
    case 'fertig':
      return uebertragen
    case 'dublette':
      return { icon: 'content_copy', farbe: 'text-orange-text', status: 'Dublette' }
    case 'abgelehnt':
      return { icon: 'error', farbe: 'text-rot-text', status: 'abgelehnt' }
    default:
      return { icon: 'error', farbe: 'text-rot-text', status: 'Fehler' }
  }
}

/** „heute“, „gestern“ oder das Datum. */
function tag(zeitpunkt: number | undefined, jetzt = new Date()): string {
  if (!zeitpunkt) return 'heute'
  const datum = new Date(zeitpunkt)
  const tagesbeginn = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
  const tage = Math.round((tagesbeginn(jetzt) - tagesbeginn(datum)) / 86_400_000)
  if (tage <= 0) return 'heute'
  if (tage === 1) return 'gestern'
  return formatDatum(datum.toISOString())
}

/** Notiz unter dem Namen: Herkunft und Größe, Fortschritt oder Ergebnis. */
export function notiz(e: UploadEintrag): string {
  switch (e.status) {
    case 'wartet':
      return `${e.herkunft === 'geteilt' ? 'geteilt' : 'aus Dateien'} · ${formatGroesse(e.groesse)}`
    case 'laedt':
      return `${e.prozent} %`
    case 'verarbeitung':
      return 'in Verarbeitung'
    case 'fertig':
      return `${tag(e.uebertragenAm ?? e.erstelltAm)} · ${e.vorgang?.belegId ? 'Beleg angelegt' : (e.vorgang?.ergebnis ?? 'übertragen')}`
    case 'dublette':
      return e.vorgang?.ergebnis ?? 'Dublette eines vorhandenen Belegs.'
    default:
      return e.meldung ?? 'Hochladen fehlgeschlagen.'
  }
}

function Zeile({ eintrag }: { eintrag: UploadEintrag }) {
  const d = darstellung(eintrag)
  return (
    <li className="flex items-center gap-3 border-t border-linie py-3" data-testid="warteschlange-eintrag">
      {eintrag.status === 'verarbeitung' && (eintrag.vorgang ?? eintrag.vorgangId) && <Verfolgung eintrag={eintrag} />}
      <span
        role="img"
        aria-label={d.status}
        title={d.status}
        className={`flex h-6 w-6 flex-none items-center justify-center ${d.farbe}`}
      >
        <Icon name={d.icon} className="text-[24px]" />
      </span>
      <span className="min-w-0 flex-1 text-[13px]">
        <span className="block truncate font-mono">{eintrag.name}</span>
        <span className="gedaempft">{notiz(eintrag)}</span>
      </span>
    </li>
  )
}
