import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte Routen-Struktur ab `pfad`. */
function renderApp(pfad: string) {
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
  return router
}

describe('Layout _app', () => {
  it('zeigt angemeldet Seitenleiste, Kopfleiste und Seite', async () => {
    renderApp('/export')
    expect(await screen.findByRole('heading', { level: 1, name: 'Export' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Hauptnavigation' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Beleg hochladen' })).toHaveAttribute('href', '/hochladen')
  })

  it('leitet abgemeldete Benutzer nach /anmelden', async () => {
    szenario.abgemeldet()
    const router = renderApp('/belege')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
    expect(await screen.findByRole('heading', { level: 1, name: 'Anmelden' })).toBeInTheDocument()
  })

  it('leitet ohne Einrichtung nach /anmelden', async () => {
    szenario.nichtEingerichtet()
    const router = renderApp('/')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
  })

  it('blendet „Beleg hochladen“ für NurLesen aus', async () => {
    szenario.rolle('NurLesen')
    renderApp('/export')
    expect(await screen.findByRole('heading', { level: 1, name: 'Export' })).toBeInTheDocument()
    await screen.findByRole('button', { name: 'Benutzermenü' })
    await screen.findByText('M. Keller')
    expect(screen.queryByRole('link', { name: 'Beleg hochladen' })).not.toBeInTheDocument()
  })
})
