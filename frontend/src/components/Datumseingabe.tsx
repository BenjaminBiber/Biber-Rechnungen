import { useRef, useState, type ComponentProps, type KeyboardEvent } from 'react'
import { Icon } from '@/components/Icon'
import { Popover, PopoverAnchor, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { formatDatum } from '@/lib/format'
import { cn } from '@/lib/utils'
import { datumEinlesen } from '@/lib/zeitraum'

const zwei = (n: number) => String(n).padStart(2, '0')

/** Datum aus Jahr, Monat (0-basiert, darf über-/unterlaufen) und Tag als „JJJJ-MM-TT“. */
function iso(jahr: number, monat: number, tag: number): string {
  const d = new Date(Date.UTC(jahr, monat, tag))
  return `${d.getUTCFullYear()}-${zwei(d.getUTCMonth() + 1)}-${zwei(d.getUTCDate())}`
}

function teile(datum: string): [number, number, number] {
  const [j, m, t] = datum.split('-').map(Number)
  return [j, m - 1, t]
}

function verschieben(datum: string, tage: number, monate = 0): string {
  const [j, m, t] = teile(datum)
  if (monate === 0) return iso(j, m, t + tage)
  // Monatssprung: Tag auf das Monatsende begrenzen (31.01. → 28./29.02.).
  const letzter = Number(iso(j, m + monate + 1, 0).slice(8))
  return iso(j, m + monate, Math.min(t, letzter))
}

const lokalHeute = (d: Date) => iso(d.getFullYear(), d.getMonth(), d.getDate())

const monatsName = new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric', timeZone: 'UTC' })
const tagesName = new Intl.DateTimeFormat('de-DE', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })
const WOCHENTAGE = ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So']

/** Eingabe als Bereich `[von, bis]` (ISO); ein Einzeldatum ergibt `[d, d]`. `null` bei leer/ungültig. */
function bereichAus(text: string, zeitraum: boolean): [string, string] | null {
  const t = text.trim()
  if (!t) return null
  if (zeitraum) {
    const stuecke = t.split(/\s*[–-]\s*(?=\d)/)
    if (stuecke.length === 2) {
      const von = datumEinlesen(stuecke[0])
      const bis = datumEinlesen(stuecke[1])
      return von && bis && von <= bis ? [von, bis] : null
    }
  }
  const d = datumEinlesen(t)
  return d ? [d, d] : null
}

type Props = Omit<ComponentProps<'input'>, 'value' | 'onChange' | 'type'> & {
  /** Text im Format „TT.MM.JJJJ“ (bei `zeitraum` auch „TT.MM.JJJJ – TT.MM.JJJJ“). */
  wert: string
  onAendern: (text: string) => void
  /** Nach einer Auswahl im Kalender (der neue Text wurde schon per `onAendern` gemeldet), z. B. zum Speichern. */
  onGewaehlt?: () => void
  /** Erlaubt im Kalender die Wahl eines Zeitraums: erster Klick = Beginn, zweiter Klick = Ende. */
  zeitraum?: boolean
  /** Nur für Tests. */
  heute?: Date
}

/**
 * Datumsfeld: freie Eingabe „TT.MM.JJJJ“ plus Kalender-Auswahl über den Knopf rechts im Feld.
 * Der Text bleibt die Quelle der Wahrheit – Prüfen und Umwandeln übernimmt der Aufrufer.
 */
