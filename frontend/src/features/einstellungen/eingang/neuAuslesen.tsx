import { useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useEffect, useRef, useState } from 'react'
import { belegeNeuLaden } from '@/api/belege'
import { einstellungenKeys } from '@/api/einstellungen'
import { useNeuAuslesenAbbrechen, useNeuAuslesenStarten, useNeuAuslesenStatus } from '@/api/neuAuslesen'
import type { DiVerbrauch, NeuAuslesenLauf } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { formatZahl } from '@/lib/format'

/** „1 Std. 5 Min.“, „14 Min.“ – Minuten gerundet, damit die Anzeige beim Pollen nicht zappelt. */
function minutenText(sekunden: number): string {
  const minuten = Math.max(1, Math.round(sekunden / 60))
  if (minuten < 60) return `${minuten} Min.`
  const stunden = Math.floor(minuten / 60)
  const rest = minuten % 60
  return rest ? `${stunden} Std. ${rest} Min.` : `${stunden} Std.`
}

/** Geschätzte Restzeit: „noch ca. 8 Min.“, unter einer Minute „gleich fertig“. */
export function restzeitText(sekunden: number | null | undefined): string {
  if (sekunden == null) return 'Restzeit wird berechnet …'
  if (sekunden < 60) return 'gleich fertig'
  return `noch ca. ${minutenText(sekunden)}`
}

/** Dauer eines Laufs: „Dauer 45 Sek.“, „Dauer 14 Min.“. */
export function dauerText(sekunden: number): string {
  return sekunden < 60 ? `Dauer ${Math.max(1, sekunden)} Sek.` : `Dauer ${minutenText(sekunden)}`
}

type Zustand = 'laeuft' | 'wirdAbgebrochen' | 'fertig' | 'abgebrochen'

export function zustand(lauf: NeuAuslesenLauf): Zustand {
  if (lauf.laeuft) return lauf.abgebrochen ? 'wirdAbgebrochen' : 'laeuft'
  return lauf.abbruchgrund ? 'abgebrochen' : 'fertig'
}

const titel: Record<Zustand, string> = {
  laeuft: 'Belege werden erneut ausgelesen',
  wirdAbgebrochen: 'Wird nach dem aktuellen Beleg abgebrochen …',
  fertig: 'Erneutes Auslesen abgeschlossen',
  abgebrochen: 'Erneutes Auslesen vorzeitig beendet',
}

/** „120 neu ausgelesen · 2 Fehler · 1 übersprungen · noch ca. 8 Min.“ */
export function fortschrittText(lauf: NeuAuslesenLauf): string {
  const ausgelesen = lauf.verarbeitet - lauf.anzahlFehler - lauf.anzahlUebersprungen
  const teile = [`${formatZahl(ausgelesen)} neu ausgelesen`]
  if (lauf.anzahlFehler > 0) teile.push(`${formatZahl(lauf.anzahlFehler)} Fehler`)
  if (lauf.anzahlUebersprungen > 0) teile.push(`${formatZahl(lauf.anzahlUebersprungen)} übersprungen`)
  teile.push(lauf.laeuft ? restzeitText(lauf.restSekunden) : dauerText(lauf.dauerSekunden))
  return teile.join(' · ')
}

const balkenFarbe: Record<Zustand, string> = {
  laeuft: 'var(--color-akzent)',
  wirdAbgebrochen: 'var(--color-orange-text)',
  fertig: 'var(--color-gruen-text)',
  abgebrochen: 'var(--color-orange-text)',
}

