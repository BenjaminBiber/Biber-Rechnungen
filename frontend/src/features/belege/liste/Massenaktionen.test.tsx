import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { Massenaktion } from '@/api/typen'
import { db } from '@/mocks/db'
import { kategorieIds, tagIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte App ab `pfad`; merkt sich jede Massenaktion und zählt die Abrufe der Liste. */
function renderBelege(pfad = '/belege?ansicht=ZuPruefen') {
  const aktionen: Massenaktion[] = []
  let listenAbrufe = 0
  server.use(
    http.post('*/api/belege/massenaktion', async ({ request }) => {
      aktionen.push((await request.clone().json()) as Massenaktion)
    }),
    http.get('*/api/belege', () => {
      listenAbrufe++
    }),
  )
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
  return { router, aktionen, listenAbrufe: () => listenAbrufe, nutzer: userEvent.setup() }
}

const zeilen = () => screen.getAllByRole('row').slice(1)
const leiste = () => screen.getByRole('toolbar', { name: 'Massenaktionen' })

/** Wählt die ersten `anzahl` Zeilen der Tabelle und gibt ihre Beleg-IDs zurück. */
async function waehle(nutzer: ReturnType<typeof userEvent.setup>, anzahl: number) {
  await screen.findByRole('table', {}, { timeout: 5000 })
  await waitFor(() => expect(zeilen().length).toBeGreaterThanOrEqual(anzahl))
  for (const zeile of zeilen().slice(0, anzahl)) await nutzer.click(within(zeile).getByRole('checkbox'))
  return leiste()
}

async function nachAktion(listenAbrufeVorher: number, listenAbrufe: () => number, toast: RegExp) {
  expect(await screen.findByText(toast)).toBeInTheDocument()
  await waitFor(() => expect(screen.queryByRole('toolbar', { name: 'Massenaktionen' })).not.toBeInTheDocument())
  await waitFor(() => expect(listenAbrufe()).toBeGreaterThan(listenAbrufeVorher))
}

describe('Massenaktionen', { timeout: 20_000 }, () => {
  it('zeigt Anzahl und Summe der gewählten Belege', async () => {
    const { nutzer } = renderBelege()
    const l = await waehle(nutzer, 2)
    expect(l).toHaveTextContent('2 Belege ausgewählt')
    expect(l).toHaveTextContent(/·\s*[\d.]+,\d\d\s€/)
  })

  it('Bestätigen sendet „Bestaetigen“ für alle gewählten', async () => {
    const { nutzer, aktionen, listenAbrufe } = renderBelege()
    const l = await waehle(nutzer, 3)
    const vorher = listenAbrufe()
    await nutzer.click(within(l).getByRole('button', { name: 'Bestätigen' }))
    await nachAktion(vorher, listenAbrufe, /3 Belege bestätigt/)
    expect(aktionen).toEqual([{ aktion: 'Bestaetigen', belegIds: expect.any(Array) }])
    expect(aktionen[0].belegIds).toHaveLength(3)
  })

  it('Tags setzt hinzugefügte Tags', async () => {
    const { nutzer, aktionen, listenAbrufe } = renderBelege()
    const l = await waehle(nutzer, 2)
    const vorher = listenAbrufe()
    await nutzer.click(within(l).getByRole('button', { name: 'Tags' }))
    await nutzer.click(await screen.findByRole('option', { name: /Reisekosten/ }))
    await nachAktion(vorher, listenAbrufe, /Tags bei 2 Belegen gesetzt/)
    expect(aktionen).toEqual([{ aktion: 'TagsSetzen', belegIds: expect.any(Array), tagIds: [tagIds.reise] }])
  })

  it('Tags entfernt abgewählte gemeinsame Tags', async () => {
    const { nutzer, aktionen } = renderBelege()
    const l = await waehle(nutzer, 2)
    await nutzer.click(within(l).getByRole('button', { name: 'Tags' }))
    const gewaehlt = (await screen.findAllByRole('option')).filter(
      (o) => o.getAttribute('aria-selected') === 'true' && o.getAttribute('aria-disabled') !== 'true',
    )
    expect(gewaehlt.length).toBeGreaterThan(0)
    await nutzer.click(gewaehlt[0])
    await screen.findByText(/Tags bei \d+ Belegen? entfernt/)
    expect(aktionen[0].aktion).toBe('TagsEntfernen')
    expect(aktionen[0].tagIds).toHaveLength(1)
  })

  it('Kategorie sendet „KategorieSetzen“', async () => {
    const { nutzer, aktionen, listenAbrufe } = renderBelege()
    const l = await waehle(nutzer, 2)
    const vorher = listenAbrufe()
    await nutzer.click(within(l).getByRole('button', { name: 'Kategorie' }))
    await nutzer.click(await screen.findByRole('option', { name: /Bürobedarf/ }))
    await nachAktion(vorher, listenAbrufe, /Kategorie bei \d+ Belege?n? gesetzt/)
    expect(aktionen).toEqual([
      { aktion: 'KategorieSetzen', belegIds: expect.any(Array), kategorieId: expect.any(String) },
    ])
    expect(Object.values(kategorieIds)).toContain(aktionen[0].kategorieId)
  })

  it('Als bezahlt markieren setzt den gewählten Zahlungs-Tag', async () => {
    const { nutzer, aktionen, listenAbrufe } = renderBelege()
    const l = await waehle(nutzer, 2)
    const vorher = listenAbrufe()
    await nutzer.click(within(l).getByRole('button', { name: 'Als bezahlt markieren' }))
    await nutzer.click(await screen.findByRole('menuitem', { name: 'bezahlt PayPal' }))
    await nachAktion(vorher, listenAbrufe, /2 Belege als bezahlt markiert/)
    expect(aktionen).toEqual([{ aktion: 'TagsSetzen', belegIds: expect.any(Array), tagIds: [tagIds.pp] }])
  })

  it('setzt bei genau einem Zahlungs-Tag sofort', async () => {
    db.tags = db.tags.filter((t) => !t.istZahlungsTag || t.id === tagIds.bez)
    const { nutzer, aktionen } = renderBelege()
    const l = await waehle(nutzer, 1)
    await nutzer.click(within(l).getByRole('button', { name: 'Als bezahlt markieren' }))
    await screen.findByText(/1 Beleg als bezahlt markiert/)
    expect(aktionen[0]).toMatchObject({ aktion: 'TagsSetzen', tagIds: [tagIds.bez] })
  })

  it('Exportieren navigiert mit den Listenfiltern nach /export', async () => {
    const { nutzer, router } = renderBelege(
      `/belege?von=2020-01-01&bis=2030-12-31&kategorien=${kategorieIds.softwareHosting}`,
    )
    const l = await waehle(nutzer, 1)
    await nutzer.click(within(l).getByRole('button', { name: 'Exportieren' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/export'))
    expect(router.state.location.search).toMatchObject({
      von: '2020-01-01',
      bis: '2030-12-31',
      kategorien: [kategorieIds.softwareHosting],
    })
  })

  it('Fehler zeigt einen stehenden Toast mit „Erneut“', async () => {
    const { nutzer } = renderBelege()
    server.use(http.post('*/api/belege/massenaktion', () => HttpResponse.json({}, { status: 500 })))
    const l = await waehle(nutzer, 1)
    await nutzer.click(within(l).getByRole('button', { name: 'Bestätigen' }))
    expect(await screen.findByText('Die Massenaktion ist fehlgeschlagen.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Erneut' })).toBeInTheDocument()
    expect(leiste()).toBeInTheDocument()
  })

  it('NurLesen sieht nur „Exportieren“ und „Auswahl aufheben“', async () => {
    szenario.rolle('NurLesen')
    const { nutzer } = renderBelege()
    const l = await waehle(nutzer, 1)
    await waitFor(() =>
      expect(
        within(l)
          .getAllByRole('button')
          .map((b) => b.textContent),
      ).toEqual([expect.stringContaining('Exportieren'), 'Auswahl aufheben']),
    )
  })
})
