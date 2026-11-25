import { useNavigate } from '@tanstack/react-router'
import { QRCodeSVG } from 'qrcode.react'
import { useState, type FormEvent } from 'react'
import { useErsteinrichtung, useTotpVorschlag } from '@/api/auth'
import { Button } from '@/components/ui/button'
import {
  Feld,
  Meldung,
  SchrittTitel,
  allgemeinerFehler,
  grosserKnopf,
  pruefeEmail,
  pruefeName,
  pruefePasswort,
  serverFehler,
} from './gemeinsam'

/** TOTP-Schlüssel in Vierergruppen („JBSW Y3DP EHPK 3PXP“). */
export function inVierergruppen(schluessel: string): string {
  return schluessel.match(/.{1,4}/g)?.join(' ') ?? ''
}

/** Schritt „ersteinrichtung“: ersten Admin mit Zwei-Faktor-Anmeldung anlegen. */
export function Ersteinrichtung() {
  const navigate = useNavigate()
  const vorschlag = useTotpVorschlag()
  const einrichten = useErsteinrichtung()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [passwort, setPasswort] = useState('')
  const [fehler, setFehler] = useState<{ name?: string; email?: string; passwort?: string }>({})

  async function absenden(e: FormEvent) {
    e.preventDefault()
    const neu = { name: pruefeName(name), email: pruefeEmail(email), passwort: pruefePasswort(passwort) }
    setFehler(neu)
    if (neu.name || neu.email || neu.passwort || !vorschlag.data) return
    const ok = await einrichten
      .mutateAsync({ name: name.trim(), email: email.trim(), passwort, totpSchluessel: vorschlag.data.schluessel })
      .then(() => true)
      .catch(() => false)
    if (ok) await navigate({ to: '/' })
  }

  return (
    <form noValidate onSubmit={absenden} className="flex flex-col gap-4">
      <SchrittTitel
        titel="Admin-Konto anlegen"
        text="Der erste Benutzer wird automatisch Admin. Danach richtest du die Zwei-Faktor-Anmeldung ein."
      />
      {allgemeinerFehler(einrichten.error) && <Meldung>{allgemeinerFehler(einrichten.error)}</Meldung>}
      <Feld
        label="Name"
        icon="person"
        autoComplete="name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        fehler={fehler.name ?? serverFehler(einrichten.error, 'name')}
      />
      <Feld
        label="E-Mail-Adresse"
        icon="alternate_email"
        type="email"
        autoComplete="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        fehler={fehler.email ?? serverFehler(einrichten.error, 'email')}
      />
      <Feld
        label="Passwort"
        icon="lock"
        type="password"
        autoComplete="new-password"
        value={passwort}
        onChange={(e) => setPasswort(e.target.value)}
        fehler={fehler.passwort ?? serverFehler(einrichten.error, 'passwort')}
      />
      <div className="flex items-center gap-3.5 rounded-[10px] border border-rand p-3.5">
        {vorschlag.data ? (
          <>
            <QRCodeSVG
              value={vorschlag.data.otpAuthUri}
              size={88}
              role="img"
              aria-label="QR-Code für die Authenticator-App"
              className="flex-none"
            />
            <div className="text-[13px] leading-normal text-gedaempft-stark">
              QR-Code mit der Authenticator-App scannen oder Schlüssel eingeben:
              <br />
              <span className="font-mono text-text" data-testid="totp-schluessel">
                {inVierergruppen(vorschlag.data.schluessel)}
              </span>
            </div>
          </>
        ) : (
          <div className="text-[13px] text-gedaempft">
            {vorschlag.isError ? 'Schlüssel konnte nicht geladen werden.' : 'Schlüssel wird erzeugt …'}
          </div>
        )}
      </div>
      <Button
        type="submit"
        variant="primaer"
        className={grosserKnopf}
        disabled={einrichten.isPending || !vorschlag.data}
      >
        Konto anlegen und fortfahren
      </Button>
    </form>
  )
}
