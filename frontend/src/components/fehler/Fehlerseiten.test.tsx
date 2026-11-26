import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiFehler } from '@/api/client'
import { db } from '@/mocks/db'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { Fehlerseite, fehlerart, WIEDERHOLEN_NACH } from './Fehlerseiten'

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
    expect(fehlerart(new ApiFehler(404, 'x'))).toMatchObject({ titel: 'Diese Seite gibt es nicht', aktion: 'weg' })
    expect(fehlerart(new ApiFehler(403, 'x'))).toMatchObject({ titel: 'Dafür fehlen dir die Rechte', aktion: 'weg' })
    expect(fehlerart(new ApiFehler(502, 'x'))).toMatchObject({
      code: '502',
      grund: 'Bad Gateway',
      titel: 'Der Server antwortet gerade nicht',
      ton: 'orange',
      aktion: 'automatisch',
    })
    expect(fehlerart(new ApiFehler(503, 'x'))).toMatchObject({ code: '503', titel: 'Kurz in Wartung' })
    expect(fehlerart(new ApiFehler(500, 'x'))).toMatchObject({ titel: 'Da ist etwas schiefgelaufen', ton: 'rot' })
    expect(fehlerart(new ApiFehler(409, 'Beleg ist gesperrt'))).toMatchObject({
      text: 'Beleg ist gesperrt',
      aktion: 'erneut',
    })
  })

  it('erkennt fehlende Verbindung und veraltete Versionen', () => {
    expect(fehlerart(new TypeError('Failed to fetch'))).toMatchObject({ code: 'Offline', aktion: 'automatisch' })
    expect(fehlerart(new ApiFehler(0, 'Netzwerkfehler beim Hochladen')).code).toBe('Offline')
    expect(fehlerart(new TypeError('Failed to fetch dynamically imported module: /assets/x.js'))).toMatchObject({
      titel: 'Neue Version verfügbar',
      aktion: 'neuLaden',
    })
  })

  it('fällt sonst auf eine allgemeine Meldung zurück', () => {
    expect(fehlerart(new Error('kaputt'))).toMatchObject({ titel: 'Da ist etwas schiefgelaufen', aktion: 'erneut' })
    expect(fehlerart('nur ein Text').titel).toBe('Da ist etwas schiefgelaufen')
  })
})

describe('Fehlerseiten im Router', () => {
  it('zeigt 404 für unbekannte Adressen innerhalb der Shell', async () => {
    renderApp('/gibt-es-nicht')
    expect(await screen.findByRole('heading', { level: 1, name: 'Diese Seite gibt es nicht' }, T)).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Hauptnavigation' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zum Dashboard' })).toHaveAttribute('href', '/')
  })

  it('zeigt 404 auch für tiefe unbekannte Adressen', async () => {
    renderApp('/einstellungen/gibt-es-nicht/weiter')
    expect(await screen.findByRole('heading', { level: 1, name: 'Diese Seite gibt es nicht' }, T)).toBeInTheDocument()
  })

  it('leitet abgemeldet auch bei unbekannter Adresse zur Anmeldung', async () => {
    szenario.abgemeldet()
    const router = renderApp('/gibt-es-nicht')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
  })

  it('zeigt Serverfehler beim Start im Vollbild und erholt sich mit „Jetzt erneut versuchen“', async () => {
    szenario.fehler('/api/auth/status', 500)
    renderApp('/export')
    expect(await screen.findByRole('heading', { level: 1, name: 'Da ist etwas schiefgelaufen' }, T)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('interner Fehler')
    expect(screen.getByRole('timer')).toHaveTextContent('Neuer Versuch automatisch in 15 s')
    expect(screen.queryByRole('navigation', { name: 'Hauptnavigation' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Technische Details' }))
    expect(screen.getByText('500 Internal Server Error')).toBeInTheDocument()
    expect(screen.getByText('GET /api/auth/status')).toBeInTheDocument()

    db.fehler.length = 0
    await userEvent.click(screen.getByRole('button', { name: 'Jetzt erneut versuchen' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Export' }, T)).toBeInTheDocument()
  })

  it('versucht es nach Ablauf des Countdowns von selbst erneut', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      szenario.fehler('/api/auth/status', 502)
      renderApp('/export')
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Der Server antwortet gerade nicht' }, T),
      ).toBeInTheDocument()

      db.fehler.length = 0
      await act(() => vi.advanceTimersByTimeAsync(WIEDERHOLEN_NACH * 1000))
      expect(await screen.findByRole('heading', { level: 1, name: 'Export' }, T)).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })
})
