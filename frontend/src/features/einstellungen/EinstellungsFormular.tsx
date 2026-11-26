import { useState, type FormEvent, type ReactNode } from 'react'
import { istApiFehler } from '@/api/client'
import { useEinstellungenSpeichern, type EinstellungGruppen, type EinstellungSpeichern } from '@/api/einstellungen'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { useRolle } from '@/components/shell/useRolle'

export type Feldfehler = Record<string, string>

export type FormularHelfer<T> = {
  /** Aktueller (ungespeicherter) Stand. */
  wert: T
  /** Ändert einzelne Felder. */
  setze: (teil: Partial<T>) => void
  /** Fehler je Feld (aus `validieren` oder aus der Antwort des Servers). */
  fehler: Feldfehler
  /** Nicht-Admins sehen alles schreibgeschützt. */
  schreibgeschuetzt: boolean
}

/** Erster Buchstabe klein: Der Server meldet Felder teils als `Jahre`, das Formular kennt `jahre`. */
const feldname = (name: string) => name.charAt(0).toLowerCase() + name.slice(1)

/** Feldfehler aus einer `ValidationProblem`-Antwort; erste Meldung je Feld. */
export function serverFeldfehler(f: Error): Feldfehler | undefined {
  return istApiFehler(f) && f.fehler
    ? Object.fromEntries(Object.entries(f.fehler).map(([feld, meldungen]) => [feldname(feld), meldungen[0]]))
    : undefined
}

/**
 * Gemeinsamer Rahmen eines Einstellungsbereichs: Zustand, „Verwerfen“/„Speichern“ (nur bei Änderungen aktiv),
 * Feldfehler, Toast „Gespeichert“ und Schreibschutz für Nicht-Admins.
 * `T` ist der Formularzustand; weicht er vom gespeicherten Inhalt ab (z. B. Zahlen als Text), wandelt `zuWerten` um.
 */
export function EinstellungsFormular<G extends keyof EinstellungGruppen, T>({
  gruppe,
  titel,
  wert,
  children,
  validieren,
  zuWerten,
}: {
  gruppe: G
  titel: string
  wert: T
  children: (helfer: FormularHelfer<T>) => ReactNode
  validieren?: (wert: T) => Feldfehler
  zuWerten?: (wert: T) => EinstellungGruppen[G]
}) {
  const toast = useToast()
  const { istAdmin } = useRolle()
  const speichern = useEinstellungenSpeichern()
  const [ausgang, setAusgang] = useState(wert)
  const [aktuell, setAktuell] = useState(wert)
  const [fehler, setFehler] = useState<Feldfehler>({})

  // Neue Werte vom Server (nach dem Speichern oder Neuladen) übernehmen.
  if (JSON.stringify(wert) !== JSON.stringify(ausgang)) {
    setAusgang(wert)
    setAktuell(wert)
    setFehler({})
  }

  const geaendert = JSON.stringify(aktuell) !== JSON.stringify(ausgang)
  const schreibgeschuetzt = !istAdmin

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    if (!geaendert || schreibgeschuetzt) return
    const pruefung = validieren?.(aktuell) ?? {}
    setFehler(pruefung)
    if (Object.keys(pruefung).length > 0) return
    const werte = zuWerten ? zuWerten(aktuell) : (aktuell as unknown as EinstellungGruppen[G])
    speichern.mutateAsync({ gruppe, werte } as EinstellungSpeichern).then(
      () => toast.erfolg('Gespeichert'),
      (f: Error) => {
        const feldfehler = serverFeldfehler(f)
        if (feldfehler) setFehler(feldfehler)
        toast.fehler(f.message)
      },
    )
  }

  return (
    <form onSubmit={absenden} noValidate className="flex flex-col gap-[18px]" aria-label={titel}>
      {schreibgeschuetzt && (
        <p role="note" className="m-0 rounded-[10px] bg-flaeche px-3.5 py-3 text-sm text-gedaempft-stark">
          Nur Admins können Einstellungen ändern.
        </p>
      )}
      <fieldset disabled={schreibgeschuetzt} className="m-0 min-w-0 border-0 p-0">
        {children({
          wert: aktuell,
          setze: (teil) => setAktuell((alt) => ({ ...alt, ...teil })),
          fehler,
          schreibgeschuetzt,
        })}
      </fieldset>
      {!schreibgeschuetzt && (
        <div className="flex justify-end gap-2">
          <Button
            type="button"
            disabled={!geaendert || speichern.isPending}
            onClick={() => {
              setAktuell(ausgang)
              setFehler({})
            }}
          >
            Verwerfen
          </Button>
          <Button
            type="submit"
            variant="primaer"
            disabled={!geaendert || speichern.isPending}
            laedt={speichern.isPending}
          >
            Speichern
          </Button>
        </div>
      )}
    </form>
  )
}
