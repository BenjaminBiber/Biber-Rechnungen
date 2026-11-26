import { useQueryErrorResetBoundary } from '@tanstack/react-query'
import { Link, useCanGoBack, useRouter, useRouterState, type ErrorComponentProps } from '@tanstack/react-router'
import { useEffect, useEffectEvent, useId, useState, type ReactNode } from 'react'
import { istApiFehler } from '@/api/client'
import { BiberAnimation } from './BiberAnimation'
import { Icon } from '@/components/Icon'
import { Logo } from '@/components/Logo'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/** Sekunden bis zum automatischen neuen Versuch. */
export const WIEDERHOLEN_NACH = 15

/** Wie ein Fehler dem Benutzer erklärt wird. */
export type Fehlerart = {
  /** Kurzer Code im Chip neben dem Biber, z. B. „502“ oder „Offline“. */
  code: string
  /** Statusbezeichnung für die technischen Details, z. B. „Bad Gateway“. */
  grund: string
  titel: string
  text: string
  /** Farbe des Codes: orange für vorübergehende, rot für echte Serverfehler, grau für 4xx. */
  ton: 'orange' | 'rot' | 'grau'
  /**
   * Hauptaktion: `automatisch` versucht es nach {@link WIEDERHOLEN_NACH} s selbst erneut, `erneut` nur per Knopf,
   * `neuLaden` lädt die Seite (neue Version nach einem Update), `weg` führt zum Dashboard oder zurück.
   */
  aktion: 'automatisch' | 'erneut' | 'neuLaden' | 'weg'
}

const GRUENDE: Record<number, string> = {
  400: 'Bad Request',
  401: 'Unauthorized',
  403: 'Forbidden',
  404: 'Not Found',
  409: 'Conflict',
  410: 'Gone',
  413: 'Content Too Large',
  422: 'Unprocessable Content',
  429: 'Too Many Requests',
  500: 'Internal Server Error',
  502: 'Bad Gateway',
  503: 'Service Unavailable',
  504: 'Gateway Timeout',
}

const NICHT_GEFUNDEN = {
  titel: 'Diese Seite gibt es nicht',
  text: 'Vielleicht wurde der Beleg gelöscht oder zusammengeführt, oder der Link ist unvollständig.',
}

const veralteteVersion =
  /dynamically imported module|Importing a module script failed|Failed to load module script|ChunkLoadError/i

/** Ordnet einen geworfenen Fehler einer verständlichen Meldung zu. */
export function fehlerart(fehler: unknown): Fehlerart {
  if (istApiFehler(fehler) && fehler.status > 0) {
    const status = fehler.status
    const code = String(status)
    const grund = GRUENDE[status] ?? ''
    if (status === 404) return { code, grund, ...NICHT_GEFUNDEN, ton: 'grau', aktion: 'weg' }
    if (status === 401 || status === 403)
      return {
        code,
        grund,
        titel: 'Dafür fehlen dir die Rechte',
        text: 'Diesen Bereich dürfen nur bestimmte Benutzer sehen. Ein Admin kann ihn für dich freigeben.',
        ton: 'grau',
        aktion: 'weg',
      }
    if (status === 502 || status === 504)
      return {
        code,
        grund,
        titel: 'Der Server antwortet gerade nicht',
        text: 'Biber-Rechnungen ist im Moment nicht erreichbar. Meist dauert das nur ein paar Sekunden, zum Beispiel nach einem Neustart oder Update.',
        ton: 'orange',
        aktion: 'automatisch',
      }
    if (status === 503)
      return {
        code,
        grund,
        titel: 'Kurz in Wartung',
        text: 'Gerade läuft ein Update oder eine Wartung. Gleich geht es weiter, du musst nichts tun.',
        ton: 'orange',
        aktion: 'automatisch',
      }
    if (status >= 500)
      return {
        code,
        grund,
        titel: 'Da ist etwas schiefgelaufen',
        text: 'Beim Verarbeiten deiner Anfrage ist ein interner Fehler aufgetreten. Er wurde protokolliert.',
        ton: 'rot',
        aktion: 'automatisch',
      }
    return { code, grund, titel: 'Anfrage fehlgeschlagen', text: fehler.titel, ton: 'grau', aktion: 'erneut' }
  }

  const nachricht = fehler instanceof Error ? fehler.message : String(fehler)
  if (veralteteVersion.test(nachricht))
    return {
      code: 'Update',
      grund: '',
      titel: 'Neue Version verfügbar',
      text: 'Biber-Rechnungen wurde aktualisiert. Lade die Seite neu, um die aktuelle Version zu verwenden.',
      ton: 'grau',
      aktion: 'neuLaden',
    }
  if (istApiFehler(fehler) || (fehler instanceof TypeError && /fetch|network|load failed/i.test(nachricht)))
    return {
      code: 'Offline',
      grund: '',
      titel: 'Keine Verbindung zum Server',
      text: 'Biber-Rechnungen ist gerade nicht erreichbar. Prüfe deine Internetverbindung, sobald der Server antwortet, geht es weiter.',
      ton: 'orange',
      aktion: 'automatisch',
    }
  return {
    code: 'Fehler',
    grund: '',
    titel: 'Da ist etwas schiefgelaufen',
    text: 'Beim Anzeigen dieser Seite ist ein unerwarteter Fehler aufgetreten.',
    ton: 'rot',
    aktion: 'erneut',
  }
}

