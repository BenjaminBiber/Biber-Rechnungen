import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  type AnyRouter,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { render, type RenderResult } from '@testing-library/react'
import type { ReactNode } from 'react'

export type RenderMitAppErgebnis = RenderResult & {
  queryClient: QueryClient
  router: AnyRouter
}

/**
 * Rendert `ui` innerhalb von QueryClient und einem Memory-Router.
 * `route` ist die Start-URL (Standard „/“); `ui` erscheint auf jeder URL.
 * Da der Router asynchron lädt, Elemente mit `findBy…` abfragen.
 */
export function renderMitApp(ui: ReactNode, { route = '/' }: { route?: string } = {}): RenderMitAppErgebnis {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { retry: false },
    },
  })

  const wurzel = createRootRoute({ component: () => ui })
  const routeTree = wurzel.addChildren([
    createRoute({ getParentRoute: () => wurzel, path: '/' }),
    createRoute({ getParentRoute: () => wurzel, path: '$' }),
  ])
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [route] }),
  })

  const ergebnis = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { ...ergebnis, queryClient, router }
}
