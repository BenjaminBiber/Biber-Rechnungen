import { QueryClient } from '@tanstack/react-query'
import { createRouter } from '@tanstack/react-router'
import { Fehlerseite } from '@/components/fehler/Fehlerseiten'
import { routeTree } from './routeTree.gen'

export type RouterKontext = { queryClient: QueryClient }

export function erzeugeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      // Zurück im Tab bzw. wieder online: veraltete Daten neu laden (Hintergrund-Jobs ändern Belege auch ohne
      // Zutun dieser Seite). Offene Formulare bleiben unberührt – sie halten Änderungen in einem eigenen Entwurf.
      queries: { staleTime: 30_000, refetchOnWindowFocus: true, refetchOnReconnect: true },
    },
  })
}

export function erzeugeRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree,
    context: { queryClient },
    defaultPreload: 'intent',
    // Daten kommen über TanStack Query; der Router soll nicht zusätzlich cachen.
    defaultPreloadStaleTime: 0,
    scrollRestoration: true,
    // Fehler einer Seite ersetzen nur deren Inhalt; Shell und Navigation bleiben bedienbar.
    defaultErrorComponent: Fehlerseite,
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof erzeugeRouter>
  }
}
