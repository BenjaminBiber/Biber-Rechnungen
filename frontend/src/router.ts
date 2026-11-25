import { QueryClient } from '@tanstack/react-query'
import { createRouter } from '@tanstack/react-router'
import { Fehlerseite } from '@/components/fehler/Fehlerseiten'
import { routeTree } from './routeTree.gen'

export type RouterKontext = { queryClient: QueryClient }

export function erzeugeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, refetchOnWindowFocus: false },
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
