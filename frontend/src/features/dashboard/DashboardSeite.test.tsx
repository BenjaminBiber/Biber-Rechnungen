import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte App ab `pfad` und merkt sich die Query-Parameter aller Dashboard-Anfragen. */
function renderDashboard(pfad = '/?jahr=2026') {
  const anfragen: URLSearchParams[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (url.pathname === '/api/dashboard') anfragen.push(url.searchParams)
  }
  server.events.on('request:start', mitschnitt)
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
  return {
    router,
    anfragen,
    ende: () => server.events.removeListener('request:start', mitschnitt),
  }
}

const karte = (name: RegExp | string) => screen.findByRole('link', { name }, { timeout: 5000 })

describe('Dashboard', () => {
  it('zeigt die Kennzahlen der Fixtures mit den richtigen Linkzielen', async () => {
    const { ende } = renderDashboard('/?jahr=2026&zeitraum=Jahr')
    const kosten = await karte(/^Kosten (?!2026)/)
    expect(kosten).toHaveTextContent('1.704,00 €')
    expect(kosten).toHaveTextContent('+5,8 % ggü. Nov 2025')
    expect(kosten.getAttribute('href')).toMatch(/^\/belege\?.*von=2026-/)

    const jahr = await karte(/^Kosten 2026/)
    expect(jahr).toHaveTextContent('20.845,00 €')
    expect(jahr).toHaveAttribute('href', '/export')

    const erwartet: Array<[RegExp, string, string]> = [
      [/^Offen\d/, '9', 'Offen'],
      [/^Überfällig/, '2', 'Ueberfaellig'],
      [/^Inbox/, '12', 'Inbox'],
      [/^Zu prüfen\d/, '5', 'ZuPruefen'],
    ]
    for (const [name, wert, ansicht] of erwartet) {
      const link = await karte(name)
      expect(link).toHaveTextContent(wert)
      expect(link).toHaveAttribute('href', `/belege?ansicht=${ansicht}`)
    }
    ende()
  })

  it('lädt beim Umschalten des Zeitraums neu', async () => {
    const { router, anfragen, ende } = renderDashboard()
    await karte(/^Kosten 2026/)
    await waitFor(() => expect(anfragen.at(-1)?.get('zeitraum')).toBe('Jahr'))
    expect(anfragen.at(-1)?.get('jahr')).toBe('2026')

    await userEvent.click(screen.getByRole('button', { name: 'Monat' }))
    await waitFor(() => expect(anfragen.at(-1)?.get('zeitraum')).toBe('Monat'))
    expect(router.state.location.search).toMatchObject({ zeitraum: 'Monat', jahr: 2026 })
    expect(screen.getByRole('button', { name: 'Monat' })).toHaveAttribute('aria-pressed', 'true')

    await userEvent.selectOptions(screen.getByLabelText('Jahr'), String(new Date().getFullYear() - 1))
    await waitFor(() => expect(anfragen.at(-1)?.get('jahr')).toBe(String(new Date().getFullYear() - 1)))
    ende()
  })

  it('zeigt Listen, Top-Korrespondenten und Systemstatus', async () => {
    renderDashboard()
    expect(await screen.findByRole('heading', { name: 'Top-Korrespondenten 2026' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Autohaus Brenner GmbH/ })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/korrespondenten\/.+/),
    )
    expect(screen.getByRole('link', { name: 'Alle' })).toHaveAttribute('href', '/korrespondenten')
    expect(screen.getByRole('link', { name: /Druckerei Weller.*Leistungsdatum fehlt/ })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/belege\/.+/),
    )
    expect(screen.getByRole('link', { name: 'Liste öffnen' })).toHaveAttribute('href', '/belege?ansicht=ZuPruefen')
    expect(screen.getByText('überfällig seit 28.10.2026')).toBeInTheDocument()
    expect(screen.getByText('fällig 26.11.2026')).toBeInTheDocument()
    expect(await screen.findByText('Integritätsprüfung ohne Befund')).toBeInTheDocument()
    expect(screen.getByText('Größte Preisänderungen')).toBeInTheDocument()
    expect(screen.getByText('-12,5 %')).toBeInTheDocument()
  })

  it('enthält eine Screenreader-Tabelle mit 12 Monaten', async () => {
    renderDashboard()
    const tabelle = await screen.findByRole('table', { name: 'Gesamtkosten pro Monat 2026' })
    expect(within(tabelle).getAllByRole('row')).toHaveLength(13)
    expect(within(tabelle).getByRole('rowheader', { name: 'Nov' })).toBeInTheDocument()
  })

  it('blendet die Vorsteuer-Karte bei vorsteuer: null aus und zeigt sie sonst', async () => {
    renderDashboard()
    await karte(/^Kosten 2026/)
    expect(screen.queryByRole('heading', { name: /^Vorsteuer/ })).not.toBeInTheDocument()
  })

  it('zeigt die Vorsteuer-Karte, wenn Vorsteuer vorhanden ist', async () => {
    db.dashboard = {
      ...db.dashboard,
      vorsteuer: { zeitraum: 'Quartalsweise', zeilen: [{ bezeichnung: 'Q4', betrag: 812.5 }], summe: 812.5 },
    }
    renderDashboard()
    expect(await screen.findByRole('heading', { name: 'Vorsteuer 2026' })).toBeInTheDocument()
    expect(screen.getAllByText('812,50 €')).toHaveLength(2)
  })

  it('färbt den DI-Verbrauch ab der Warnschwelle orange', async () => {
    renderDashboard()
    expect(await screen.findByText('412 / 500 Seiten · 82 %')).toBeInTheDocument()
    expect(screen.getByTestId('di-balken')).toHaveAttribute('data-warnung', 'true')
    expect(screen.getByText('Warnschwelle erreicht')).toBeInTheDocument()
  })

  it('zeigt unter der Warnschwelle keinen Hinweis, bei Pause „Warteschlange pausiert“', async () => {
    db.diVerbrauch = { ...db.diVerbrauch, seiten: 200, prozent: 40, pausiert: true }
    renderDashboard()
    expect(await screen.findByText('200 / 500 Seiten · 40 %')).toBeInTheDocument()
    expect(screen.getByTestId('di-balken')).toHaveAttribute('data-warnung', 'false')
    expect(screen.getByText('Warteschlange pausiert')).toBeInTheDocument()
  })

  it('zeigt den Fehlerzustand der Systemkarte', async () => {
    szenario.fehler('/api/system/status', 500)
    renderDashboard()
    expect(await screen.findByText('Systemstatus konnte nicht geladen werden.')).toBeInTheDocument()
  })

  it('zeigt bei Fehler „Erneut versuchen“ und lädt nach', async () => {
    szenario.fehler('/api/dashboard', 500)
    renderDashboard()
    const knopf = await screen.findByRole('button', { name: /Erneut versuchen/ })
    db.fehler = []
    await userEvent.click(knopf)
    expect(await karte(/^Kosten 2026/)).toBeInTheDocument()
  })

  it('zeigt beim Laden Skelette', async () => {
    renderDashboard()
    expect(await screen.findByLabelText('Dashboard wird geladen')).toBeInTheDocument()
  })

  it('zeigt bei leerem Bestand keine Hochladen-Kachel', async () => {
    szenario.leer()
    renderDashboard()
    expect(await screen.findByRole('heading', { name: 'Gesamtkosten pro Monat' })).toBeInTheDocument()
    expect(screen.queryByText('Noch keine Belege – lade den ersten hoch')).not.toBeInTheDocument()
    // Nur die Navigation führt noch zum Hochladen, das Dashboard selbst nicht.
    const inhalt = screen.getByRole('heading', { level: 1, name: 'Dashboard' }).closest('main')!
    expect(within(inhalt).queryByRole('link', { name: 'Hochladen' })).not.toBeInTheDocument()
  })
})
