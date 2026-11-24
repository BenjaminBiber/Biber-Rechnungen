import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useEffect, type ReactNode } from 'react'
import { onTestFinished } from 'vitest'
import { alleEntfernen } from '@/components/toast/toast-store'
import { belegIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { routeTree } from '@/routeTree.gen'

/** Seitenzahl, die das gemockte `react-pdf` beim Laden meldet. */
export const pdfMock = { seiten: 1 }

/**
 * Ersatz für `react-pdf` (jsdom hat kein Canvas): `vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))`.
 * Die Seite zeigt ihre Eigenschaften als data-Attribute.
 */
export const reactPdfMock = {
  pdfjs: { GlobalWorkerOptions: {} },
  Document: function Document({
    children,
    onLoadSuccess,
  }: {
    children: ReactNode
    onLoadSuccess?: (pdf: { numPages: number }) => void
  }) {
    useEffect(() => onLoadSuccess?.({ numPages: pdfMock.seiten }), [onLoadSuccess])
    return <div data-testid="pdf-dokument">{children}</div>
  },
  Page: function Page({ pageNumber, width, rotate }: { pageNumber: number; width: number; rotate: number }) {
    return <canvas data-testid="pdf-seite" data-seite={pageNumber} data-breite={width} data-drehung={rotate} />
  },
}

export const b2 = belegIds.b2

/** Rendert die echte App ab `pfad` (Standard: Beleg b2 „Bürowelt Krämer KG“). */
export function renderBelegDetail(pfad = `/belege/${b2}`) {
  alleEntfernen()
  const nutzer = userEvent.setup()
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [pfad] }),
  })
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { nutzer, router, queryClient }
}

/** Wartet, bis der Kopf des Belegs steht. */
export function kopfGeladen(name = 'Bürowelt Krämer KG') {
  return screen.findByRole('heading', { level: 1, name }, { timeout: 5000 })
}

type Anfrage = { methode: string; pfad: string; body: unknown }

/** Schneidet alle ändernden Anfragen samt JSON-Körper mit (bis Testende). */
export function anfragenMitschneiden() {
  const offen: Promise<Anfrage>[] = []
  const zuhoeren = ({ request }: { request: Request }) => {
    if (request.method === 'GET') return
    offen.push(
      request
        .clone()
        .text()
        .then((text) => ({
          methode: request.method,
          pfad: new URL(request.url).pathname,
          body: text ? (JSON.parse(text) as unknown) : undefined,
        })),
    )
  }
  server.events.on('request:start', zuhoeren)
  onTestFinished(() => server.events.removeListener('request:start', zuhoeren))
  return { anfragen: () => Promise.all(offen) }
}