const TON_KLASSE: Record<Fehlerart['ton'], string> = {
  orange: 'text-orange-text',
  rot: 'text-rot-text',
  grau: 'text-grau-text',
}

/** Größere Knöpfe als im Rest der App (Design „Fehlerseite“: 44 px hoch). */
const GROSS = 'h-11 gap-2 rounded-[9px] px-[18px] text-[15px]'

const zeitpunktFormat = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})

type Detail = { name: string; wert: string; mehrzeilig?: boolean }

/**
 * Gemeinsamer Rahmen der Fehlerseiten: Biber im Kreis mit Code-Chip, daneben Titel, Text, Aktionen und
 * aufklappbare technische Details. In der Shell (`vollbild = false`) ersetzt er nur den Seiteninhalt;
 * als Vollbild (ohne Shell, z. B. wenn der Server beim Start nicht antwortet) bringt er Logo und Fläche mit.
 */
function FehlerRahmen({
  vollbild,
  art,
  aktionen,
  countdown,
  details,
  alarm = false,
}: {
  vollbild: boolean
  art: Pick<Fehlerart, 'code' | 'titel' | 'text' | 'ton'>
  aktionen: ReactNode
  countdown?: ReactNode
  details: Detail[]
  alarm?: boolean
}) {
  const inhalt = (
    <div className="flex w-full max-w-[980px] flex-wrap items-center justify-center gap-x-[72px] gap-y-12">
      <div
        aria-hidden="true"
        className="relative flex size-[300px] max-w-full flex-none items-center justify-center rounded-full bg-blau-flaeche"
      >
        <BiberAnimation code={art.code} ton={art.ton} />
        <span
          className={cn(
            'absolute top-[34px] right-1.5 inline-flex h-[34px] items-center rounded-[17px] bg-karte px-3.5 font-mono text-[15px] font-semibold shadow-[0_4px_14px_rgba(21,25,34,0.12)]',
            TON_KLASSE[art.ton],
          )}
        >
          {art.code}
        </span>
      </div>

      <div className="flex max-w-[500px] flex-[1_1_360px] flex-col gap-[18px]">
        {/* Nur Titel und Text als Alarm: der Countdown darin würde sonst jede Sekunde vorgelesen. */}
        <div role={alarm ? 'alert' : undefined} className="flex flex-col gap-2.5">
          <h1 className="m-0 text-[34px] leading-[1.2] font-semibold text-text">{art.titel}</h1>
          <p className="m-0 text-base leading-[1.6] text-gedaempft-stark">{art.text}</p>
        </div>
        {countdown}
        <div className="mt-0.5 flex flex-wrap gap-2.5">{aktionen}</div>
        <TechnischeDetails details={details} />
      </div>
    </div>
  )

  if (!vollbild) return <div className="seite flex min-h-full items-center justify-center py-10">{inhalt}</div>

  return (
    <div className="flex min-h-screen flex-col bg-flaeche font-sans text-text">
      <header className="px-8 py-6">
        <Link to="/" className="inline-flex items-center gap-2.5 text-marke no-underline">
          <Logo className="h-[26px] w-11" />
          <span className="text-base font-semibold text-text">Biber-Rechnungen</span>
        </Link>
      </header>
      <main className="box-border flex flex-1 items-center justify-center px-6 pt-6 pb-[72px]">{inhalt}</main>
    </div>
  )
}

/**
 * Wann zuletzt Details offen zu sehen waren. Schlägt ein erneuter Versuch fehl, baut der Router die Fehlerseite
 * neu auf (die neue rendert, bevor die alte abgebaut ist); die Details bleiben dann offen, statt alle 15 s
 * zuzuklappen. Eine spätere, unabhängige Fehlerseite startet wieder zugeklappt.
 */
let detailsZuletztOffen = 0
const DETAILS_MERKEN_MS = 60_000

