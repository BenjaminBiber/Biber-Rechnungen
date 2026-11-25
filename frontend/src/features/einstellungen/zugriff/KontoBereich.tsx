import { QRCodeSVG } from 'qrcode.react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { googleStartUrl, useAuthStatus } from '@/api/auth'
import { istApiFehler } from '@/api/client'
import {
  useAndereAbmelden,
  useKonto,
  useKontoAendern,
  usePasswortAendern,
  useTotpAktivieren,
  useTotpDeaktivieren,
  useTotpEinrichten,
  useWiederherstellungscodesErzeugen,
} from '@/api/konto'
import type { Konto, TotpEinrichtung } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDatum } from '@/lib/format'
import { Zeile } from '../Zeile'
import { Feld, MINDEST_LAENGE, Rueckfrage, StaerkeAnzeige } from './gemeinsam'

type Toast = ReturnType<typeof useToast>
const fehlerMelden = (toast: Toast) => (f: Error) => void toast.fehler(f.message)

/** Erster Feldfehler des Servers oder `undefined`. */
const feldFehler = (f: Error, feld: string) => (istApiFehler(f) ? f.fehler?.[feld]?.[0] : undefined)

function NameUndEmail({ konto }: { konto: Konto }) {
  const toast = useToast()
  const aendern = useKontoAendern()
  const [name, setName] = useState(konto.name)
  const [email, setEmail] = useState(konto.email)
  const [fehler, setFehler] = useState<Record<string, string | undefined>>({})
  const geaendert = name !== konto.name || email !== konto.email

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    aendern.mutateAsync({ name: name.trim(), email: email.trim() }).then(
      () => {
        setFehler({})
        toast.erfolg('Gespeichert')
      },
      (f: Error) => {
        setFehler({ name: feldFehler(f, 'name'), email: feldFehler(f, 'email') })
        toast.fehler(f.message)
      },
    )
  }

  return (
    <form onSubmit={absenden} noValidate className="flex flex-col gap-3" aria-label="Name und E-Mail">
      <div className="grid gap-3.5 sm:grid-cols-2">
        <Feld
          id="konto-name"
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          fehler={fehler.name}
        />
        <Feld
          id="konto-email"
          label="E-Mail-Adresse"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          fehler={fehler.email}
        />
      </div>
      <Button
        type="submit"
        variant="primaer"
        className="self-end"
        disabled={!geaendert || aendern.isPending}
        laedt={aendern.isPending}
      >
        Speichern
      </Button>
    </form>
  )
}

