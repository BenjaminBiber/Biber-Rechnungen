import { useNavigate } from '@tanstack/react-router'
import { useEffect, useRef, useState, type ClipboardEvent, type FormEvent, type KeyboardEvent } from 'react'
import { useTotp, useWiederherstellungscode } from '@/api/auth'
import { Icon } from '@/components/Icon'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { Ankreuzfeld, Feld, Meldung, SchrittTitel, TextKnopf, allgemeinerFehler, grosserKnopf } from './gemeinsam'

const leer = ['', '', '', '', '', '']

/** Ablauf der Ziffernfelder: Eingabe → automatische Prüfung (Welle) → gültig (grün) oder falsch (Schütteln). */
type Pruefzustand = 'eingabe' | 'pruefen' | 'gueltig' | 'falsch'

const warte = (ms: number) => new Promise((fertig) => setTimeout(fertig, ms))

/** Schritt „totp“: sechs Ziffernfelder oder Wiederherstellungscode. */
export function TotpFormular() {
  const navigate = useNavigate()
  const totp = useTotp()
  const wiederherstellung = useWiederherstellungscode()
  const [ziffern, setZiffern] = useState(leer)
  const [vertrauen, setVertrauen] = useState(false)
  const [mitCode, setMitCode] = useState(false)
  const [code, setCode] = useState('')
  const [fehler, setFehler] = useState<string>()
  const felder = useRef<Array<HTMLInputElement | null>>([])
  const [zustand, setZustand] = useState<Pruefzustand>('eingabe')
  const gesperrt = zustand === 'pruefen' || zustand === 'gueltig'
  const wert = ziffern.join('')

  // Sobald alle sechs Ziffern stehen, wird der Code ohne Klick geprüft.
  useEffect(() => {
    if (!mitCode && wert.length === 6 && zustand === 'eingabe') void pruefeZiffern(wert)
    // eslint-disable-next-line react-hooks/exhaustive-deps -- nur bei vollständiger Eingabe auslösen
  }, [wert, mitCode, zustand])

  async function pruefeZiffern(code: string) {
    setFehler(undefined)
    setZustand('pruefen')
    const antwort = await totp.mutateAsync({ code, geraetVertrauen: vertrauen }).catch(() => null)
    if (antwort?.status === 'Ok') {
      setZustand('gueltig')
      await warte(350)
      await navigate({ to: '/' })
      return
    }
    if (antwort?.status === 'Gesperrt') setFehler('Zu viele Fehlversuche. Bitte in 10 Minuten erneut versuchen.')
    else if (antwort) setFehler('Der Code ist ungültig.')
    // Falsch: kurz schütteln, dann leeren und vorne neu beginnen.
    setZustand('falsch')
    await warte(450)
    setZiffern(leer)
    setZustand('eingabe')
    felder.current[0]?.focus()
  }

  function setzeAb(index: number, werte: string[]) {
    setZiffern((alt) => {
      const neu = [...alt]
      werte.forEach((w, i) => {
        if (index + i < 6) neu[index + i] = w
      })
      return neu
    })
    felder.current[Math.min(index + werte.length, 5)]?.focus()
  }

  function beiEingabe(index: number, wert: string) {
    const nurZiffern = wert.replace(/\D/g, '')
    if (!nurZiffern) {
      setZiffern((alt) => alt.map((z, i) => (i === index ? '' : z)))
      return
    }
    // Autofill liefert den ganzen Code, sonst zählt die zuletzt getippte Ziffer.
    if (nurZiffern.length >= 6) setzeAb(0, nurZiffern.slice(0, 6).split(''))
    else setzeAb(index, [nurZiffern.slice(-1)])
  }

  function beiEinfuegen(index: number, e: ClipboardEvent<HTMLInputElement>) {
    const nurZiffern = e.clipboardData.getData('text').replace(/\D/g, '')
    if (!nurZiffern) return
    e.preventDefault()
    // Ein vollständiger Code verteilt sich immer ab dem ersten Feld.
    setzeAb(nurZiffern.length >= 6 ? 0 : index, nurZiffern.slice(0, 6).split(''))
  }

  function beiTaste(index: number, e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Backspace' && !ziffern[index] && index > 0) {
      e.preventDefault()
      setZiffern((alt) => alt.map((z, i) => (i === index - 1 ? '' : z)))
      felder.current[index - 1]?.focus()
    }
  }

  async function absenden(e: FormEvent) {
    e.preventDefault()
    setFehler(undefined)
    let antwort
    if (mitCode) {
      if (!code.trim()) return setFehler('Bitte einen Wiederherstellungscode eingeben.')
      antwort = await wiederherstellung.mutateAsync({ code: code.trim() }).catch(() => null)
    } else {
      if (wert.length !== 6) return setFehler('Bitte alle 6 Ziffern eingeben.')
      if (zustand === 'eingabe') await pruefeZiffern(wert)
      return
    }
    if (!antwort) return
    if (antwort.status === 'Ok') await navigate({ to: '/' })
    else if (antwort.status === 'Gesperrt') setFehler('Zu viele Fehlversuche. Bitte in 10 Minuten erneut versuchen.')
    else setFehler(mitCode ? 'Der Wiederherstellungscode ist ungültig.' : 'Der Code ist ungültig.')
  }

  const sonstiger = allgemeinerFehler(mitCode ? wiederherstellung.error : totp.error)

  return (
    <form noValidate onSubmit={absenden} className="flex flex-col gap-[18px]">
      <button
        type="button"
        onClick={() => void navigate({ to: '/anmelden', search: {} })}
        className="btn h-[34px] gap-1 self-start pr-3 pl-2 text-[13px]"
      >
        <Icon name="arrow_back" className="text-[18px] [vertical-align:0]" />
        Zurück
      </button>
      <SchrittTitel
        titel="Bestätigungscode"
        text={
          mitCode
            ? 'Gib einen deiner Wiederherstellungscodes ein.'
            : 'Gib den 6-stelligen Code aus deiner Authenticator-App ein.'
        }
      />
      {fehler && <Meldung>{fehler}</Meldung>}
      {sonstiger && <Meldung>{sonstiger}</Meldung>}
      {mitCode ? (
        <Feld
          label="Wiederherstellungscode"
          icon="key"
          autoComplete="off"
          spellCheck={false}
          className="font-mono"
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
      ) : (
        <>
          {/* Vor den Ziffern, weil der Code nach der sechsten Ziffer sofort geprüft wird. */}
          <Ankreuzfeld label="Diesem Gerät 30 Tage vertrauen" checked={vertrauen} onChange={setVertrauen} />
          <fieldset className="m-0 border-0 p-0">
            <legend className="sr-only">Code</legend>
            <div
              className={cn('flex justify-between gap-2', zustand === 'falsch' && 'ziffern-schuetteln')}
              aria-busy={zustand === 'pruefen'}
            >
              {ziffern.map((ziffer, i) => (
                <input
                  key={i}
                  ref={(el) => {
                    felder.current[i] = el
                  }}
                  aria-label={`Ziffer ${i + 1}`}
                  inputMode="numeric"
                  autoComplete={i === 0 ? 'one-time-code' : 'off'}
                  maxLength={6}
                  value={ziffer}
                  onChange={(e) => beiEingabe(i, e.target.value)}
                  onPaste={(e) => beiEinfuegen(i, e)}
                  onKeyDown={(e) => beiTaste(i, e)}
                  onFocus={(e) => e.target.select()}
                  readOnly={gesperrt}
                  style={{ animationDelay: zustand === 'pruefen' ? `${i * 70}ms` : undefined }}
                  className={cn(
                    'eingabe h-[54px] w-[46px] min-w-0 rounded-[10px] text-center font-mono text-[22px] font-semibold transition-[border-color,background-color,color] duration-200',
                    zustand === 'pruefen' && 'ziffer-pruefen',
                    zustand === 'gueltig' && 'ziffer-gueltig',
                    zustand === 'falsch' && 'ziffer-falsch',
                  )}
                />
              ))}
            </div>
          </fieldset>
        </>
      )}
      <Button
        type="submit"
        variant="primaer"
        className={grosserKnopf}
        laedt={totp.isPending || wiederherstellung.isPending || gesperrt}
      >
        Anmelden
      </Button>
      <TextKnopf
        className="text-center text-[13px]"
        onClick={() => {
          setMitCode((m) => !m)
          setFehler(undefined)
        }}
      >
        {mitCode ? 'Code aus der Authenticator-App verwenden' : 'Wiederherstellungscode verwenden'}
      </TextKnopf>
    </form>
  )
}
