import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reactPdfMock } from '@/features/belege/detail/testHilfe'
import { db, zuruecksetzen } from '@/mocks/db'
import { belegIds, tagIds } from '@/mocks/fixtures/ids'
import { tagKurz } from '@/mocks/fixtures/tags'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

vi.mock('react-pdf', () => reactPdfMock)

function breite(mobil: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: mobil,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }))
}

function renderApp(pfad: string) {
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
  return { nutzer, router }
}

const warte = { timeout: 5000 }

beforeEach(() => breite(true))
afterEach(() => {
  vi.unstubAllGlobals()
  zuruecksetzen()
})

describe('Mobile Übersicht', () => {
  it('zeigt Kacheln, Kosten pro Monat und drei Einträge „Zu prüfen“', async () => {
    renderApp('/?jahr=2026')
    const inbox = await screen.findByRole('link', { name: /Inbox\s*12/ }, warte)
    expect(inbox).toHaveAttribute('href', '/belege?ansicht=Inbox')
    expect(screen.getByRole('link', { name: /^Zu prüfen\s*5$/ })).toHaveAttribute('href', '/belege?ansicht=ZuPruefen')
    expect(screen.getByText('2.418,40 €')).toBeInTheDocument()
    expect(screen.getByText('9 Belege')).toBeInTheDocument()
    expect(screen.getByText('412,30 €')).toBeInTheDocument()
    expect(screen.getByText('20.845,00 €')).toBeInTheDocument()
    expect(screen.getByText(/\+9,7 % ggü\. Jan–Nov 2025/)).toBeInTheDocument()
    const pruefen = screen.getByRole('region', { name: 'Zu prüfen' })
    expect(within(pruefen).getAllByRole('link', { name: /€/ })).toHaveLength(3)
    expect(within(pruefen).getByRole('link', { name: /Bürowelt Krämer KG.*264,00\s€/ })).toBeInTheDocument()
    expect(within(pruefen).getByRole('link', { name: 'Alle zu prüfenden Belege' })).toBeInTheDocument()
  })

  it('rendert am Desktop weiterhin das Dashboard', async () => {
    breite(false)
    renderApp('/?jahr=2026')
    expect(await screen.findByRole('heading', { name: 'Dashboard' }, warte)).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Zeitraum' })).toBeInTheDocument()
  })
})

async function zeilen() {
  const liste = await screen.findByRole('list', { name: 'Belege' }, warte)
  await waitFor(() => expect(within(liste).queryAllByRole('listitem').length).toBeGreaterThan(0), warte)
  return within(liste).getAllByRole('listitem')
}