/** Aufklappbare Liste mit Status, Zeitpunkt, Anfrage und Anfrage-ID, kopierbar für den Admin. */
function TechnischeDetails({ details }: { details: Detail[] }) {
  const [offen, setOffen] = useState(() => Date.now() - detailsZuletztOffen < DETAILS_MERKEN_MS)
  useEffect(() => {
    if (!offen) {
      detailsZuletztOffen = 0
      return
    }
    const merken = () => {
      detailsZuletztOffen = Date.now()
    }
    merken()
    const timer = setInterval(merken, 1000)
    return () => clearInterval(timer)
  }, [offen])
  const [kopiert, setKopiert] = useState(false)
  const id = useId()
  const hatAnfrageId = details.some((d) => d.name === 'Anfrage-ID')

  useEffect(() => {
    if (!kopiert) return
    const t = setTimeout(() => setKopiert(false), 2000)
    return () => clearTimeout(t)
  }, [kopiert])

  async function kopieren() {
    await navigator.clipboard?.writeText(details.map((d) => `${d.name}: ${d.wert}`).join('\n'))
    setKopiert(true)
  }

  return (
    <div className="flex flex-col gap-2.5 border-t border-rand pt-3.5">
      <button
        type="button"
        aria-expanded={offen}
        aria-controls={id}
        onClick={() => setOffen(!offen)}
        className="-ml-1 inline-flex h-8 cursor-pointer items-center gap-1 self-start rounded-md border-0 bg-transparent pr-2 pl-1 text-[13px] font-medium text-gedaempft-stark hover:bg-segment hover:text-text"
      >
        <Icon
          name="chevron_right"
          className={cn('text-[20px] transition-transform duration-200', offen && 'rotate-90')}
        />
        Technische Details
      </button>
      {offen && (
        <div id={id} className="flex flex-col gap-3 rounded-[10px] border border-rand bg-karte px-4 py-3.5">
          <dl className="m-0 grid grid-cols-[110px_minmax(0,1fr)] gap-x-3.5 gap-y-2 text-[13px]">
            {details.map((d) => (
              <div key={d.name} className="contents">
                <dt className="text-gedaempft">{d.name}</dt>
                <dd
                  className={cn(
                    'm-0 font-mono wrap-anywhere text-text',
                    d.mehrzeilig && 'max-h-48 overflow-auto whitespace-pre-wrap',
                  )}
                >
                  {d.wert}
                </dd>
              </div>
            ))}
          </dl>
          <Button size="klein" className="self-start" onClick={kopieren}>
            <Icon name={kopiert ? 'check' : 'content_copy'} className="text-[16px]" />
            {kopiert ? 'Kopiert' : 'Details kopieren'}
          </Button>
        </div>
      )}
      <p className="m-0 text-[13px] text-gedaempft">
        Besteht das Problem weiter? Schick {hatAnfrageId ? 'die Anfrage-ID' : 'die technischen Details'} an deinen
        Admin.
      </p>
    </div>
  )
}

/** Countdown bis zum automatischen Versuch, mit Fortschrittsbalken. */
function Countdown({ rest }: { rest: number }) {
  const prozent = Math.round(((WIEDERHOLEN_NACH - rest) / WIEDERHOLEN_NACH) * 100)
  return (
    <div role="timer" className="flex max-w-[360px] flex-col gap-1.5">
      <span className="text-[13px] text-gedaempft-stark">
        Neuer Versuch automatisch in <strong className="font-semibold text-text tabular-nums">{rest} s</strong>
      </span>
      <span aria-hidden="true" className="h-1 overflow-hidden rounded-sm bg-rand">
        <span
          className="block h-1 bg-akzent transition-[width] duration-1000 ease-linear"
          style={{ width: `${prozent}%` }}
        />
      </span>
    </div>
  )
}

/** Zählt von {@link WIEDERHOLEN_NACH} herunter und ruft bei 0 `beiNull` auf; `neuStarten` setzt zurück. */
function useCountdown(aktiv: boolean, beiNull: () => void) {
  const [rest, setRest] = useState(WIEDERHOLEN_NACH)
  const [lauf, setLauf] = useState(0)
  const ablaufen = useEffectEvent(beiNull)

  useEffect(() => {
    if (!aktiv) return
    let sekunden = WIEDERHOLEN_NACH
    const timer = setInterval(() => {
      sekunden -= 1
      if (sekunden <= 0) {
        sekunden = WIEDERHOLEN_NACH
        ablaufen()
      }
      setRest(sekunden)
    }, 1000)
    return () => clearInterval(timer)
  }, [aktiv, lauf])

  function neuStarten() {
    setRest(WIEDERHOLEN_NACH)
    setLauf((l) => l + 1)
  }

  return { rest, neuStarten }
}

