import { createFileRoute, Link, Outlet, redirect, useRouterState } from '@tanstack/react-router'
import { Suspense, useEffect, useState } from 'react'
import { authKeys } from '@/api/auth'
import { api, daten } from '@/api/client'
import { Fehlerseite, SeiteNichtGefunden } from '@/components/fehler/Fehlerseiten'
import { Icon } from '@/components/Icon'
import { Logo } from '@/components/Logo'
import { Befehlspalette } from '@/components/shell/Befehlspalette'
import { useBefehlspalette } from '@/components/shell/befehlspalette-store'
import { Benutzermenue } from '@/components/shell/Benutzermenue'
import { BottomNavigation } from '@/components/shell/BottomNavigation'
import { Kopfleiste } from '@/components/shell/Kopfleiste'
import { Ladebalken } from '@/components/shell/Ladebalken'
import { seitentitel } from '@/components/shell/navigation'
import { Seitenleiste } from '@/components/shell/Seitenleiste'
import { SeitenpanelZiel } from '@/components/shell/Seitenpanel'
import { ToastBereich } from '@/components/toast'
import { SkelettSeite } from '@/components/ui/skelett'
import { EinstellungenNavigation } from '@/features/einstellungen/EinstellungenNavigation'
import { useIstMobil } from '@/lib/useIstMobil'

/** Rahmen der angemeldeten Anwendung; ohne Einrichtung oder Anmeldung geht es nach `/anmelden`. */
export const Route = createFileRoute('/_app')({
  beforeLoad: async ({ context }) => {
    // Gleicher Schlüssel wie `useAuthStatus`, damit die Shell den Status aus dem Cache liest.
    const status = await context.queryClient.fetchQuery({
      queryKey: authKeys.status(),
      queryFn: ({ signal }) => daten(api.GET('/api/auth/status', { signal })),
    })
    if (!status.eingerichtet || !status.angemeldet) throw redirect({ to: '/anmelden' })
  },
  component: AppLayout,
  // Unbekannte Adressen unterhalb der Shell: 404 mit Seitenleiste statt Vollbild.
  notFoundComponent: () => <SeiteNichtGefunden />,
  // Scheitert schon die Statusabfrage oben, gibt es keine Shell – Fehler im Vollbild.
  errorComponent: (props) => <Fehlerseite {...props} vollbild />,
})

/** Mobile Kopfzeile (MobilDashboard.dc.html): Biber-Logo, Seitentitel, Suche, Avatar. */
function MobilKopfzeile() {
  const pfad = useRouterState({ select: (s) => s.location.pathname })
  const { oeffnen } = useBefehlspalette()
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-rand bg-karte px-4 text-text">
      <Link
        to="/"
        aria-label="Dashboard"
        className="flex h-11 flex-none items-center text-marke no-underline hover:text-marke"
      >
        <Logo className="h-7 w-[47px]" />
      </Link>
      <span className="min-w-0 flex-1 truncate text-base font-semibold">{seitentitel(pfad)}</span>
      <button
        type="button"
        onClick={oeffnen}
        aria-label="Volltextsuche"
        className="flex h-10 w-10 cursor-pointer items-center justify-center rounded-full border-0 bg-transparent text-text"
      >
        <Icon name="search" className="text-[24px]" />
      </button>
      <Benutzermenue kompakt />
      <Ladebalken />
    </header>
  )
}

/**
 * Seiteninhalt mit eigener Suspense-Grenze: Suspendiert etwas darin (z. B. react-pdf beim Laden),
 * blendet React nur den Inhalt aus – sonst griffe die Grenze der Route und Navigation samt Kopfleiste flackerten weg.
 */
function Inhalt() {
  return (
    <Suspense fallback={<SkelettSeite />}>
      <Outlet />
    </Suspense>
  )
}

function AppLayout() {
  const istMobil = useIstMobil()
  const { oeffnen } = useBefehlspalette()
  const pfad = useRouterState({ select: (s) => s.location.pathname })
  const inEinstellungen = pfad === '/einstellungen' || pfad.startsWith('/einstellungen/')
  const [panelZiel, setPanelZiel] = useState<HTMLDivElement | null>(null)

  useEffect(() => {
    const beiTaste = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        oeffnen()
      }
    }
    window.addEventListener('keydown', beiTaste)
    return () => window.removeEventListener('keydown', beiTaste)
  }, [oeffnen])

  return (
    <SeitenpanelZiel value={panelZiel}>
      <div className="flex min-h-screen bg-flaeche">
        {!istMobil && <Seitenleiste />}
        {/* Einstellungen.dc.html: Bereichsnavigation als eigene Spalte neben der Seitenleiste, über die volle Höhe */}
        {!istMobil && inEinstellungen && <EinstellungenNavigation />}
        <div className="flex min-w-0 flex-1 flex-col">
          {istMobil ? <MobilKopfzeile /> : <Kopfleiste />}
          {istMobil ? (
            <div className="flex-1 pb-[84px]">
              <Inhalt />
            </div>
          ) : (
            <div className="flex flex-1">
              <div className="min-w-0 flex-1">
                <Inhalt />
              </div>
              {/* Seitenpanels (z. B. Belegvorschau) fahren hier unter der Kopfleiste von rechts ein; der Inhalt rückt zur Seite. */}
              <div ref={setPanelZiel} className="flex flex-none" />
            </div>
          )}
        </div>
        {istMobil && <BottomNavigation />}
        <Befehlspalette />
        <ToastBereich />
      </div>
    </SeitenpanelZiel>
  )
}
