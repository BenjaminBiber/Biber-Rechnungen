import { createFileRoute, Link, Outlet, redirect, useRouterState } from '@tanstack/react-router'
import { useEffect } from 'react'
import { authKeys } from '@/api/auth'
import { api, daten } from '@/api/client'
import { Icon } from '@/components/Icon'
import { Befehlspalette } from '@/components/shell/Befehlspalette'
import { useBefehlspalette } from '@/components/shell/befehlspalette-store'
import { Benutzermenue } from '@/components/shell/Benutzermenue'
import { BottomNavigation } from '@/components/shell/BottomNavigation'
import { Kopfleiste } from '@/components/shell/Kopfleiste'
import { seitentitel } from '@/components/shell/navigation'
import { Seitenleiste } from '@/components/shell/Seitenleiste'
import { ToastBereich } from '@/components/toast'
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
})

/** Mobile Kopfzeile: Logo „B“, Seitentitel, Suche, Avatar. */
function MobilKopfzeile() {
  const pfad = useRouterState({ select: (s) => s.location.pathname })
  const { oeffnen } = useBefehlspalette()
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-rand bg-karte px-4 text-text">
      <Link
        to="/"
        aria-label="Dashboard"
        className="flex h-8 w-8 flex-none items-center justify-center rounded-lg bg-akzent text-[15px] font-bold text-auf-akzent no-underline"
      >
        B
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
    </header>
  )
}

function AppLayout() {
  const istMobil = useIstMobil()
  const { oeffnen } = useBefehlspalette()

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
    <div className="flex min-h-screen bg-flaeche">
      {!istMobil && <Seitenleiste />}
      <div className="flex min-w-0 flex-1 flex-col">
        {istMobil ? <MobilKopfzeile /> : <Kopfleiste />}
        <div className={istMobil ? 'flex-1 pb-[84px]' : 'flex-1'}>
          <Outlet />
        </div>
      </div>
      {istMobil && <BottomNavigation />}
      <Befehlspalette />
      <ToastBereich />
    </div>
  )
}
