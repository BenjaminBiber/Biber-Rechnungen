import { useRef } from 'react'
import { useBankleitzahlen, useBankleitzahlenAbrufen, useBankleitzahlenHochladen } from '@/api/bankleitzahlen'
import { istApiFehler } from '@/api/client'
import type { BankleitzahlenStand } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { formatDatumZeit, formatZahl } from '@/lib/format'

const downloadSeite =
  'https://www.bundesbank.de/de/aufgaben/unbarer-zahlungsverkehr/serviceangebot/bankleitzahlen/download-bankleitzahlen-602592'

/** „3.507 Banken · von der Bundesbank geholt am 24.11.2026, 04:23“ */
export function standText(s: BankleitzahlenStand): string {
  if (s.anzahl === 0 || !s.geladenAm) return 'Noch keine Bankleitzahlen geladen – IBANs bleiben ohne Banknamen.'
  const woher = s.quelle === 'Upload' ? `aus „${s.datei}“ hochgeladen` : 'von der Bundesbank geholt'
  return `${formatZahl(s.anzahl, 0)} Banken · ${woher} am ${formatDatumZeit(s.geladenAm)}`
}

/**
 * Einstellungen → Prüfungen (Admin): Bankleitzahlen der Bundesbank, aus denen IBANs ihren Banknamen bekommen.
 * Der Server holt sie selbst; hier lassen sie sich sofort abrufen oder – ohne Internetzugang – hochladen.
 */
export function BankleitzahlenKarte() {
  const toast = useToast()
  const { data, isError, error } = useBankleitzahlen()
  const abrufen = useBankleitzahlenAbrufen()
  const hochladen = useBankleitzahlenHochladen()
  const dateiwahl = useRef<HTMLInputElement>(null)
  const beschaeftigt = abrufen.isPending || hochladen.isPending

  const holen = () =>
    abrufen.mutateAsync().then(
      (s) =>
        s.letzterFehler
          ? toast.fehler(s.letzterFehler)
          : toast.erfolg(`Bankleitzahlen der Bundesbank geladen: ${formatZahl(s.anzahl, 0)} Banken`),
      (f: Error) => toast.fehler(f.message),
    )

  const laden = (datei: File) =>
    hochladen.mutateAsync(datei).then(
      (s) => toast.erfolg(`Bankleitzahlen aus „${datei.name}“ übernommen: ${formatZahl(s.anzahl, 0)} Banken`),
      // Bei 400 steht der Grund („keine Bankleitzahlendatei …“) am Feld `datei`, nicht im Titel.
      (f: Error) => toast.fehler((istApiFehler(f) && f.fehler?.datei?.[0]) || f.message),
    )

  return (
    <section aria-labelledby="bankleitzahlen-titel" className="flex flex-col gap-3 border-t border-linie pt-[18px]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-[1_1_320px]">
          <h3 id="bankleitzahlen-titel" className="m-0 text-[15px] font-semibold text-text">
            Bankleitzahlen
          </h3>
          <p className="gedaempft m-0 mt-1 text-[13px]">
            Zeigt zu deutschen IBANs den Namen der Bank. Die Liste holt sich der Server wöchentlich von der Deutschen
            Bundesbank. Ohne Internetzugang die Datei „blz-aktuell-txt-data.txt“ von der{' '}
            <a href={downloadSeite} target="_blank" rel="noreferrer">
              Downloadseite der Bundesbank
            </a>{' '}
            hier hochladen.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" disabled={beschaeftigt} laedt={abrufen.isPending} onClick={() => void holen()}>
            <Icon name="refresh" />
            Jetzt abrufen
          </Button>
          <Button
            type="button"
            disabled={beschaeftigt}
            laedt={hochladen.isPending}
            onClick={() => dateiwahl.current?.click()}
          >
            <Icon name="upload_file" />
            Datei hochladen
          </Button>
          <input
            ref={dateiwahl}
            type="file"
            accept=".txt,text/plain"
            className="hidden"
            aria-label="Bankleitzahlendatei"
            onChange={(e) => {
              const datei = e.target.files?.[0]
              e.target.value = '' // dieselbe Datei lässt sich so erneut wählen
              if (datei) void laden(datei)
            }}
          />
        </div>
      </div>
      {isError && (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      )}
      {data && (
        <div className="flex flex-col gap-1 text-[13px]">
          <p className="m-0">{standText(data)}</p>
          {data.letzterFehler && (
            <p className="m-0 text-gefahr">
              Abruf
              {data.letzterAbrufAm && ` am ${formatDatumZeit(data.letzterAbrufAm)}`} fehlgeschlagen:{' '}
              {data.letzterFehler}
            </p>
          )}
        </div>
      )}
    </section>
  )
}