describe('Mobile Belegliste', () => {
  it('zeigt alle Belege der Fixtures als Zeilen mit „+n“-Chip ab drei Tags', async () => {
    const b2 = db.belege.find((b) => b.id === belegIds.b2)!
    const inbox = db.tags.find((t) => t.id === tagIds.inbox)!
    const reise = db.tags.find((t) => t.name === 'Reisekosten')!
    b2.tags = [tagKurz(inbox), ...b2.tags.filter((t) => t.id !== inbox.id), tagKurz(reise)]
    renderApp('/belege')
    const alle = await zeilen()
    expect(alle).toHaveLength(db.belege.length)
    const zeile = alle.find((z) => within(z).queryByText('Bürowelt Krämer KG') && within(z).queryByText('264,00 €'))!
    expect(within(zeile).getByText('+1')).toHaveAttribute('title', 'Reisekosten')
    expect(within(zeile).getByLabelText('1 weitere: Reisekosten')).toBeInTheDocument()
    expect(within(zeile).getByText('19.11.2026 · Bürobedarf')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Filter' })).toHaveAttribute('aria-haspopup', 'dialog')
  })

  it('Filter-Sheet übernimmt die Auswahl erst mit dem Button, Schließen verwirft sie', async () => {
    const { nutzer, router } = renderApp('/belege')
    await zeilen()
    await nutzer.click(screen.getByRole('button', { name: 'Filter' }))
    const sheet = await screen.findByRole('dialog', { name: 'Filtern' })
    expect(within(sheet).getByRole('radio', { name: /Alle Belege/ })).toHaveAttribute('aria-checked', 'true')
    expect(await within(sheet).findByRole('button', { name: '12 Belege anzeigen' }, warte)).toBeInTheDocument()

    await nutzer.click(within(sheet).getByRole('radio', { name: /Zu prüfen/ }))
    expect(await within(sheet).findByRole('button', { name: '5 Belege anzeigen' }, warte)).toBeInTheDocument()
    expect(router.state.location.search).toEqual({})

    await nutzer.click(within(sheet).getByRole('button', { name: 'Zurücksetzen' }))
    expect(within(sheet).getByRole('radio', { name: /Alle Belege/ })).toHaveAttribute('aria-checked', 'true')

    await nutzer.click(within(sheet).getByRole('radio', { name: /Offen/ }))
    await nutzer.click(within(sheet).getByRole('button', { name: 'Schließen' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(router.state.location.search).toEqual({})
    expect(screen.getByRole('button', { name: 'Filter' })).toBeInTheDocument()

    await nutzer.click(screen.getByRole('button', { name: 'Filter' }))
    const neu = await screen.findByRole('dialog', { name: 'Filtern' })
    expect(within(neu).getByRole('radio', { name: /Alle Belege/ })).toHaveAttribute('aria-checked', 'true')
    await nutzer.click(within(neu).getByRole('radio', { name: /Zu prüfen/ }))
    await nutzer.click(await within(neu).findByRole('button', { name: '5 Belege anzeigen' }, warte))
    await waitFor(() => expect(router.state.location.search).toEqual({ ansicht: 'ZuPruefen' }))
    expect(screen.getByRole('button', { name: 'Filter, 1 aktiv' })).toBeInTheDocument()
    await waitFor(async () => expect(await zeilen()).toHaveLength(5), warte)
  })

  it('Esc schließt das Sheet', async () => {
    const { nutzer } = renderApp('/belege')
    await zeilen()
    await nutzer.click(screen.getByRole('button', { name: 'Filter' }))
    await screen.findByRole('dialog', { name: 'Filtern' })
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('sortiert nach Betrag, höchster zuerst', async () => {
    const { nutzer, router } = renderApp('/belege')
    await zeilen()
    await nutzer.click(screen.getByRole('button', { name: 'Filter' }))
    const sheet = await screen.findByRole('dialog', { name: 'Filtern' })
    await nutzer.click(within(sheet).getByRole('radio', { name: 'Betrag, höchster zuerst' }))
    await nutzer.click(await within(sheet).findByRole('button', { name: '12 Belege anzeigen' }, warte))
    await waitFor(() => expect(router.state.location.search).toEqual({ sortierung: 'Brutto' }))
    await waitFor(async () => expect((await zeilen())[0]).toHaveTextContent('534,00 €'), warte)
    expect(screen.getByRole('button', { name: 'Filter, 1 aktiv' })).toBeInTheDocument()
  })

  it('rendert am Desktop weiterhin die Tabelle', async () => {
    breite(false)
    renderApp('/belege')
    expect(await screen.findByRole('table', {}, warte)).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Belege' })).not.toBeInTheDocument()
  })
})

describe('Mobile Beleg-Ansicht', () => {
  it('zeigt Betrag, Chips, Datenkarte und für „zu prüfen“ Bearbeiten und Bestätigen', async () => {
    const { nutzer } = renderApp(`/belege/${belegIds.b2}`)
    expect(await screen.findByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' }, warte)).toBeInTheDocument()
    expect(screen.getByText('Beleg')).toBeInTheDocument()
    expect(screen.getByLabelText('Betrag')).toHaveTextContent('264,00 €')
    expect(screen.getByText('Inbox')).toBeInTheDocument()
    expect(screen.getByText('zu prüfen')).toBeInTheDocument()
    const daten = screen.getByRole('region', { name: 'Eckdaten' })
    expect(within(daten).getByText('19.11.2026')).toBeInTheDocument()
    expect(within(daten).getByText('Netto')).toBeInTheDocument()
    expect(within(daten).getByText(/^USt 19 %$/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Original öffnen' })).toHaveAttribute('target', '_blank')
    expect(screen.getByRole('button', { name: 'Weitere Aktionen' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Bearbeiten/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Bestätigen/ })).toBeInTheDocument()

    const details = screen.getByRole('button', { name: /Positionen, Prüfung, Historie und Dateien/ })
    expect(details).toHaveAttribute('aria-expanded', 'false')
    await nutzer.click(details)
    expect(details).toHaveAttribute('aria-expanded', 'true')

    await nutzer.click(screen.getByRole('button', { name: /Bearbeiten/ }))
    expect(screen.getByRole('region', { name: 'Beleg bearbeiten' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Bestätigen/ })).not.toBeInTheDocument()
  })

  it('ohne „zu prüfen“ nur Bearbeiten', async () => {
    renderApp(`/belege/${belegIds.b1}`)
    await screen.findByRole('heading', { level: 1, name: 'Hostwerk Süd GmbH' }, warte)
    expect(screen.getByRole('button', { name: /Bearbeiten/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Bestätigen/ })).not.toBeInTheDocument()
  })

  it('NurLesen sieht weder Bearbeiten noch Bestätigen', async () => {
    szenario.rolle('NurLesen')
    renderApp(`/belege/${belegIds.b2}`)
    await screen.findByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' }, warte)
    await waitFor(() => expect(screen.getByLabelText('Betrag')).toBeInTheDocument())
    expect(screen.queryByRole('button', { name: /Bearbeiten/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Bestätigen/ })).not.toBeInTheDocument()
  })

  it('rendert am Desktop weiterhin den bisherigen Kopf', async () => {
    breite(false)
    renderApp(`/belege/${belegIds.b2}`)
    await screen.findByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' }, warte)
    expect(screen.getByRole('button', { name: /Erneut auslesen/ })).toBeInTheDocument()
    expect(screen.queryByLabelText('Betrag')).not.toBeInTheDocument()
  })
})
