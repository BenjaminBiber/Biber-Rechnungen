import { useNavigate } from '@tanstack/react-router'
import { useState, type FormEvent } from 'react'
import { googleStartUrl, useAnmelden, useAuthStatus } from '@/api/auth'
import type { AnmeldeErgebnis } from '@/api/typen'
import { Button } from '@/components/ui/button'
import {
  Ankreuzfeld,
  Feld,
  Meldung,
  SchrittTitel,
  TextKnopf,
  allgemeinerFehler,
  pruefeEmail,
  serverFehler,
  type AnmeldeFehler,
} from './gemeinsam'

const meldungen: Partial<Record<AnmeldeErgebnis['status'] | AnmeldeFehler, string>> = {
  Ungueltig: 'E-Mail-Adresse oder Passwort ist falsch.',
  Gesperrt: 'Zu viele Fehlversuche. Bitte in 10 Minuten erneut versuchen.',
  gesperrt: 'Zu viele Fehlversuche. Bitte in 10 Minuten erneut versuchen.',
  'google-unbekannt': 'Dieses Google-Konto ist keinem Benutzer zugeordnet.',
}

/** Logo von „Mit Google anmelden“ wie im Design. */
export function GoogleZeichen() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true" className="text-akzent-text">
      <circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <path d="M12 12h8" stroke="currentColor" strokeWidth="2.2" />
    </svg>
  )
}

export function Trenner() {
  return (
    <div className="flex items-center gap-3 text-[13px] text-gedaempft">
      <span className="h-px flex-1 bg-rand" />
      oder
      <span className="h-px flex-1 bg-rand" />
    </div>
  )
}

/** Schritt „anmelden“: E-Mail und Passwort. */
export function AnmeldenFormular({ fehler }: { fehler?: AnmeldeFehler }) {
  const navigate = useNavigate()
  const { data: status } = useAuthStatus()
  const anmelden = useAnmelden()
  const [email, setEmail] = useState('')
  const [passwort, setPasswort] = useState('')
  const [bleiben, setBleiben] = useState(true)
  const [emailFehler, setEmailFehler] = useState<string>()
  const [passwortFehler, setPasswortFehler] = useState<string>()
  const [ergebnis, setErgebnis] = useState<AnmeldeErgebnis['status']>()
  const [vergessen, setVergessen] = useState(false)

  async function absenden(e: FormEvent) {
    e.preventDefault()
    const fe = pruefeEmail(email)
    const fp = passwort ? undefined : 'Bitte das Passwort eingeben.'
    setEmailFehler(fe)
    setPasswortFehler(fp)
    if (fe || fp) return
    const antwort = await anmelden
      .mutateAsync({ email: email.trim(), passwort, angemeldetBleiben: bleiben })
      .catch(() => null)
    if (!antwort) return
    setErgebnis(antwort.status)
    if (antwort.status === 'Ok') await navigate({ to: '/' })
    else if (antwort.status === 'TotpErforderlich') await navigate({ to: '/anmelden', search: { schritt: 'totp' } })
    else if (antwort.status === 'WartetAufFreigabe')
      await navigate({ to: '/anmelden', search: { schritt: 'freigabe', email: email.trim() } })
  }

  const meldung = (ergebnis && meldungen[ergebnis]) ?? (!ergebnis && fehler ? meldungen[fehler] : undefined)
  const sonstiger = allgemeinerFehler(anmelden.error)

  return (
    <form noValidate onSubmit={absenden} className="flex flex-col gap-[18px]">
      <SchrittTitel titel="Anmelden" text="Mit deinem Firmenkonto" />
      {meldung && <Meldung>{meldung}</Meldung>}
      {sonstiger && <Meldung>{sonstiger}</Meldung>}
      <Feld
        label="E-Mail-Adresse"
        type="email"
        autoComplete="username"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        fehler={emailFehler ?? serverFehler(anmelden.error, 'email')}
      />
      <Feld
        label="Passwort"
        type="password"
        autoComplete="current-password"
        value={passwort}
        onChange={(e) => setPasswort(e.target.value)}
        fehler={passwortFehler ?? serverFehler(anmelden.error, 'passwort')}
        zusatz={
          <TextKnopf className="text-[13px]" aria-expanded={vergessen} onClick={() => setVergessen((v) => !v)}>
            Vergessen?
          </TextKnopf>
        }
      />
      {vergessen && (
        <Meldung art="info">
          Wende dich an einen Admin – er kann dein Passwort unter Einstellungen › Benutzer zurücksetzen.
        </Meldung>
      )}
      <Ankreuzfeld label="Angemeldet bleiben" checked={bleiben} onChange={setBleiben} />
      <Button type="submit" variant="primaer" className="h-11 w-full text-[15px]" disabled={anmelden.isPending}>
        Weiter
      </Button>
      {status?.googleAktiv && (
        <>
          <Trenner />
          <a href={googleStartUrl()} className="btn h-11 w-full text-[15px] text-text no-underline">
            <GoogleZeichen />
            Mit Google anmelden
          </a>
        </>
      )}
      {status?.registrierungOffen && (
        <p className="m-0 text-center text-sm text-gedaempft-stark">
          Noch kein Konto?{' '}
          <TextKnopf onClick={() => void navigate({ to: '/anmelden', search: { schritt: 'registrieren' } })}>
            Registrieren
          </TextKnopf>
        </p>
      )}
    </form>
  )
}
