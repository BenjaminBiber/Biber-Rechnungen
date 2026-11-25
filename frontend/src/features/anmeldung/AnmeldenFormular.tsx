import { useNavigate } from '@tanstack/react-router'
import { useState, type FormEvent } from 'react'
import { useAnmelden, useAuthStatus } from '@/api/auth'
import type { AnmeldeErgebnis } from '@/api/typen'
import { Button } from '@/components/ui/button'
import {
  AnmeldeReiter,
  Ankreuzfeld,
  Feld,
  GoogleKnopf,
  Meldung,
  TextKnopf,
  Trenner,
  allgemeinerFehler,
  grosserKnopf,
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

/** Schritt „anmelden“: E-Mail und Passwort (Login.dc.html, Reiter „Anmelden“). */
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
    <>
      <AnmeldeReiter aktiv="anmelden" />
      <form noValidate onSubmit={absenden} className="flex flex-col gap-4">
        <h1 className="sr-only">Anmelden</h1>
        <p className="m-0 mb-1 text-[15px] leading-relaxed text-gedaempft-stark">
          Melde dich mit deinem Firmenkonto an, um Belege zu sichten, zu prüfen und auszuwerten.
        </p>
        {meldung && <Meldung>{meldung}</Meldung>}
        {sonstiger && <Meldung>{sonstiger}</Meldung>}
        <Feld
          label="E-Mail-Adresse"
          icon="alternate_email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          fehler={emailFehler ?? serverFehler(anmelden.error, 'email')}
        />
        <Feld
          label="Passwort"
          icon="lock"
          type="password"
          autoComplete="current-password"
          value={passwort}
          onChange={(e) => setPasswort(e.target.value)}
          fehler={passwortFehler ?? serverFehler(anmelden.error, 'passwort')}
        />
        <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
          <Ankreuzfeld label="Angemeldet bleiben" checked={bleiben} onChange={setBleiben} />
          <TextKnopf aria-expanded={vergessen} onClick={() => setVergessen((v) => !v)}>
            Passwort vergessen?
          </TextKnopf>
        </div>
        {vergessen && (
          <Meldung art="info">
            Wende dich an einen Admin – er kann dein Passwort unter Einstellungen › Benutzer zurücksetzen.
          </Meldung>
        )}
        <Button type="submit" variant="primaer" className={grosserKnopf} laedt={anmelden.isPending}>
          Anmelden
        </Button>
        {status?.googleAktiv && (
          <>
            <Trenner />
            <GoogleKnopf>Mit Google anmelden</GoogleKnopf>
          </>
        )}
      </form>
    </>
  )
}
