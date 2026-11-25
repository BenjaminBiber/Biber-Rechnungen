import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ApiFehler } from '@/api/client'
import { db } from '@/mocks/db'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { Fehlerseite, fehlerart } from './Fehlerseiten'

/** Rendert die echte Routen-Struktur ab `pfad`, mit derselben Standard-Fehlerseite wie die App. */
function renderApp(pfad: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [pfad] }),
    defaultErrorComponent: Fehlerseite,
  })
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

const T = { timeout: 5000 }

describe('fehlerart', () => {
  it('ordnet API-Fehler nach Status ein', () => {
    expect(fehlerart(new ApiFehler(404, 'x')).titel).toBe('Nicht gefunden')
    expect(fehlerart(new ApiFehler(403, 'x')).titel).toBe('Keine Berechtigung')
    expect(fehlerart(new ApiFehler(503, 'x'))).toMatchObject({ code: '503', titel: 'Fehler auf dem Server' })
    expect(fehlerart(new ApiFehler(409, 'Beleg ist gesperrt')).text).toBe('Beleg ist gesperrt')
  })

  it('erkennt fehlende Verbindung und veraltete Versionen', () => {
    expect(fehlerart(new TypeError('Failed to fetch')).titel).toBe('Server nicht erreichbar')
    expect(fehlerart(new TypeError('Failed to fetch dynamically imported module: /assets/x.js'))).toMatchObject({
      titel: 'Neue Version verfügbar',
      neuLaden: true,
    })
  })

  it('fällt sonst auf eine allgemeine Meldung zurück', () => {
    expect(fehlerart(new Error('kaputt')).titel).toBe('Etwas ist schiefgelaufen')
    expect(fehlerart('nur ein Text').titel).toBe('Etwas ist schiefgelaufen')
  })
})

describe('Fehlerseiten im Router', () => {
  it('zeigt 404 für unbekannte Adressen innerhalb der Shell', async () => {
    renderApp('/gibt-es-nicht')
    expect(await screen.findByRole('heading', { level: 1, name: 'Seite nicht gefunden' }, T)).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Hauptnavigation' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zum Dashboard' })).toHaveAttribute('href', '/')
  })

  it('zeigt 404 auch für tiefe unbekannte Adressen', async () => {
    renderApp('/einstellungen/gibt-es-nicht/weiter')
    expect(await screen.findByRole('heading', { level: 1, name: 'Seite nicht gefunden' }, T)).toBeInTheDocument()
  })

  it('leitet abgemeldet auch bei unbekannter Adresse zur Anmeldung', async () => {
    szenario.abgemeldet()
    const router = renderApp('/gibt-es-nicht')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
  })

  it('zeigt Serverfehler beim Start im Vollbild und erholt sich mit „Erneut versuchen“', async () => {
    szenario.fehler('/api/auth/status', 500)
    renderApp('/export')
    expect(await screen.findByRole('heading', { level: 1, name: 'Fehler auf dem Server' }, T)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('500')
    expect(screen.queryByRole('navigation', { name: 'Hauptnavigation' })).not.toBeInTheDocument()

    db.fehler.length = 0
    await userEvent.click(screen.getByRole('button', { name: 'Erneut versuchen' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Export' }, T)).toBeInTheDocument()
  })
})
