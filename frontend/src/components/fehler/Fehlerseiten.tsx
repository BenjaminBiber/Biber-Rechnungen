import { useQueryErrorResetBoundary } from '@tanstack/react-query'
import { Link, useCanGoBack, useRouter, type ErrorComponentProps } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { istApiFehler } from '@/api/client'
import biberSuche from '@/assets/fehler/biber-suche.png'
import biberReparatur from '@/assets/fehler/biber-reparatur.png'
import { Icon } from '@/components/Icon'
import { Logo } from '@/components/Logo'
import { Button } from '@/components/ui/button'

/** Wie ein Fehler dem Benutzer erklärt wird. */
export type Fehlerart = {
  icon: string
  /** Kurzer Code über dem Titel, z. B. „500“ oder „Offline“. */
  code: string
  titel: string
  text: string
  /** Nur ein vollständiges Neuladen hilft (neue Version nach einem Update). */
  neuLaden: boolean
}

const veralteteVersion =
  /dynamically imported module|Importing a module script failed|Failed to load module script|ChunkLoadError/i

/** Ordnet einen geworfenen Fehler einer verständlichen Meldung zu. */
export function fehlerart(fehler: unknown): Fehlerart {
  if (istApiFehler(fehler)) {
    if (fehler.status === 404)
      return {
        icon: 'search_off',
        code: '404',
        titel: 'Nicht gefunden',
        text: 'Den angefragten Eintrag gibt es nicht oder nicht mehr.',
        neuLaden: false,
      }
    if (fehler.status === 401 || fehler.status === 403)
      return {
        icon: 'lock',
        code: String(fehler.status),
        titel: 'Keine Berechtigung',
        text: 'Für diesen Bereich fehlen dir die nötigen Rechte.',
        neuLaden: false,
      }
    if (fehler.status >= 500)
      return {
        icon: 'cloud_off',
        code: String(fehler.status),
        titel: 'Fehler auf dem Server',
        text: 'Der Server konnte die Anfrage nicht verarbeiten. Versuch es gleich noch einmal.',
        neuLaden: false,
      }
    return {
      icon: 'error',
      code: String(fehler.status),
      titel: 'Anfrage fehlgeschlagen',
      text: fehler.titel,
      neuLaden: false,
    }
  }
  const nachricht = fehler instanceof Error ? fehler.message : String(fehler)
  if (veralteteVersion.test(nachricht))
    return {
      icon: 'system_update',
      code: 'Update',
      titel: 'Neue Version verfügbar',
      text: 'Biber-Rechnungen wurde aktualisiert. Lade die Seite neu, um die aktuelle Version zu verwenden.',
      neuLaden: true,
    }
  if (fehler instanceof TypeError && /fetch|network|load failed/i.test(nachricht))
    return {
      icon: 'wifi_off',
      code: 'Offline',
      titel: 'Server nicht erreichbar',
      text: 'Die Verbindung zum Server ist fehlgeschlagen. Prüfe die Verbindung und versuch es erneut.',
      neuLaden: false,
    }
  return {
    icon: 'error',
    code: 'Fehler',
    titel: 'Etwas ist schiefgelaufen',
    text: 'Beim Anzeigen dieser Seite ist ein unerwarteter Fehler aufgetreten.',
    neuLaden: false,
  }
}

/**
 * Gemeinsamer Rahmen der Fehlerseiten. In der Shell (`vollbild = false`) ersetzt er nur den Seiteninhalt;
 * als Vollbild (ohne Shell, z. B. wenn der Server beim Start nicht antwortet) bringt er Logo und Fläche mit.
 */
