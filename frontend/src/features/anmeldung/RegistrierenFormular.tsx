import { useNavigate } from '@tanstack/react-router'
import { useState, type FormEvent } from 'react'
import { useAuthStatus, useRegistrieren } from '@/api/auth'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import {
  AnmeldeReiter,
  Feld,
  GoogleKnopf,
  MINDEST_PASSWORT,
  Meldung,
  Trenner,
  allgemeinerFehler,
  grosserKnopf,
  pruefeEmail,
  pruefeName,
  pruefePasswort,
  serverFehler,
} from './gemeinsam'

/**
 * Stärke 0–4: unter 12 Zeichen 0 („zu kurz“), sonst 1 plus je ein Punkt für 14 und 16 Zeichen
 * sowie für zwei und drei Zeichenarten (Klein-, Großbuchstaben, Ziffern, Sonderzeichen).
 */
export function passwortStaerke(passwort: string): 0 | 1 | 2 | 3 | 4 {
  if (passwort.length < MINDEST_PASSWORT) return 0
  const arten = [/[a-zäöüß]/, /[A-ZÄÖÜ]/, /\d/, /[^a-zA-Z0-9äöüßÄÖÜ]/].filter((r) => r.test(passwort)).length
  const punkte = [passwort.length >= 14, passwort.length >= 16, arten >= 2, arten >= 3].filter(Boolean).length
  return Math.min(4, 1 + punkte) as 1 | 2 | 3 | 4
}

const stufen = [
  { text: `Zu kurz – mindestens ${MINDEST_PASSWORT} Zeichen`, farbe: 'bg-rot-text', schrift: 'text-rot-text' },
  { text: 'Schwaches Passwort', farbe: 'bg-rot-text', schrift: 'text-rot-text' },
  { text: 'Mittleres Passwort', farbe: 'bg-orange-text', schrift: 'text-orange-text' },
  { text: 'Starkes Passwort', farbe: 'bg-gruen-text', schrift: 'text-gruen-text' },
  { text: 'Sehr starkes Passwort', farbe: 'bg-gruen-text', schrift: 'text-gruen-text' },
]

function StaerkeAnzeige({ passwort }: { passwort: string }) {
  if (!passwort) return null
  const staerke = passwortStaerke(passwort)
  const stufe = stufen[staerke]!
  return (
    <>
      <div className="mt-2 flex gap-1" aria-hidden="true">
        {[1, 2, 3, 4].map((n) => (
          <span key={n} className={cn('h-1 flex-1 rounded-sm', n <= staerke ? stufe.farbe : 'bg-rand')} />
        ))}
      </div>
      <div className={cn('mt-1 text-xs', stufe.schrift)} aria-live="polite">
        {stufe.text}
      </div>
    </>
  )
}

/** Schritt „registrieren“: Konto beantragen, danach Freigabe-Hinweis. */
export function RegistrierenFormular() {
  const navigate = useNavigate()
  const { data: status } = useAuthStatus()
  const registrieren = useRegistrieren()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [passwort, setPasswort] = useState('')
  const [fehler, setFehler] = useState<{ name?: string; email?: string; passwort?: string }>({})

  async function absenden(e: FormEvent) {
    e.preventDefault()
    const neu = { name: pruefeName(name), email: pruefeEmail(email), passwort: pruefePasswort(passwort) }
    setFehler(neu)
    if (neu.name || neu.email || neu.passwort) return
    const ok = await registrieren
      .mutateAsync({ name: name.trim(), email: email.trim(), passwort })
      .then(() => true)
      .catch(() => false)
    if (ok) await navigate({ to: '/anmelden', search: { schritt: 'freigabe', email: email.trim() } })
  }

  return (
    <>
      <AnmeldeReiter aktiv="registrieren" />
      <form noValidate onSubmit={absenden} className="flex flex-col gap-4">
        <h1 className="sr-only">Konto erstellen</h1>
        <p className="m-0 mb-1 text-[15px] leading-relaxed text-gedaempft-stark">
          Neue Konten sind gesperrt, bis ein Admin sie freigibt.
        </p>
        {allgemeinerFehler(registrieren.error) && <Meldung>{allgemeinerFehler(registrieren.error)}</Meldung>}
        <Feld
          label="Name"
          icon="person"
          autoComplete="name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          fehler={fehler.name ?? serverFehler(registrieren.error, 'name')}
        />
        <Feld
          label="E-Mail-Adresse"
          icon="alternate_email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          fehler={fehler.email ?? serverFehler(registrieren.error, 'email')}
        />
        <Feld
          label="Passwort"
          icon="lock"
          type="password"
          autoComplete="new-password"
          value={passwort}
          onChange={(e) => setPasswort(e.target.value)}
          fehler={fehler.passwort ?? serverFehler(registrieren.error, 'passwort')}
          unterhalb={<StaerkeAnzeige passwort={passwort} />}
        />
        <Button type="submit" variant="primaer" className={grosserKnopf} laedt={registrieren.isPending}>
          Konto erstellen
        </Button>
        {status?.googleAktiv && (
          <>
            <Trenner />
            <GoogleKnopf>Mit Google registrieren</GoogleKnopf>
          </>
        )}
      </form>
    </>
  )
}
