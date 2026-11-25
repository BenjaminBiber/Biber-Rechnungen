import { useId, useState, type ReactNode } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { formatDatum } from '@/lib/format'
import { datumEinlesen, zeitraumVorgabe, zeitraumVorgaben } from '@/lib/zeitraum'

export type Zeitraum = { von: string | null; bis: string | null }

/** Zeitraum-Filter (Belege, Dashboard; Basis Rechnungsdatum): Vorgaben plus Von/Bis als TT.MM.JJJJ. */
export function ZeitraumAuswahl({
  wert,
  onAendern,
  ausloeser,
  heute = new Date(),
  anfangsOffen = false,
}: {
  wert: Zeitraum
  onAendern: (z: Zeitraum) => void
  ausloeser: ReactNode
  /** Nur für Tests. */
  heute?: Date
  /** Beim ersten Anzeigen gleich geöffnet (z. B. frisch hinzugefügte Filter-Pill). */
  anfangsOffen?: boolean
}) {
  const id = useId()
  const [offen, setOffen] = useState(anfangsOffen)
  const [von, setVon] = useState('')
  const [bis, setBis] = useState('')
  const [fehler, setFehler] = useState<string | null>(null)

  function oeffnenAendern(neu: boolean) {
    if (neu) {
      setVon(wert.von ? formatDatum(wert.von) : '')
      setBis(wert.bis ? formatDatum(wert.bis) : '')
      setFehler(null)
    }
    setOffen(neu)
  }

  function uebernehmen(z: Zeitraum) {
    onAendern(z)
    setOffen(false)
  }

  function anwenden() {
    const vonIso = von.trim() ? datumEinlesen(von) : null
    const bisIso = bis.trim() ? datumEinlesen(bis) : null
    if ((von.trim() && !vonIso) || (bis.trim() && !bisIso)) {
      setFehler('Bitte ein gültiges Datum im Format TT.MM.JJJJ eingeben.')
      return
    }
    if (vonIso && bisIso && vonIso > bisIso) {
      setFehler('„Von“ liegt nach „Bis“.')
      return
    }
    uebernehmen({ von: vonIso, bis: bisIso })
  }

  return (
    <Popover open={offen} onOpenChange={oeffnenAendern}>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
      <PopoverContent role="dialog" aria-label="Zeitraum" className="w-80">
        <div className="mb-3 flex flex-wrap gap-1.5">
          {zeitraumVorgaben.map((v) => (
            <button
              key={v.id}
              type="button"
              className="btn btn-klein"
              onClick={() => uebernehmen(zeitraumVorgabe(v.id, heute))}
            >
              {v.text}
            </button>
          ))}
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            anwenden()
          }}
        >
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="label" htmlFor={`${id}-von`}>
                Von
              </label>
              <input
                id={`${id}-von`}
                className="eingabe mono"
                placeholder="TT.MM.JJJJ"
                value={von}
                aria-invalid={fehler ? true : undefined}
                onChange={(e) => setVon(e.target.value)}
              />
            </div>
            <div>
              <label className="label" htmlFor={`${id}-bis`}>
                Bis
              </label>
              <input
                id={`${id}-bis`}
                className="eingabe mono"
                placeholder="TT.MM.JJJJ"
                value={bis}
                aria-invalid={fehler ? true : undefined}
                onChange={(e) => setBis(e.target.value)}
              />
            </div>
          </div>
          {fehler && (
            <p role="alert" className="hinweis mt-2 text-rot-text">
              {fehler}
            </p>
          )}
          <div className="mt-3 flex justify-end gap-2">
            <button type="button" className="btn" onClick={() => uebernehmen({ von: null, bis: null })}>
              Leeren
            </button>
            <button type="submit" className="btn btn-primaer">
              Anwenden
            </button>
          </div>
        </form>
      </PopoverContent>
    </Popover>
  )
}
