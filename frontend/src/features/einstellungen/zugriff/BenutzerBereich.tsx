import { useState, type FormEvent } from 'react'
import { useAuthStatus } from '@/api/auth'
import {
  useBenutzer,
  useBenutzerAblehnen,
  useBenutzerAendern,
  useBenutzerAnlegen,
  useBenutzerFreigeben,
  usePasswortZuruecksetzen,
} from '@/api/benutzer'
import { useEinstellungen, useEinstellungenSpeichern } from '@/api/einstellungen'
import type { Benutzer, Rolle, ZugriffEinstellungen } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { formatDatum } from '@/lib/format'
import { Schalter } from '../Schalter'
import { Zeile } from '../Zeile'
import { Feld, MINDEST_LAENGE, Rueckfrage, rollen, rollenText } from './gemeinsam'
import { SkelettTabelle } from '@/components/ui/skelett'

const FUENF_MINUTEN = 5 * 60 * 1000

/** „jetzt“ bei unter fünf Minuten, sonst Datum, ohne Wert „—“. */
export function zuletztAktiv(iso?: string | null, jetzt = Date.now()): string {
  if (!iso) return '—'
  return jetzt - new Date(iso).getTime() < FUENF_MINUTEN ? 'jetzt' : formatDatum(iso)
}

/** Wartend: nie freigegeben (noch ohne Rolle); deaktiviert: Rolle vorhanden, aber nicht aktiv. */
const wartet = (b: Benutzer) => !b.freigegeben && !b.rolle

function StatusChip({ benutzer }: { benutzer: Benutzer }) {
  if (benutzer.freigegeben) return <Chip ton="gruen">aktiv</Chip>
  if (wartet(benutzer)) return <Chip ton="orange">wartet auf Freigabe</Chip>
  return <Chip ton="grau">deaktiviert</Chip>
}

function ZweiFaktorChip({ benutzer }: { benutzer: Benutzer }) {
  if (benutzer.totpAktiv) return <Chip ton="gruen">TOTP</Chip>
  if (benutzer.anmeldung === 'Google') return <Chip ton="grau">über Google</Chip>
  return <Chip ton="grau">nicht eingerichtet</Chip>
}

const fehlerMelden = (toast: ReturnType<typeof useToast>) => (f: Error) => void toast.fehler(f.message)

/** Hinweisbalken eines wartenden Kontos mit Rollenauswahl, „Ablehnen“ und „Freigeben“. */
function FreigabeBalken({ benutzer }: { benutzer: Benutzer }) {
  const toast = useToast()
  const freigeben = useBenutzerFreigeben()
  const ablehnen = useBenutzerAblehnen()
  const [rolle, setRolle] = useState<Rolle>('Benutzer')
  const [rueckfrage, setRueckfrage] = useState(false)
  return (
    <div
      role="alert"
      className="flex flex-wrap items-center gap-3 rounded-[10px] bg-blau-flaeche px-3.5 py-3 text-sm text-blau-text"
    >
      <Icon name="person_add" />
      <span className="flex-[1_1_240px]">
        <strong>{benutzer.name}</strong> hat sich registriert und wartet auf Freigabe.
      </span>
      <select
        className="eingabe h-8 w-auto text-[13px]"
        aria-label={`Rolle für ${benutzer.name}`}
        value={rolle}
        onChange={(e) => setRolle(e.target.value as Rolle)}
      >
        {rollen.map((r) => (
          <option key={r.wert} value={r.wert}>
            {r.text}
          </option>
        ))}
      </select>
      <Button type="button" size="klein" onClick={() => setRueckfrage(true)} laedt={ablehnen.isPending}>
        Ablehnen
      </Button>
      <Button
        type="button"
        size="klein"
        variant="primaer"
        laedt={freigeben.isPending}
        onClick={() =>
          freigeben
            .mutateAsync({ id: benutzer.id, rolle })
            .then(() => toast.erfolg(`${benutzer.name} freigegeben`), fehlerMelden(toast))
        }
      >
        Freigeben
      </Button>
      {rueckfrage && (
        <Rueckfrage
          titel={`Registrierung von ${benutzer.name} ablehnen?`}
          text="Das Konto wird gelöscht. Die Person kann sich erneut registrieren."
          bestaetigen="Ablehnen"
          gefaehrlich
          onAbbrechen={() => setRueckfrage(false)}
          onBestaetigen={() => {
            setRueckfrage(false)
            ablehnen
              .mutateAsync(benutzer.id)
              .then(() => toast.erfolg(`Registrierung von ${benutzer.name} abgelehnt`), fehlerMelden(toast))
          }}
        />
      )}
    </div>
  )
}

