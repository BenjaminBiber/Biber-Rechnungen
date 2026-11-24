import { useNavigate } from '@tanstack/react-router'
import { useRef, useState, type ClipboardEvent, type FormEvent, type KeyboardEvent } from 'react'
import { useTotp, useWiederherstellungscode } from '@/api/auth'
import { Icon } from '@/components/Icon'
import { Button } from '@/components/ui/button'
import { Ankreuzfeld, Feld, Meldung, SchrittTitel, TextKnopf, allgemeinerFehler } from './gemeinsam'

const leer = ['', '', '', '', '', '']

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
      const wert = ziffern.join('')
      if (wert.length !== 6) return setFehler('Bitte alle 6 Ziffern eingeben.')
      antwort = await totp.mutateAsync({ code: wert, geraetVertrauen: vertrauen }).catch(() => null)
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
        className="flex cursor-pointer items-center gap-1 self-start border-0 bg-transparent p-0 text-sm font-medium text-gedaempft-stark"
      >
        <Icon name="arrow_back" />
        Zurück
      </button>
      <div className="flex h-[52px] w-[52px] items-center justify-center rounded-[14px] bg-blau-flaeche text-blau-text">
        <Icon name="phonelink_lock" className="text-[28px] [vertical-align:0]" />
      </div>
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
          autoComplete="off"
          spellCheck={false}
          className="font-mono"
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
      ) : (
        <>
          <fieldset className="m-0 border-0 p-0">
            <legend className="label mb-1.5 block">Code</legend>
            <div className="flex justify-between gap-2">
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
                  className="eingabe h-[54px] w-[46px] min-w-0 text-center font-mono text-[22px] font-semibold"
                />
              ))}
            </div>
          </fieldset>
          <Ankreuzfeld label="Diesem Gerät 30 Tage vertrauen" checked={vertrauen} onChange={setVertrauen} />
        </>
      )}
      <Button
        type="submit"
        variant="primaer"
        className="h-11 w-full text-[15px]"
        disabled={totp.isPending || wiederherstellung.isPending}
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