function FehlerRahmen({
  vollbild,
  code,
  titel,
  text,
  aktionen,
  details,
  alarm = false,
}: {
  vollbild: boolean
  icon: string
  code: string
  titel: string
  text: string
  aktionen: ReactNode
  details?: ReactNode
  alarm?: boolean
}) {
  const inhalt = (
    <div
      role={alarm ? 'alert' : undefined}
      className="mx-auto flex w-full max-w-[480px] flex-col items-center gap-4 py-10 text-center sm:py-16"
    >
      <div aria-hidden="true" className="w-64 max-w-full sm:w-80">
        <img src={code === '404' ? biberSuche : biberReparatur} alt="" className="aspect-[3/2] w-full object-contain" />
      </div>
      <div className="flex flex-col gap-2">
        <p className="m-0 font-mono text-xs font-medium tracking-wide text-gedaempft uppercase">{code}</p>
        <h1 className="m-0 text-[22px] font-semibold text-text">{titel}</h1>
        <p className="gedaempft m-0 text-sm">{text}</p>
      </div>
      <div className="flex flex-wrap justify-center gap-2">{aktionen}</div>
      {details}
    </div>
  )

  if (!vollbild) return <div className="seite">{inhalt}</div>

  return (
    <div className="flex min-h-screen flex-col bg-flaeche font-sans text-text">
      <header className="flex items-center gap-3 px-6 py-5">
        <Logo className="h-7 w-[47px] text-marke" />
        <span className="text-base font-semibold">Biber-Rechnungen</span>
      </header>
      <main className="flex flex-1 items-center px-4 pb-16">{inhalt}</main>
    </div>
  )
}

function ZumDashboard({ primaer }: { primaer: boolean }) {
  return (
    <Button asChild variant={primaer ? 'primaer' : 'standard'}>
      <Link to="/">
        <Icon name="home" className="text-[18px]" />
        Zum Dashboard
      </Link>
    </Button>
  )
}

/** 404 für unbekannte Adressen; in der Shell (`_app`) oder als Vollbild (Root). */
export function SeiteNichtGefunden({ vollbild = false }: { vollbild?: boolean }) {
  const router = useRouter()
  const kannZurueck = useCanGoBack()
  return (
    <FehlerRahmen
      vollbild={vollbild}
      icon="explore_off"
      code="404"
      titel="Seite nicht gefunden"
      text="Diese Adresse gibt es in Biber-Rechnungen nicht. Vielleicht ist der Link veraltet oder vertippt."
      aktionen={
        <>
          {kannZurueck && (
            <Button onClick={() => router.history.back()}>
              <Icon name="arrow_back" className="text-[18px]" />
              Zurück
            </Button>
          )}
          <ZumDashboard primaer />
        </>
      }
    />
  )
}

/** Fehlerseite für Ausnahmen beim Laden oder Rendern einer Route (`errorComponent`). */
export function Fehlerseite({ error, reset, vollbild = false }: ErrorComponentProps & { vollbild?: boolean }) {
  const router = useRouter()
  const abfragen = useQueryErrorResetBoundary()
  const art = fehlerart(error)

  function erneutVersuchen() {
    abfragen.reset()
    reset()
    void router.invalidate()
  }

  // In der Entwicklung der Stack (beginnt mit der Meldung), sonst nur die Meldung.
  const technisch = istApiFehler(error)
    ? `${error.status} – ${error.titel}`
    : error instanceof Error
      ? import.meta.env.DEV && error.stack
        ? error.stack
        : `${error.name}: ${error.message}`
      : String(error)

  return (
    <FehlerRahmen
      vollbild={vollbild}
      alarm
      icon={art.icon}
      code={art.code}
      titel={art.titel}
      text={art.text}
      aktionen={
        <>
          {art.neuLaden ? (
            <Button variant="primaer" onClick={() => window.location.reload()}>
              <Icon name="refresh" className="text-[18px]" />
              Seite neu laden
            </Button>
          ) : (
            <Button variant="primaer" onClick={erneutVersuchen}>
              <Icon name="refresh" className="text-[18px]" />
              Erneut versuchen
            </Button>
          )}
          {!vollbild && <ZumDashboard primaer={false} />}
        </>
      }
      details={
        <details className="w-full text-left text-xs text-gedaempft">
          <summary className="cursor-pointer text-center select-none">Technische Details</summary>
          <pre className="mt-2 max-h-48 overflow-auto rounded-md border border-rand bg-karte p-3 font-mono whitespace-pre-wrap">
            {technisch}
          </pre>
        </details>
      }
    />
  )
}