export function Datumseingabe({
  wert,
  onAendern,
  onGewaehlt,
  zeitraum = false,
  heute = new Date(),
  className,
  readOnly,
  disabled,
  placeholder,
  ...rest
}: Props) {
  const [offen, setOffen] = useState(false)
  const heuteIso = lokalHeute(heute)
  const auswahl = bereichAus(wert, zeitraum)
  const gesperrt = readOnly || disabled
  // Zeitraum: Beginn gewählt, Ende noch offen – gemeldet (`onGewaehlt`) wird erst beim Schließen.
  const unfertig = useRef(false)

  function oeffnenAendern(neu: boolean) {
    if (!neu && unfertig.current) {
      unfertig.current = false
      onGewaehlt?.()
    }
    setOffen(neu)
  }

  return (
    <Popover open={offen} onOpenChange={oeffnenAendern}>
      <PopoverAnchor asChild>
        <div className="relative">
          <input
            {...rest}
            className={cn('eingabe', !gesperrt && 'pr-9', className)}
            value={wert}
            readOnly={readOnly}
            disabled={disabled}
            placeholder={placeholder ?? (zeitraum ? 'TT.MM.JJJJ oder von – bis' : 'TT.MM.JJJJ')}
            autoComplete="off"
            onChange={(e) => onAendern(e.target.value)}
          />
          {!gesperrt && (
            <PopoverTrigger asChild>
              <button
                type="button"
                aria-label={zeitraum ? 'Datum oder Zeitraum im Kalender wählen' : 'Datum im Kalender wählen'}
                className="absolute top-1/2 right-1 grid size-7 -translate-y-1/2 place-items-center rounded-md text-gedaempft hover:bg-hover hover:text-text focus-visible:outline-2 focus-visible:outline-akzent"
              >
                <Icon name="calendar_month" className="text-[18px]" />
              </button>
            </PopoverTrigger>
          )}
        </div>
      </PopoverAnchor>
      {offen && (
        <PopoverContent
          role="dialog"
          aria-label={zeitraum ? 'Datum oder Zeitraum wählen' : 'Datum wählen'}
          align="end"
          className="w-[296px] p-3"
          onOpenAutoFocus={(e) => {
            // Fokus auf den gewählten (oder heutigen) Tag statt auf „Vorheriges Jahr“.
            e.preventDefault()
            const inhalt = e.currentTarget as HTMLElement
            inhalt.querySelector<HTMLElement>('[data-tag][tabindex="0"]')?.focus()
          }}
        >
          <Kalender
            auswahl={auswahl}
            heute={heuteIso}
            zeitraum={zeitraum}
            onWahl={(text, fertig) => {
              onAendern(text)
              unfertig.current = !fertig
              if (fertig) {
                onGewaehlt?.()
                setOffen(false)
              }
            }}
          />
        </PopoverContent>
      )}
    </Popover>
  )
}