function PasswortDialog({ onSchliessen }: { onSchliessen: () => void }) {
  const toast = useToast()
  const aendern = usePasswortAendern()
  const [alt, setAlt] = useState('')
  const [neu, setNeu] = useState('')
  const [wiederholung, setWiederholung] = useState('')
  const [fehler, setFehler] = useState<Record<string, string | undefined>>({})

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    const f: Record<string, string> = {}
    if (!alt) f.alt = 'Aktuelles Passwort eingeben.'
    if (neu.length < MINDEST_LAENGE) f.neu = `Mindestens ${MINDEST_LAENGE} Zeichen.`
    if (wiederholung !== neu) f.wiederholung = 'Die Passwörter stimmen nicht überein.'
    setFehler(f)
    if (Object.keys(f).length > 0) return
    aendern.mutateAsync({ altesPasswort: alt, neuesPasswort: neu }).then(
      () => {
        toast.erfolg('Passwort geändert')
        onSchliessen()
      },
      (x: Error) => {
        setFehler({ alt: feldFehler(x, 'altesPasswort'), neu: feldFehler(x, 'neuesPasswort') })
        toast.fehler(x.message)
      },
    )
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>Passwort ändern</DialogTitle>
        </DialogHeader>
        <form onSubmit={absenden} noValidate className="flex flex-col gap-3" aria-label="Passwort ändern">
          <Feld
            id="passwort-alt"
            label="Aktuelles Passwort"
            type="password"
            autoComplete="current-password"
            value={alt}
            onChange={(e) => setAlt(e.target.value)}
            fehler={fehler.alt}
          />
          <Feld
            id="passwort-neu"
            label="Neues Passwort"
            type="password"
            autoComplete="new-password"
            value={neu}
            onChange={(e) => setNeu(e.target.value)}
            fehler={fehler.neu}
            unterhalb={<StaerkeAnzeige passwort={neu} />}
          />
          <Feld
            id="passwort-wiederholung"
            label="Neues Passwort wiederholen"
            type="password"
            autoComplete="new-password"
            value={wiederholung}
            onChange={(e) => setWiederholung(e.target.value)}
            fehler={fehler.wiederholung}
          />
          <DialogFooter>
            <Button type="button" onClick={onSchliessen}>
              Abbrechen
            </Button>
            <Button type="submit" variant="primaer" laedt={aendern.isPending}>
              Passwort ändern
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Wiederherstellungscodes einmalig mit „Kopieren“ und „Herunterladen“. */
function CodeListe({ codes }: { codes: string[] }) {
  const toast = useToast()
  const text = codes.join('\n')
  const kopieren = () =>
    navigator.clipboard.writeText(text).then(
      () => toast.erfolg('Codes kopiert'),
      () => toast.fehler('Kopieren nicht möglich'),
    )
  const herunterladen = () => {
    const url = URL.createObjectURL(new Blob([text + '\n'], { type: 'text/plain' }))
    const a = document.createElement('a')
    a.href = url
    a.download = 'biber-rechnungen-wiederherstellungscodes.txt'
    a.click()
    URL.revokeObjectURL(url)
  }
  return (
    <div className="flex flex-col gap-3 text-sm">
      <p className="m-0">
        Bewahre diese Codes sicher auf. Jeder Code funktioniert einmal, falls du keinen Zugriff auf die
        Authenticator-App hast. Sie werden nur jetzt angezeigt.
      </p>
      <ul
        aria-label="Wiederherstellungscodes"
        className="mono m-0 grid list-none grid-cols-2 gap-1.5 rounded-lg bg-flaeche p-3"
      >
        {codes.map((c) => (
          <li key={c}>{c}</li>
        ))}
      </ul>
      <div className="flex gap-2">
        <Button type="button" size="klein" onClick={() => void kopieren()}>
          <Icon name="content_copy" />
          Kopieren
        </Button>
        <Button type="button" size="klein" onClick={herunterladen}>
          <Icon name="download" />
          Herunterladen
        </Button>
      </div>
    </div>
  )
}

function CodesDialog({ titel, codes, onSchliessen }: { titel: string; codes: string[]; onSchliessen: () => void }) {
  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>{titel}</DialogTitle>
        </DialogHeader>
        <CodeListe codes={codes} />
        <DialogFooter>
          <Button type="button" variant="primaer" onClick={onSchliessen}>
            Fertig
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Einrichtung in zwei Schritten: QR-Code und Schlüssel, dann sechsstelliger Code; danach die Codes. */
function TotpDialog({ onSchliessen }: { onSchliessen: () => void }) {
  const toast = useToast()
  const einrichten = useTotpEinrichten()
  const aktivieren = useTotpAktivieren()
  const [einrichtung, setEinrichtung] = useState<TotpEinrichtung>()
  const [schritt, setSchritt] = useState<1 | 2>(1)
  const [code, setCode] = useState('')
  const [fehler, setFehler] = useState<string>()
  const [codes, setCodes] = useState<string[]>()
  const gestartet = useRef(false)

  useEffect(() => {
    if (gestartet.current) return
    gestartet.current = true
    einrichten.mutateAsync().then(setEinrichtung, fehlerMelden(toast))
  }, [einrichten, toast])

  const bestaetigen = (e: FormEvent) => {
    e.preventDefault()
    const sauber = code.replace(/\s/g, '')
    if (!/^\d{6}$/.test(sauber)) return setFehler('Sechsstelligen Code eingeben.')
    setFehler(undefined)
    aktivieren.mutateAsync({ code: sauber }).then(
      (antwort) => setCodes(antwort.codes),
      (f: Error) => setFehler(feldFehler(f, 'code') ?? f.message),
    )
  }

  if (codes) return <CodesDialog titel="Zwei-Faktor-Anmeldung aktiv" codes={codes} onSchliessen={onSchliessen} />

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>Zwei-Faktor-Anmeldung einrichten</DialogTitle>
        </DialogHeader>
        {schritt === 1 ? (
          <div className="flex flex-col gap-3 text-sm">
            <p className="m-0">Schritt 1 von 2: Scanne den QR-Code mit deiner Authenticator-App.</p>
            {einrichtung ? (
              <>
                <div className="self-center rounded-lg bg-white p-3">
                  <QRCodeSVG value={einrichtung.otpAuthUri} size={168} title="QR-Code für die Authenticator-App" />
                </div>
                <div>
                  <span className="gedaempft text-[13px]">Oder Schlüssel eingeben:</span>
                  <div className="mono" aria-label="Schlüssel">
                    {einrichtung.schluessel.replace(/(.{4})(?=.)/g, '$1 ')}
                  </div>
                </div>
              </>
            ) : (
              <p className="gedaempft m-0">Lädt …</p>
            )}
            <DialogFooter>
              <Button type="button" onClick={onSchliessen}>
                Abbrechen
              </Button>
              <Button type="button" variant="primaer" disabled={!einrichtung} onClick={() => setSchritt(2)}>
                Weiter
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <form onSubmit={bestaetigen} noValidate className="flex flex-col gap-3 text-sm" aria-label="Code bestätigen">
            <p className="m-0">Schritt 2 von 2: Gib den sechsstelligen Code aus der App ein.</p>
            <Feld
              id="totp-code"
              label="Code"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              fehler={fehler}
            />
            <DialogFooter>
              <Button type="button" onClick={() => setSchritt(1)}>
                Zurück
              </Button>
              <Button type="submit" variant="primaer" laedt={aktivieren.isPending}>
                Aktivieren
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}

type Offen = 'passwort' | 'totp' | 'codesFrage' | 'deaktivierenFrage' | undefined

function KontoInhalt({ konto }: { konto: Konto }) {
  const toast = useToast()
  const { data: status } = useAuthStatus()
  const codesErzeugen = useWiederherstellungscodesErzeugen()
  const deaktivieren = useTotpDeaktivieren()
  const abmelden = useAndereAbmelden()
  const [offen, setOffen] = useState<Offen>()
  const [neueCodes, setNeueCodes] = useState<string[]>()
  const schliessen = () => setOffen(undefined)

  return (
    <>
      <NameUndEmail konto={konto} />
      <div className="flex flex-col">
        <Zeile
          titel="Passwort"
          beschreibung={
            konto.passwortGeaendertAm ? `zuletzt geändert am ${formatDatum(konto.passwortGeaendertAm)}` : 'nie geändert'
          }
        >
          <Button type="button" size="klein" onClick={() => setOffen('passwort')}>
            Ändern
          </Button>
        </Zeile>
        <Zeile
          titel="Zwei-Faktor-Anmeldung (TOTP)"
          beschreibung={
            konto.totpAktiv
              ? `aktiv · ${konto.wiederherstellungscodes} Wiederherstellungscodes übrig`
              : 'nicht eingerichtet'
          }
        >
          {konto.totpAktiv ? (
            <span className="flex gap-2">
              <Button type="button" size="klein" onClick={() => setOffen('codesFrage')}>
                Codes neu erzeugen
              </Button>
              <Button type="button" size="klein" className="text-gefahr" onClick={() => setOffen('deaktivierenFrage')}>
                Deaktivieren
              </Button>
            </span>
          ) : (
            <Button type="button" size="klein" onClick={() => setOffen('totp')}>
              Einrichten
            </Button>
          )}
        </Zeile>
        <Zeile titel="Google-Konto verknüpfen" beschreibung={konto.googleVerknuepft ? 'verknüpft' : 'nicht verknüpft'}>
          {!konto.googleVerknuepft && status?.googleAktiv && (
            <Button asChild size="klein">
              <a href={googleStartUrl(true)}>Verknüpfen</a>
            </Button>
          )}
        </Zeile>
        <Zeile
          titel="Aktive Sitzungen"
          beschreibung={konto.aktiveSitzungen === 1 ? '1 Gerät · dieses Gerät' : `${konto.aktiveSitzungen} Geräte`}
        >
          <Button
            type="button"
            size="klein"
            disabled={konto.aktiveSitzungen <= 1 || abmelden.isPending}
            laedt={abmelden.isPending}
            onClick={() =>
              abmelden.mutateAsync().then(() => toast.erfolg('Andere Geräte abgemeldet'), fehlerMelden(toast))
            }
          >
            Andere abmelden
          </Button>
        </Zeile>
      </div>
      {offen === 'passwort' && <PasswortDialog onSchliessen={schliessen} />}
      {offen === 'totp' && <TotpDialog onSchliessen={schliessen} />}
      {offen === 'codesFrage' && (
        <Rueckfrage
          titel="Neue Wiederherstellungscodes erzeugen?"
          text="Die bisherigen Codes werden ungültig."
          bestaetigen="Neu erzeugen"
          onAbbrechen={schliessen}
          onBestaetigen={() => {
            schliessen()
            codesErzeugen.mutateAsync().then((a) => setNeueCodes(a.codes), fehlerMelden(toast))
          }}
        />
      )}
      {offen === 'deaktivierenFrage' && (
        <Rueckfrage
          titel="Zwei-Faktor-Anmeldung deaktivieren?"
          text="Die Anmeldung braucht danach nur noch das Passwort. Die Wiederherstellungscodes werden ungültig."
          bestaetigen="Deaktivieren"
          gefaehrlich
          onAbbrechen={schliessen}
          onBestaetigen={() => {
            schliessen()
            deaktivieren
              .mutateAsync()
              .then(() => toast.erfolg('Zwei-Faktor-Anmeldung deaktiviert'), fehlerMelden(toast))
          }}
        />
      )}
      {neueCodes && (
        <CodesDialog
          titel="Neue Wiederherstellungscodes"
          codes={neueCodes}
          onSchliessen={() => setNeueCodes(undefined)}
        />
      )}
    </>
  )
}

/** Bereich „Mein Konto“ (jede Rolle). `google` stammt aus der Rückkehr der Google-Verknüpfung. */
export function KontoBereich({ google }: { google?: 'verknuepft' | 'vergeben' }) {
  const toast = useToast()
  const { data, isError, error } = useKonto()
  const gemeldet = useRef(false)

  useEffect(() => {
    if (!google || gemeldet.current) return
    gemeldet.current = true
    if (google === 'verknuepft') toast.erfolg('Google-Konto verknüpft')
    else toast.fehler('Dieses Google-Konto ist bereits mit einem anderen Benutzer verknüpft')
  }, [google, toast])

  return (
    <div className="flex flex-col gap-[18px]">
      <h2 className="m-0 text-lg font-semibold text-text">Mein Konto</h2>
      {data ? (
        <KontoInhalt konto={data} />
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : (
        <p className="gedaempft m-0 text-sm">Lädt …</p>
      )}
    </div>
  )
}