function Fortschritt({ lauf }: { lauf: NeuAuslesenLauf }) {
  const toast = useToast()
  const abbrechen = useNeuAuslesenAbbrechen()
  const z = zustand(lauf)
  const prozent = lauf.gesamt > 0 ? Math.floor((lauf.verarbeitet / lauf.gesamt) * 100) : 100

  return (
    <section
      aria-label="Fortschritt erneutes Auslesen"
      aria-live="polite"
      className="flex flex-col gap-2 rounded-[10px] bg-flaeche p-4"
      data-zustand={z}
    >
      <div className="flex flex-wrap justify-between gap-2 text-sm">
        <strong>{titel[z]}</strong>
        <span className="num">
          {formatZahl(lauf.verarbeitet)} / {formatZahl(lauf.gesamt)} Belege · {prozent} %
        </span>
      </div>
      <div
        className="h-2.5 rounded-[5px] bg-linie"
        role="progressbar"
        aria-label="Erneut ausgelesene Belege"
        aria-valuemin={0}
        aria-valuemax={lauf.gesamt}
        aria-valuenow={lauf.verarbeitet}
      >
        <span
          className="block h-2.5 rounded-[5px] transition-[width] duration-500"
          style={{ width: `${prozent}%`, background: balkenFarbe[z] }}
        />
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="gedaempft text-[13px]">{fortschrittText(lauf)}</span>
        {lauf.laeuft && (
          <Button
            type="button"
            size="klein"
            disabled={lauf.abgebrochen || abbrechen.isPending}
            laedt={abbrechen.isPending}
            onClick={() => abbrechen.mutateAsync().catch((f: Error) => toast.fehler(f.message))}
          >
            <Icon name="stop" />
            Abbrechen
          </Button>
        )}
      </div>
      {z === 'abgebrochen' && <span className="text-[13px] text-orange-text">Grund: {lauf.abbruchgrund}</span>}
      {lauf.anzahlFehler > 0 && (
        <details className="text-[13px]">
          <summary className="cursor-pointer text-gedaempft-stark">
            {lauf.anzahlFehler === 1 ? '1 Beleg' : `${formatZahl(lauf.anzahlFehler)} Belege`} mit Fehler
          </summary>
          {lauf.letzterFehler && <p className="m-0 mt-1.5 text-gefahr">Letzter Fehler: {lauf.letzterFehler}</p>}
          <ul aria-label="Belege mit Fehler" className="m-0 mt-1.5 flex list-none flex-wrap gap-x-3 gap-y-1 p-0">
            {lauf.fehlerBelege.map((id) => (
              <li key={id}>
                <Link to="/belege/$belegId" params={{ belegId: id }} className="mono">
                  {id.slice(0, 8)}
                </Link>
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  )
}

function StartDialog({
  anzahl,
  verbrauch,
  diKonfiguriert,
  onSchliessen,
}: {
  anzahl: number
  verbrauch?: DiVerbrauch
  diKonfiguriert: boolean
  onSchliessen: () => void
}) {
  const toast = useToast()
  const starten = useNeuAuslesenStarten()

  const absenden = () =>
    starten.mutateAsync().then(
      () => {
        toast.erfolg('Erneutes Auslesen gestartet')
        onSchliessen()
      },
      (f: Error) => toast.fehler(f.message),
    )

  return (
    <AlertDialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 flex-none items-center justify-center rounded-[20px] bg-orange-flaeche text-orange-text">
              <Icon name="refresh" />
            </span>
            <AlertDialogTitle className="pt-2">
              {anzahl === 1 ? '1 Beleg' : `Alle ${formatZahl(anzahl)} Belege`} erneut auslesen?
            </AlertDialogTitle>
          </div>
        </AlertDialogHeader>
        <div className="flex flex-col gap-3 text-sm">
          <AlertDialogDescription className="m-0">
            Rechnungsdaten, Steuerzeilen und Positionen werden mit dem neuen Ergebnis überschrieben – auch von Hand
            korrigierte. Von Hand gesetzte Kategorien und Korrespondenten bleiben erhalten.
          </AlertDialogDescription>
          {diKonfiguriert && (
            <div className="flex items-start gap-2.5 rounded-[10px] bg-flaeche p-3 text-gedaempft-stark">
              <Icon name="document_scanner" />
              <span>
                Belege ohne E-Rechnung gehen erneut durch Document Intelligence und zählen zum Monatslimit
                {verbrauch &&
                  ` (aktuell ${formatZahl(verbrauch.seiten, 0)} von ${formatZahl(verbrauch.limit, 0)} Seiten)`}
                . Ist das Limit erreicht, hält der Lauf an.
              </span>
            </div>
          )}
          <p className="m-0 text-gedaempft-stark">
            Der Lauf arbeitet im Hintergrund; du kannst die Seite verlassen und ihn jederzeit abbrechen.
          </p>
        </div>
        <AlertDialogFooter>
          <Button type="button" onClick={onSchliessen}>
            Abbrechen
          </Button>
          <Button
            type="button"
            variant="primaer"
            disabled={starten.isPending}
            laedt={starten.isPending}
            onClick={() => void absenden()}
          >
            Jetzt auslesen
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/**
 * Einstellungen → Document Intelligence (Admin): alle Belege erneut auslesen. Nach dem Start erscheint die Übersicht
 * mit Fortschritt und Restzeit; sie wird gepollt, solange der Lauf im Hintergrund arbeitet.
 */
export function NeuAuslesenKarte({ verbrauch, diKonfiguriert }: { verbrauch?: DiVerbrauch; diKonfiguriert: boolean }) {
  const queryClient = useQueryClient()
  const { data, isError, error } = useNeuAuslesenStatus()
  const [fragen, setFragen] = useState(false)
  const lauf = data?.lauf
  const laeuft = lauf?.laeuft
  const verarbeitet = lauf?.verarbeitet

  // Während des Laufs den DI-Verbrauch nachziehen, am Ende alles Belegabhängige.
  useEffect(() => {
    if (laeuft && verarbeitet) void queryClient.invalidateQueries({ queryKey: einstellungenKeys.diVerbrauch() })
  }, [laeuft, verarbeitet, queryClient])
  const vorher = useRef(laeuft)
  useEffect(() => {
    if (vorher.current && laeuft === false) {
      void belegeNeuLaden(queryClient)
      void queryClient.invalidateQueries({ queryKey: einstellungenKeys.diVerbrauch() })
    }
    vorher.current = laeuft
  }, [laeuft, queryClient])

  return (
    <section aria-labelledby="neu-auslesen-titel" className="flex flex-col gap-3 border-t border-linie pt-[18px]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-[1_1_320px]">
          <h3 id="neu-auslesen-titel" className="m-0 text-[15px] font-semibold text-text">
            Alle Belege erneut auslesen
          </h3>
          <p className="gedaempft m-0 mt-1 text-[13px]">
            Liest {data ? `alle ${formatZahl(data.anzahlBelege)}` : 'alle'} Belege mit der aktuellen Erkennung neu aus,
            etwa nachdem die Auslese-Regeln verbessert wurden.
          </p>
        </div>
        <Button type="button" disabled={!data || !!laeuft || data.anzahlBelege === 0} onClick={() => setFragen(true)}>
          <Icon name="refresh" />
          Alle neu auslesen
        </Button>
      </div>
      {isError && (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      )}
      {lauf && <Fortschritt lauf={lauf} />}
      {fragen && data && (
        <StartDialog
          anzahl={data.anzahlBelege}
          verbrauch={verbrauch}
          diKonfiguriert={diKonfiguriert}
          onSchliessen={() => setFragen(false)}
        />
      )}
    </section>
  )
}
