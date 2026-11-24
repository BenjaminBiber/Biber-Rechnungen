import { useId, useState, type ReactNode } from 'react'
import type { BetragArt } from '@/api/typen'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { formatZahl } from '@/lib/format'

export type Betragsbereich = { von: number | null; bis: number | null; art: BetragArt }

/** „1.234,5“ oder „1234.50“ → Zahl; leer → null; ungültig → NaN. */
function betragEinlesen(text: string): number | null {
  const roh = text.trim().replace(/\s|€/g, '')
  if (!roh) return null
  const normal = roh.includes(',') ? roh.replace(/\./g, '').replace(',', '.') : roh
  return /^-?\d+(\.\d{1,2})?$/.test(normal) ? Number(normal) : Number.NaN
}

const alsText = (n: number | null) => (n === null ? '' : formatZahl(n, 2))

/** Betrag-Filter (Belege, Filterleiste): Brutto/Netto und Von/Bis in €. */
export function BetragAuswahl({
  wert,
  onAendern,
  ausloeser,
}: {
  wert: Betragsbereich
  onAendern: (b: Betragsbereich) => void
  ausloeser: ReactNode
}) {
  const id = useId()
  const [offen, setOffen] = useState(false)
  const [art, setArt] = useState<BetragArt>(wert.art)
  const [von, setVon] = useState('')
  const [bis, setBis] = useState('')
  const [fehler, setFehler] = useState<string | null>(null)

  function oeffnenAendern(neu: boolean) {
    if (neu) {
      setArt(wert.art)
      setVon(alsText(wert.von))
      setBis(alsText(wert.bis))
      setFehler(null)
    }
    setOffen(neu)
  }

  function anwenden() {
    const vonZahl = betragEinlesen(von)
    const bisZahl = betragEinlesen(bis)
    if (Number.isNaN(vonZahl) || Number.isNaN(bisZahl)) {
      setFehler('Bitte einen gültigen Betrag eingeben.')
      return
    }
    if (vonZahl !== null && bisZahl !== null && vonZahl > bisZahl) {
      setFehler('„Von“ ist größer als „Bis“.')
      return
    }
    onAendern({ von: vonZahl, bis: bisZahl, art })
    setOffen(false)
  }

  return (
    <Popover open={offen} onOpenChange={oeffnenAendern}>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
      <PopoverContent role="dialog" aria-label="Betrag" className="w-72">
        <form
          onSubmit={(e) => {
            e.preventDefault()
            anwenden()
          }}
        >
          <div className="segment mb-3" role="group" aria-label="Betragsart">
            {(['Brutto', 'Netto'] as const).map((a) => (
              <button key={a} type="button" aria-pressed={art === a} onClick={() => setArt(a)}>
                {a}
              </button>
            ))}
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="label" htmlFor={`${id}-von`}>
                Von (€)
              </label>
              <input
                id={`${id}-von`}
                className="eingabe num"
                inputMode="decimal"
                value={von}
                onChange={(e) => setVon(e.target.value)}
              />
            </div>
            <div>
              <label className="label" htmlFor={`${id}-bis`}>
                Bis (€)
              </label>
              <input
                id={`${id}-bis`}
                className="eingabe num"
                inputMode="decimal"
                value={bis}
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
            <button
              type="button"
              className="btn"
              onClick={() => {
                onAendern({ von: null, bis: null, art })
                setOffen(false)
              }}
            >
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
