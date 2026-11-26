import { useState, type FormEvent } from 'react'
import { useMailZugangSpeichern } from '@/api/einstellungen'
import type { MailZugang } from '@/api/typen'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { useRolle } from '@/components/shell/useRolle'
import { serverFeldfehler, type Feldfehler } from '../EinstellungsFormular'
import { Schalter } from '../Schalter'
import { Zeile } from '../Zeile'
import { Raster, ZahlFeld, ganzzahl } from './gemeinsam'

type Stand = { host: string; port: string; ssl: boolean; benutzer: string; passwort: string }

const ausZugang = (z: MailZugang): Stand => ({
  host: z.host ?? '',
  port: String(z.port),
  ssl: z.ssl,
  benutzer: z.benutzer ?? '',
  passwort: '',
})

/** Prüft wie der Server; das Passwort ist nur Pflicht, solange keins gespeichert ist. */
export function zugangPruefen(f: Stand, passwortGesetzt: boolean): Feldfehler {
  const fehler: Feldfehler = {}
  if (!f.host.trim()) fehler.host = 'Darf nicht leer sein.'
  if (ganzzahl(f.port, 1, 65535) === undefined) fehler.port = 'Erlaubt ist 1 bis 65535.'
  if (!f.benutzer.trim()) fehler.benutzer = 'Darf nicht leer sein.'
  if (!passwortGesetzt && !f.passwort) fehler.passwort = 'Darf nicht leer sein.'
  return fehler
}

function TextFeld({
  id,
  label,
  wert,
  fehler,
  onAendern,
  ...rest
}: {
  id: string
  label: string
  wert: string
  fehler?: string
  onAendern: (wert: string) => void
  type?: string
  autoComplete?: string
  placeholder?: string
}) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className="eingabe"
        aria-invalid={!!fehler}
        value={wert}
        onChange={(ev) => onAendern(ev.target.value)}
        {...rest}
      />
      {fehler && (
        <span role="alert" className="block text-[13px] text-gefahr">
          {fehler}
        </span>
      )}
    </div>
  )
}

/**
 * IMAP-Zugang, wenn er nicht per `.env` gesetzt ist. Das Passwort kommt nie zurück:
 * Ist eins gespeichert, bleibt es bei leerem Feld erhalten.
 */
export function MailZugangFormular({ zugang }: { zugang: MailZugang }) {
  const toast = useToast()
  const { istAdmin } = useRolle()
  const speichern = useMailZugangSpeichern()
  const vomServer = ausZugang(zugang)
  const [ausgang, setAusgang] = useState(vomServer)
  const [aktuell, setAktuell] = useState(vomServer)
  const [fehler, setFehler] = useState<Feldfehler>({})

  // Neue Werte vom Server (nach dem Speichern oder Neuladen) übernehmen.
  if (JSON.stringify(vomServer) !== JSON.stringify(ausgang)) {
    setAusgang(vomServer)
    setAktuell(vomServer)
    setFehler({})
  }

  const geaendert = JSON.stringify(aktuell) !== JSON.stringify(ausgang)
  const schreibgeschuetzt = !istAdmin
  const setze = (teil: Partial<Stand>) => setAktuell((alt) => ({ ...alt, ...teil }))

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    if (!geaendert || schreibgeschuetzt) return
    const pruefung = zugangPruefen(aktuell, zugang.passwortGesetzt)
    setFehler(pruefung)
    if (Object.keys(pruefung).length > 0) return
    speichern
      .mutateAsync({
        host: aktuell.host.trim(),
        port: Number(aktuell.port),
        ssl: aktuell.ssl,
        benutzer: aktuell.benutzer.trim(),
        passwort: aktuell.passwort || null,
      })
      .then(
        () => {
          toast.erfolg('Gespeichert')
          setAktuell((alt) => ({ ...alt, passwort: '' }))
        },
        (f: Error) => {
          const feldfehler = serverFeldfehler(f)
          if (feldfehler) setFehler(feldfehler)
          toast.fehler(f.message)
        },
      )
  }

  return (
    <form
      onSubmit={absenden}
      noValidate
      className="flex flex-col gap-[18px] border-b border-linie pb-[18px]"
      aria-label="IMAP-Zugang"
    >
      <div>
        <h3 className="m-0 text-[15px] font-semibold text-text">IMAP-Zugang</h3>
        <p className="gedaempft m-0 mt-1 text-[13px]">
          Das Passwort wird verschlüsselt gespeichert und nie angezeigt. Ist <span className="mono">IMAP_HOST</span> in
          der .env gesetzt, gilt stattdessen die .env.
        </p>
      </div>
      <fieldset disabled={schreibgeschuetzt} className="m-0 flex min-w-0 flex-col gap-4 border-0 p-0">
        <Raster>
          <TextFeld
            id="m-host"
            label="IMAP-Server"
            wert={aktuell.host}
            fehler={fehler.host}
            placeholder="imap.example.de"
            autoComplete="off"
            onAendern={(host) => setze({ host })}
          />
          <ZahlFeld
            id="m-port"
            label="Port"
            wert={aktuell.port}
            fehler={fehler.port}
            onAendern={(port) => setze({ port })}
          />
          <TextFeld
            id="m-zugang-benutzer"
            label="Benutzer"
            wert={aktuell.benutzer}
            fehler={fehler.benutzer}
            autoComplete="username"
            onAendern={(benutzer) => setze({ benutzer })}
          />
          <TextFeld
            id="m-passwort"
            label="Passwort"
            type="password"
            wert={aktuell.passwort}
            fehler={fehler.passwort}
            autoComplete="new-password"
            placeholder={zugang.passwortGesetzt ? 'gespeichert – leer lassen zum Behalten' : 'App-Passwort'}
            onAendern={(passwort) => setze({ passwort })}
          />
        </Raster>
        <Zeile
          titel="SSL/TLS"
          beschreibung="Verschlüsselt ab Verbindungsaufbau (Port 993). Aus: STARTTLS, wenn der Server es anbietet."
        >
          <Schalter label="SSL/TLS" an={aktuell.ssl} disabled={schreibgeschuetzt} onAendern={(ssl) => setze({ ssl })} />
        </Zeile>
      </fieldset>
      {!schreibgeschuetzt && (
        <div className="flex justify-end gap-2">
          <Button
            type="button"
            disabled={!geaendert || speichern.isPending}
            onClick={() => {
              setAktuell(ausgang)
              setFehler({})
            }}
          >
            Verwerfen
          </Button>
          <Button
            type="submit"
            variant="primaer"
            disabled={!geaendert || speichern.isPending}
            laedt={speichern.isPending}
          >
            Speichern
          </Button>
        </div>
      )}
    </form>
  )
}