function ZumDashboard({ primaer }: { primaer: boolean }) {
  return (
    <Button asChild variant={primaer ? 'primaer' : 'standard'} className={GROSS}>
      <Link to="/">
        <Icon name="space_dashboard" className="text-[20px]" />
        Zum Dashboard
      </Link>
    </Button>
  )
}

function Zurueck() {
  const router = useRouter()
  const kannZurueck = useCanGoBack()
  if (!kannZurueck) return null
  return (
    <Button className={GROSS} onClick={() => router.history.back()}>
      <Icon name="arrow_back" className="text-[20px]" />
      Zurück
    </Button>
  )
}

/** Uhrzeit beim ersten Anzeigen und die aktuelle Adresse, für die technischen Details. */
function useKontext() {
  const [zeitpunkt] = useState(() => zeitpunktFormat.format(new Date()))
  const seite = useRouterState({ select: (s) => s.location.href })
  return { zeitpunkt, seite }
}

/** 404 für unbekannte Adressen; in der Shell (`_app`) oder als Vollbild (Root). */
export function SeiteNichtGefunden({ vollbild = false }: { vollbild?: boolean }) {
  const { zeitpunkt, seite } = useKontext()
  return (
    <FehlerRahmen
      vollbild={vollbild}
      art={{ code: '404', ton: 'grau', ...NICHT_GEFUNDEN }}
      aktionen={
        <>
          <ZumDashboard primaer />
          <Zurueck />
        </>
      }
      details={[
        { name: 'Status', wert: '404 Not Found' },
        { name: 'Zeitpunkt', wert: zeitpunkt },
        { name: 'Seite', wert: seite },
      ]}
    />
  )
}

/** Fehlerseite für Ausnahmen beim Laden oder Rendern einer Route (`errorComponent`). */
export function Fehlerseite({ error, reset, vollbild = false }: ErrorComponentProps & { vollbild?: boolean }) {
  const router = useRouter()
  const abfragen = useQueryErrorResetBoundary()
  const art = fehlerart(error)
  const { zeitpunkt, seite } = useKontext()

  function erneutVersuchen() {
    abfragen.reset()
    reset()
    void router.invalidate()
  }

  const countdown = useCountdown(art.aktion === 'automatisch', erneutVersuchen)

  const details: Detail[] = [
    { name: 'Status', wert: [art.code, art.grund].filter(Boolean).join(' ') },
    { name: 'Zeitpunkt', wert: zeitpunkt },
  ]
  if (istApiFehler(error)) {
    details.push({ name: 'Anfrage', wert: error.anfrage ?? seite })
    const anfrageId = error.problem.traceId ?? error.problem.requestId
    if (typeof anfrageId === 'string' && anfrageId) details.push({ name: 'Anfrage-ID', wert: anfrageId })
    if (error.titel && art.aktion !== 'automatisch') details.push({ name: 'Meldung', wert: error.titel })
  } else {
    details.push({ name: 'Seite', wert: seite })
    // In der Entwicklung der Stack (beginnt mit der Meldung), sonst nur die Meldung.
    const meldung =
      error instanceof Error
        ? import.meta.env.DEV && error.stack
          ? error.stack
          : `${error.name}: ${error.message}`
        : String(error)
    details.push({ name: 'Meldung', wert: meldung, mehrzeilig: true })
  }

  let aktionen: ReactNode
  if (art.aktion === 'automatisch')
    aktionen = (
      <>
        <Button
          variant="primaer"
          className={GROSS}
          onClick={() => {
            countdown.neuStarten()
            erneutVersuchen()
          }}
        >
          <Icon name="refresh" className="text-[20px]" />
          Jetzt erneut versuchen
        </Button>
        <ZumDashboard primaer={false} />
      </>
    )
  else if (art.aktion === 'erneut' || art.aktion === 'neuLaden')
    aktionen = (
      <>
        <Button
          variant="primaer"
          className={GROSS}
          onClick={art.aktion === 'neuLaden' ? () => window.location.reload() : erneutVersuchen}
        >
          <Icon name="refresh" className="text-[20px]" />
          {art.aktion === 'neuLaden' ? 'Seite neu laden' : 'Erneut versuchen'}
        </Button>
        <ZumDashboard primaer={false} />
      </>
    )
  else
    aktionen = (
      <>
        <ZumDashboard primaer />
        <Zurueck />
      </>
    )

  return (
    <FehlerRahmen
      vollbild={vollbild}
      alarm
      art={art}
      countdown={art.aktion === 'automatisch' ? <Countdown rest={countdown.rest} /> : undefined}
      aktionen={aktionen}
      details={details}
    />
  )
}