/** Dialog „Benutzer anlegen“. */
function AnlegenDialog({ onSchliessen }: { onSchliessen: () => void }) {
  const toast = useToast()
  const anlegen = useBenutzerAnlegen()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [passwort, setPasswort] = useState('')
  const [rolle, setRolle] = useState<Rolle>('Benutzer')
  const [fehler, setFehler] = useState<Record<string, string>>({})

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    const f: Record<string, string> = {}
    if (!name.trim()) f.name = 'Name ist erforderlich.'
    if (!email.includes('@')) f.email = 'Keine gültige E-Mail-Adresse.'
    if (passwort.length < MINDEST_LAENGE) f.passwort = `Mindestens ${MINDEST_LAENGE} Zeichen.`
    setFehler(f)
    if (Object.keys(f).length > 0) return
    anlegen.mutateAsync({ name: name.trim(), email: email.trim(), passwort, rolle }).then(() => {
      toast.erfolg(`${name.trim()} angelegt`)
      onSchliessen()
    }, fehlerMelden(toast))
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>Benutzer anlegen</DialogTitle>
        </DialogHeader>
        <form onSubmit={absenden} noValidate className="flex flex-col gap-3" aria-label="Benutzer anlegen">
          <Feld
            id="anlegen-name"
            label="Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fehler={fehler.name}
          />
          <Feld
            id="anlegen-email"
            label="E-Mail-Adresse"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            fehler={fehler.email}
          />
          <Feld
            id="anlegen-passwort"
            label="Passwort"
            type="password"
            autoComplete="new-password"
            value={passwort}
            onChange={(e) => setPasswort(e.target.value)}
            fehler={fehler.passwort}
          />
          <div>
            <label className="label" htmlFor="anlegen-rolle">
              Rolle
            </label>
            <select
              id="anlegen-rolle"
              className="eingabe"
              value={rolle}
              onChange={(e) => setRolle(e.target.value as Rolle)}
            >
              {rollen.map((r) => (
                <option key={r.wert} value={r.wert}>
                  {r.text}
                </option>
              ))}
            </select>
          </div>
          <DialogFooter>
            <Button type="button" onClick={onSchliessen}>
              Abbrechen
            </Button>
            <Button type="submit" variant="primaer" laedt={anlegen.isPending}>
              Anlegen
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Dialog „Passwort zurücksetzen“ eines anderen Benutzers. */
function ZuruecksetzenDialog({ benutzer, onSchliessen }: { benutzer: Benutzer; onSchliessen: () => void }) {
  const toast = useToast()
  const zuruecksetzen = usePasswortZuruecksetzen()
  const [passwort, setPasswort] = useState('')
  const [fehler, setFehler] = useState<string>()

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    if (passwort.length < MINDEST_LAENGE) return setFehler(`Mindestens ${MINDEST_LAENGE} Zeichen.`)
    setFehler(undefined)
    zuruecksetzen.mutateAsync({ id: benutzer.id, neuesPasswort: passwort }).then(() => {
      toast.erfolg(`Passwort von ${benutzer.name} zurückgesetzt`)
      onSchliessen()
    }, fehlerMelden(toast))
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>Passwort von {benutzer.name} zurücksetzen</DialogTitle>
        </DialogHeader>
        <form onSubmit={absenden} noValidate className="flex flex-col gap-3" aria-label="Passwort zurücksetzen">
          <Feld
            id="zuruecksetzen-passwort"
            label="Neues Passwort"
            type="password"
            autoComplete="new-password"
            value={passwort}
            onChange={(e) => setPasswort(e.target.value)}
            fehler={fehler}
          />
          <DialogFooter>
            <Button type="button" onClick={onSchliessen}>
              Abbrechen
            </Button>
            <Button type="submit" variant="primaer" laedt={zuruecksetzen.isPending}>
              Zurücksetzen
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Tabellenzeile mit direkt änderbarer Rolle und Zeilenmenü. */
function BenutzerZeile({ benutzer, eigener }: { benutzer: Benutzer; eigener: boolean }) {
  const toast = useToast()
  const aendern = useBenutzerAendern()
  const [zuruecksetzen, setZuruecksetzen] = useState(false)
  const aendernMit = (rolle: Rolle, aktiv: boolean) =>
    aendern.mutateAsync({ id: benutzer.id, rolle, aktiv }).then(() => toast.erfolg('Gespeichert'), fehlerMelden(toast))

  return (
    <tr>
      <td>
        <strong>{benutzer.name}</strong>
        <br />
        <span className="gedaempft text-xs">{benutzer.email}</span>
      </td>
      <td>{benutzer.anmeldung}</td>
      <td>
        {benutzer.rolle ? (
          <select
            className="eingabe h-8 w-auto text-[13px]"
            aria-label={`Rolle von ${benutzer.name}`}
            value={benutzer.rolle}
            disabled={eigener || aendern.isPending}
            aria-busy={aendern.isPending || undefined}
            onChange={(e) => void aendernMit(e.target.value as Rolle, benutzer.freigegeben)}
          >
            {rollen.map((r) => (
              <option key={r.wert} value={r.wert}>
                {r.text}
              </option>
            ))}
          </select>
        ) : (
          <span className="gedaempft">{rollenText(benutzer.rolle)}</span>
        )}
      </td>
      <td>
        <ZweiFaktorChip benutzer={benutzer} />
      </td>
      <td>
        <StatusChip benutzer={benutzer} />
      </td>
      <td>{zuletztAktiv(benutzer.zuletztAktiv)}</td>
      <td>
        {benutzer.rolle && (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button type="button" variant="geist" size="icon-klein" aria-label={`Aktionen für ${benutzer.name}`}>
                <Icon name="more_vert" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => setZuruecksetzen(true)}>Passwort zurücksetzen …</DropdownMenuItem>
              {benutzer.freigegeben ? (
                <DropdownMenuItem disabled={eigener} onSelect={() => void aendernMit(benutzer.rolle!, false)}>
                  Deaktivieren
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem onSelect={() => void aendernMit(benutzer.rolle!, true)}>Aktivieren</DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
        {zuruecksetzen && <ZuruecksetzenDialog benutzer={benutzer} onSchliessen={() => setZuruecksetzen(false)} />}
      </td>
    </tr>
  )
}

/** Schalter „Offene Registrierung“ und „Google-Anmeldung“ (Gruppe `zugriff`, sofort speichernd). */
function ZugriffSchalter() {
  const toast = useToast()
  const { data } = useEinstellungen()
  const speichern = useEinstellungenSpeichern()
  if (!data) return null
  const zugriff = data.zugriff
  const googleMoeglich = data.umgebung.googleKonfiguriert
  const setze = (teil: Partial<ZugriffEinstellungen>) =>
    speichern
      .mutateAsync({ gruppe: 'zugriff', werte: { ...zugriff, ...teil } })
      .then(() => toast.erfolg('Gespeichert'), fehlerMelden(toast))

  return (
    <div className="flex flex-col">
      <Zeile
        titel="Offene Registrierung"
        beschreibung="Jeder im LAN kann ein Konto anlegen; es bleibt bis zur Freigabe gesperrt."
      >
        <Schalter
          label="Offene Registrierung"
          an={zugriff.offeneRegistrierung}
          disabled={speichern.isPending}
          onAendern={(an) => void setze({ offeneRegistrierung: an })}
        />
      </Zeile>
      <Zeile
        titel="Google-Anmeldung"
        beschreibung={
          googleMoeglich
            ? 'OIDC-Client aus der .env · Redirect unter der LAN-Domain'
            : 'Kein OIDC-Client in der .env eingetragen – Google-Anmeldung ist nicht verfügbar.'
        }
      >
        <Schalter
          label="Google-Anmeldung"
          an={googleMoeglich && zugriff.googleAnmeldung}
          disabled={!googleMoeglich || speichern.isPending}
          onAendern={(an) => void setze({ googleAnmeldung: an })}
        />
      </Zeile>
    </div>
  )
}

function Verwaltung() {
  const { data: status } = useAuthStatus()
  const { data: liste, isError, error } = useBenutzer()
  const eigeneId = status?.benutzer?.id

  return (
    <>
      {liste ? (
        <>
          {liste.filter(wartet).map((b) => (
            <FreigabeBalken key={b.id} benutzer={b} />
          ))}
          <div className="overflow-x-auto">
            <table className="tabelle min-w-[640px]" aria-label="Benutzer">
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Anmeldung</th>
                  <th scope="col">Rolle</th>
                  <th scope="col">2FA</th>
                  <th scope="col">Status</th>
                  <th scope="col">Zuletzt aktiv</th>
                  <th scope="col">
                    <span className="sr-only">Aktion</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {liste.map((b) => (
                  <BenutzerZeile key={b.id} benutzer={b} eigener={b.id === eigeneId} />
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : (
        <SkelettTabelle zeilen={4} spalten={4} label="Benutzer werden geladen" />
      )}
      <ZugriffSchalter />
    </>
  )
}

/** Bereich „Benutzer & Freigaben“ (nur Admins). */
export function BenutzerBereich() {
  const { istAdmin } = useRolle()
  const [anlegen, setAnlegen] = useState(false)
  return (
    <div className="flex flex-col gap-3.5">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <h2 className="m-0 text-lg font-semibold text-text">Benutzer &amp; Freigaben</h2>
        {istAdmin && (
          <Button type="button" variant="primaer" onClick={() => setAnlegen(true)}>
            <Icon name="person_add" />
            Benutzer anlegen
          </Button>
        )}
      </div>
      {anlegen && <AnlegenDialog onSchliessen={() => setAnlegen(false)} />}
      {istAdmin ? (
        <Verwaltung />
      ) : (
        <p role="note" className="m-0 rounded-[10px] bg-flaeche px-3.5 py-3 text-sm text-gedaempft-stark">
          Nur Admins können Benutzer verwalten.
        </p>
      )}
    </div>
  )
}
