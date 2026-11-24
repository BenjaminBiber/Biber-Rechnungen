import { useState } from 'react'
import { useAufbewahrungLoeschen } from '@/api/aufbewahrung'
import type { Loeschbar, LoeschErgebnis } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { formatDatum } from '@/lib/format'

const BESTAETIGUNG = 'LÖSCHEN'

/** Dialog „Löschung freigeben (Admin)“ (Dialoge.dc.html); nach Erfolg mit der Pfadliste der Originale. */
export function LoeschDialog({ eintrag, onSchliessen }: { eintrag: Loeschbar; onSchliessen: () => void }) {
  const toast = useToast()
  const loeschen = useAufbewahrungLoeschen()
  const [eingabe, setEingabe] = useState('')
  const [ergebnis, setErgebnis] = useState<LoeschErgebnis>()

  // `mutateAsync`: Nach dem Neuladen fehlt der Jahrgang in der Liste, der Dialog bleibt trotzdem offen.
  const absenden = () =>
    loeschen
      .mutateAsync({ jahr: eintrag.jahr, bestaetigung: `${eintrag.jahr} ${BESTAETIGUNG}` })
      .then(setErgebnis, (f: Error) => void toast.fehler(f.message))

  const kopieren = () =>
    navigator.clipboard.writeText(ergebnis!.pfade.join('\n')).then(
      () => toast.erfolg('Liste kopiert'),
      () => toast.fehler('Kopieren nicht möglich'),
    )

  return (
    <AlertDialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 flex-none items-center justify-center rounded-[20px] bg-rot-flaeche text-rot-text">
              <Icon name="delete_forever" />
            </span>
            <AlertDialogTitle className="pt-2">
              {ergebnis
                ? `${ergebnis.geloescht} Belege aus ${eintrag.jahr} gelöscht`
                : `${eintrag.anzahl} Belege aus ${eintrag.jahr} endgültig löschen?`}
            </AlertDialogTitle>
          </div>
        </AlertDialogHeader>
        {ergebnis ? (
          <div className="flex flex-col gap-3 text-sm">
            <AlertDialogDescription className="m-0">
              Lösche jetzt die Originale auf dem WORM-Share selbst auf dem Host:
            </AlertDialogDescription>
            <pre
              aria-label="Pfadliste der Originale"
              className="mono m-0 max-h-60 overflow-auto rounded-lg bg-flaeche p-3 whitespace-pre"
            >
              {ergebnis.pfade.join('\n')}
            </pre>
          </div>
        ) : (
          <div className="flex flex-col gap-3 text-sm">
            <AlertDialogDescription className="m-0">
              Die Aufbewahrungsfrist endete am {formatDatum(eintrag.fristEnde)}. Gelöscht werden die Daten in der App
              (Belegdaten, Positionen, Vorschaubilder). Das Änderungsprotokoll behält einen Eintrag zur Löschung.
            </AlertDialogDescription>
            <div className="flex items-start gap-2.5 rounded-[10px] bg-flaeche p-3 text-gedaempft-stark">
              <Icon name="dns" />
              <span>
                Die Originale auf dem WORM-Share löschst du anschließend selbst auf dem Host. Die App zeigt dir die
                Pfadliste.
              </span>
            </div>
            <div>
              <label className="label" htmlFor="loeschen-bestaetigung">
                Zur Bestätigung „{BESTAETIGUNG}“ eingeben
              </label>
              <input
                id="loeschen-bestaetigung"
                className="eingabe"
                autoComplete="off"
                value={eingabe}
                onChange={(e) => setEingabe(e.target.value)}
              />
            </div>
          </div>
        )}
        <AlertDialogFooter>
          {ergebnis ? (
            <>
              <Button type="button" onClick={() => void kopieren()}>
                <Icon name="content_copy" />
                Liste kopieren
              </Button>
              <Button type="button" variant="primaer" onClick={onSchliessen}>
                Schließen
              </Button>
            </>
          ) : (
            <>
              <Button type="button" onClick={onSchliessen}>
                Abbrechen
              </Button>
              <Button
                type="button"
                variant="gefaehrlich"
                disabled={eingabe.trim() !== BESTAETIGUNG || loeschen.isPending}
                onClick={() => void absenden()}
              >
                Endgültig löschen
              </Button>
            </>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