function Kalender({
  auswahl,
  heute,
  zeitraum,
  onWahl,
}: {
  auswahl: [string, string] | null
  heute: string
  zeitraum: boolean
  /** `fertig` = Kalender schließen. */
  onWahl: (text: string, fertig: boolean) => void
}) {
  const [fokus, setFokus] = useState(() => auswahl?.[0] ?? heute)
  // Zeitraum: gewählter Beginn, solange das Ende noch fehlt.
  const [anfang, setAnfang] = useState<string | null>(null)
  const [schwebe, setSchwebe] = useState<string | null>(null)
  const gitter = useRef<HTMLDivElement>(null)

  const [jahr, monat] = teile(fokus)
  const ersterTag = iso(jahr, monat, 1)
  const versatz = (new Date(Date.UTC(jahr, monat, 1)).getUTCDay() + 6) % 7 // Montag = 0
  const tageImMonat = Number(iso(jahr, monat + 1, 0).slice(8))
  const wochen = Math.ceil((versatz + tageImMonat) / 7)
  const tage = Array.from({ length: wochen * 7 }, (_, i) => verschieben(ersterTag, i - versatz))

  // Markierter Bereich: laufende Zeitraum-Wahl (bis zum Mauszeiger) oder der Wert im Feld.
  const markiert: [string, string] | null = anfang ? ([anfang, schwebe ?? anfang].sort() as [string, string]) : auswahl

  function fokusSetzen(neu: string) {
    setFokus(neu)
    requestAnimationFrame(() => gitter.current?.querySelector<HTMLButtonElement>(`[data-tag="${neu}"]`)?.focus())
  }

  function waehlen(tag: string) {
    if (!zeitraum) return onWahl(formatDatum(tag), true)
    if (!anfang) {
      setAnfang(tag)
      return onWahl(formatDatum(tag), false)
    }
    const [von, bis] = [anfang, tag].sort()
    setAnfang(null)
    onWahl(von === bis ? formatDatum(von) : `${formatDatum(von)} – ${formatDatum(bis)}`, true)
  }

  function taste(e: KeyboardEvent) {
    const schritte: Record<string, () => string> = {
      ArrowLeft: () => verschieben(fokus, -1),
      ArrowRight: () => verschieben(fokus, 1),
      ArrowUp: () => verschieben(fokus, -7),
      ArrowDown: () => verschieben(fokus, 7),
      PageUp: () => verschieben(fokus, 0, e.shiftKey ? -12 : -1),
      PageDown: () => verschieben(fokus, 0, e.shiftKey ? 12 : 1),
      Home: () => verschieben(fokus, -((new Date(`${fokus}T00:00:00Z`).getUTCDay() + 6) % 7)),
      End: () => verschieben(fokus, 6 - ((new Date(`${fokus}T00:00:00Z`).getUTCDay() + 6) % 7)),
    }
    const schritt = schritte[e.key]
    if (!schritt) return
    e.preventDefault()
    fokusSetzen(schritt())
  }

  const navKnopf = 'grid size-8 place-items-center rounded-md text-gedaempft-stark hover:bg-hover hover:text-text'

  return (
    <div className="select-none">
      <div className="mb-2 flex items-center gap-0.5">
        <button
          type="button"
          className={navKnopf}
          aria-label="Vorheriges Jahr"
          onClick={() => setFokus(verschieben(fokus, 0, -12))}
        >
          <Icon name="keyboard_double_arrow_left" className="text-[18px]" />
        </button>
        <button
          type="button"
          className={navKnopf}
          aria-label="Vorheriger Monat"
          onClick={() => setFokus(verschieben(fokus, 0, -1))}
        >
          <Icon name="chevron_left" className="text-[20px]" />
        </button>
        <span className="flex-1 text-center text-sm font-semibold text-text" aria-live="polite">
          {monatsName.format(new Date(`${ersterTag}T00:00:00Z`))}
        </span>
        <button
          type="button"
          className={navKnopf}
          aria-label="Nächster Monat"
          onClick={() => setFokus(verschieben(fokus, 0, 1))}
        >
          <Icon name="chevron_right" className="text-[20px]" />
        </button>
        <button
          type="button"
          className={navKnopf}
          aria-label="Nächstes Jahr"
          onClick={() => setFokus(verschieben(fokus, 0, 12))}
        >
          <Icon name="keyboard_double_arrow_right" className="text-[18px]" />
        </button>
      </div>

      <div className="grid grid-cols-7 text-center text-[11px] font-medium text-gedaempft" aria-hidden>
        {WOCHENTAGE.map((t) => (
          <span key={t} className="py-1">
            {t}
          </span>
        ))}
      </div>
      <div
        ref={gitter}
        role="group"
        aria-label={monatsName.format(new Date(`${ersterTag}T00:00:00Z`))}
        className="grid grid-cols-7 gap-y-0.5"
        onMouseLeave={() => setSchwebe(null)}
      >
        {tage.map((tag) => {
          const imMonat = teile(tag)[1] === monat
          const imBereich = markiert !== null && tag >= markiert[0] && tag <= markiert[1]
          const randTag = markiert !== null && (tag === markiert[0] || tag === markiert[1])
          return (
            <button
              key={tag}
              type="button"
              data-tag={tag}
              tabIndex={tag === fokus ? 0 : -1}
              aria-pressed={imBereich}
              aria-current={tag === heute ? 'date' : undefined}
              aria-label={tagesName.format(new Date(`${tag}T00:00:00Z`))}
              onClick={() => waehlen(tag)}
              onKeyDown={taste}
              onMouseEnter={() => anfang && setSchwebe(tag)}
              onFocus={() => setFokus(tag)}
              className={cn(
                'mx-auto grid h-8 w-full place-items-center text-[13px] tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-akzent focus-visible:ring-inset',
                imMonat ? 'text-text' : 'text-gedaempft',
                imBereich && !randTag && 'bg-blau-flaeche text-blau-text',
                randTag
                  ? 'rounded-md bg-akzent font-semibold text-auf-akzent hover:bg-akzent-hover'
                  : 'rounded-md hover:bg-hover',
                tag === heute &&
                  !randTag &&
                  'font-semibold underline decoration-akzent decoration-2 underline-offset-4',
              )}
            >
              {Number(tag.slice(8))}
            </button>
          )
        })}
      </div>

      <div className="mt-2 flex items-center justify-between gap-2 border-t border-rand pt-2">
        <span className="text-xs text-gedaempft">
          {zeitraum ? (anfang ? 'Jetzt das Ende wählen' : 'Tag oder Beginn wählen') : ''}
        </span>
        <button
          type="button"
          className="btn btn-klein"
          onClick={() => {
            setAnfang(null)
            onWahl(formatDatum(heute), true)
          }}
        >
          Heute
        </button>
      </div>
    </div>
  )
}
