import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import type { StammartikelAendern } from '@/api/typen'
import { db } from '@/mocks/db'
import { artikelIds, korrespondentIds, stammartikelIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

function renderArtikel(pfad = '/artikel') {
  const anfragen: URLSearchParams[] = []
  const gesendet: { methode: string; url: string; koerper: StammartikelAendern }[] = []
  server.use(
    http.get('*/api/artikel', ({ request }) => {
      const p = new URL(request.url).searchParams
      if (p.get('seitengroesse') !== '1') anfragen.push(p)
    }),
    http.all('*/api/stammartikel*', async ({ request }) => {
      if (request.method === 'POST' || request.method === 'PUT')
        gesendet.push({
          methode: request.method,
          url: request.url,
          koerper: (await request.clone().json()) as StammartikelAendern,
        })
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
  return { router, anfragen, letzte: () => anfragen.at(-1)!, gesendet, nutzer: userEvent.setup() }
}

const zeilen = () => screen.getAllByRole('row').slice(1)
const zeile = (name: string) => zeilen().find((z) => within(z).queryAllByRole('link', { name })[0])!

async function tabelle() {
  await screen.findByRole('table')
  await waitFor(() => expect(zeilen().length).toBeGreaterThan(0))
}

describe('Artikelseite', () => {
  it('zeigt neun Artikel mit Chips in der richtigen Farbe', async () => {
    renderArtikel()
    expect(await screen.findByRole('heading', { level: 1, name: 'Artikel' })).toBeInTheDocument()
    await tabelle()
    expect(zeilen()).toHaveLength(9)
    expect(await screen.findByRole('tab', { name: 'Artikel (9)' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Stammartikel (4)' })).toBeInTheDocument()

    const papier = zeile('Kopierpapier A4 80 g')
    expect(within(papier).getByText(/24,90\s€/)).toBeInTheDocument()
    expect(within(papier).getByText('/ Karton')).toBeInTheDocument()
    expect(within(papier).getByText(/1,00\s€/)).toBeInTheDocument()
    expect(within(papier).getByText('+8,3 %').closest('.chip')).toHaveClass('chip-orange')

    const mobil = zeile('Business Mobil L')
    expect(within(mobil).getByText('−12,5 %').closest('.chip')).toHaveClass('chip-gruen')
    expect(within(mobil).getAllByText('—').length).toBeGreaterThan(0)

    expect(within(zeile('Leasingrate Transporter')).getByText('±0 %').closest('.chip')).toHaveClass('chip-grau')
    expect(within(zeile('Ordner breit 8 cm, blau')).getByText('nicht zugeordnet')).toBeInTheDocument()
    expect(within(zeile('Managed Server M')).getByRole('link', { name: 'Managed Server' })).toHaveAttribute(
      'href',
      `/artikel/stammartikel/${stammartikelIds.managedServer}`,
    )
  })

  it('Filter und Suche setzen Query-Parameter', async () => {
    const { nutzer, letzte, router } = renderArtikel()
    await tabelle()

    await nutzer.type(screen.getByRole('searchbox', { name: 'Artikel suchen' }), 'papier')
    await waitFor(() => expect(letzte().get('suche')).toBe('papier'))
    expect(router.state.location.search).toMatchObject({ suche: 'papier' })
    await waitFor(() => expect(zeilen()).toHaveLength(2))

    await nutzer.click(screen.getByRole('button', { name: 'Filter zurücksetzen' }))
    await waitFor(() => expect(zeilen()).toHaveLength(9))

    await nutzer.click(screen.getByRole('switch', { name: 'Ohne Stammartikel' }))
    await waitFor(() => expect(letzte().get('ohneStammartikel')).toBe('true'))
    await waitFor(() => expect(zeilen()).toHaveLength(3))
    await nutzer.click(screen.getByRole('switch', { name: 'Ohne Stammartikel' }))

    await nutzer.click(screen.getByRole('button', { name: /Preisänderung/ }))
    await nutzer.click(await screen.findByRole('menuitemradio', { name: 'Gefallen' }))
    await waitFor(() => expect(letzte().get('preisaenderung')).toBe('Gefallen'))
    await waitFor(() => expect(zeilen()).toHaveLength(3))
    expect(router.state.location.search).toMatchObject({ preisaenderung: 'Gefallen' })
  })

  it('Korrespondent-Filter setzt korrespondentId', async () => {
    const { nutzer, letzte } = renderArtikel()
    await tabelle()
    await nutzer.click(screen.getByRole('button', { name: /Korrespondent/ }))
    await nutzer.click(await screen.findByRole('option', { name: /Druckerei Weller/ }))
    await waitFor(() => expect(letzte().get('korrespondentId')).toBe(korrespondentIds.druckereiWeller))
    await waitFor(() => expect(zeilen()).toHaveLength(1))
  })

  it('Reiterwechsel zeigt vier Stammartikel-Karten', async () => {
    const { nutzer, router } = renderArtikel()
    await tabelle()
    await nutzer.click(screen.getByRole('tab', { name: /Stammartikel/ }))
    expect(await screen.findAllByRole('article')).toHaveLength(4)
    expect(router.state.location.search).toMatchObject({ reiter: 'stammartikel' })
    const papier = screen.getByRole('article', { name: 'Kopierpapier A4 80 g' })
    expect(within(papier).getByText('je 100 Blatt')).toBeInTheDocument()
    expect(within(papier).getByText('2 Artikel · Bürowelt Krämer KG, Druckerei Weller')).toBeInTheDocument()
    expect(within(papier).getByText(/Günstigster: 1,00\s€ \(Bürowelt\)/)).toBeInTheDocument()
    expect(within(papier).getByText('+8,3 %')).toBeInTheDocument()
  })

  it('Leerzustand ohne Stammartikel', async () => {
    db.stammartikel = []
    renderArtikel('/artikel?reiter=stammartikel')
    expect(
      await screen.findByText(
        'Noch keine Stammartikel – markiere im Reiter ‚Artikel‘ zwei gleiche Artikel verschiedener Händler.',
      ),
    ).toBeInTheDocument()
  })

  it('Knopf zählt die Auswahl; Dialog rechnet Grundpreise und sendet die Anfrage', async () => {
    // a1 und a7 gehören schon zu Kopierpapier – für die Neuanlage beide freistellen.
    db.artikel = db.artikel.map((a) =>
      a.id === artikelIds.a1 || a.id === artikelIds.a7 ? { ...a, stammartikel: null } : a,
    )
    db.stammartikel = db.stammartikel.filter((s) => s.id !== stammartikelIds.kopierpapier)
    const { nutzer, gesendet, router } = renderArtikel()
    await tabelle()
    const knopf = screen.getByRole('button', { name: 'Zu Stammartikel zusammenführen' })
    expect(knopf).toBeDisabled()

    await nutzer.click(screen.getByRole('checkbox', { name: 'Ordner breit 8 cm, blau auswählen' }))
    expect(knopf).toBeDisabled()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Ordner breit 8 cm, blau auswählen' }))

    await nutzer.click(screen.getByRole('checkbox', { name: 'Kopierpapier A4 80 g auswählen' }))
    await nutzer.click(screen.getByRole('checkbox', { name: 'Papier Kopier A4 80g/m² 500 Bl. auswählen' }))
    await nutzer.click(screen.getByRole('button', { name: '2 zu Stammartikel zusammenführen' }))

    const dialog = await screen.findByRole('dialog', { name: 'Stammartikel bilden' })
    expect(within(dialog).getByRole('textbox', { name: 'Name' })).toHaveValue('Kopierpapier A4 80 g')
    await nutzer.selectOptions(within(dialog).getByRole('combobox', { name: 'Basiseinheit' }), 'Blatt')
    const menge = within(dialog).getByRole('textbox', { name: 'Grundpreis je' })
    await nutzer.clear(menge)
    await nutzer.type(menge, '100')
    await nutzer.type(within(dialog).getByRole('textbox', { name: 'Faktor Kopierpapier A4 80 g' }), '2500')

    // Faktor 0 wird abgelehnt.
    await nutzer.type(within(dialog).getByRole('textbox', { name: 'Faktor Papier Kopier A4 80g/m² 500 Bl.' }), '0')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Stammartikel anlegen' }))
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('größer als 0')
    expect(gesendet).toHaveLength(0)

    const faktor = within(dialog).getByRole('textbox', { name: 'Faktor Papier Kopier A4 80g/m² 500 Bl.' })
    await nutzer.clear(faktor)
    await nutzer.type(faktor, '500')
    expect(within(dialog).getByRole('status')).toHaveTextContent(
      /Grundpreis je 100 Blatt: Bürowelt 1,00\s€ · Weller 1,10\s€/,
    )

    await nutzer.click(within(dialog).getByRole('button', { name: 'Stammartikel anlegen' }))
    await waitFor(() => expect(gesendet).toHaveLength(1))
    expect(gesendet[0].methode).toBe('POST')
    expect(gesendet[0].koerper).toEqual({
      name: 'Kopierpapier A4 80 g',
      basiseinheit: 'Blatt',
      grundpreisMenge: 100,
      artikel: [
        { artikelId: artikelIds.a1, umrechnungFaktor: 2500 },
        { artikelId: artikelIds.a7, umrechnungFaktor: 500 },
      ],
    })
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/artikel\/stammartikel\/[0-9a-f-]+$/))
    expect(await screen.findByText('Stammartikel „Kopierpapier A4 80 g“ angelegt')).toBeInTheDocument()
  })

  it('Artikel mit vorhandenem Stammartikel führt zum Zuordnen', async () => {
    const { nutzer, gesendet, router } = renderArtikel()
    await tabelle()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Kopierpapier A4 80 g auswählen' }))
    await nutzer.click(screen.getByRole('checkbox', { name: 'Ordner breit 8 cm, blau auswählen' }))
    await nutzer.click(screen.getByRole('button', { name: '2 zu Stammartikel zusammenführen' }))

    const dialog = await screen.findByRole('dialog', { name: 'Stammartikel zuordnen' })
    expect(within(dialog).queryByRole('textbox', { name: 'Name' })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('textbox', { name: 'Faktor Kopierpapier A4 80 g' })).not.toBeInTheDocument()
    await nutzer.type(within(dialog).getByRole('textbox', { name: 'Faktor Ordner breit 8 cm, blau' }), '1')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Zuordnen' }))

    await waitFor(() => expect(gesendet).toHaveLength(1))
    expect(gesendet[0].methode).toBe('PUT')
    expect(gesendet[0].url).toContain(stammartikelIds.kopierpapier)
    expect(gesendet[0].koerper.artikel).toContainEqual({ artikelId: artikelIds.a2, umrechnungFaktor: 1 })
    expect(gesendet[0].koerper.artikel).toContainEqual({ artikelId: artikelIds.a1, umrechnungFaktor: 2500 })
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/artikel/stammartikel/${stammartikelIds.kopierpapier}`),
    )
  })

  it('NurLesen: keine Auswahl, kein Zusammenführen', async () => {
    szenario.rolle('NurLesen')
    renderArtikel()
    await tabelle()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /zusammenführen/ })).not.toBeInTheDocument()
  })
})
