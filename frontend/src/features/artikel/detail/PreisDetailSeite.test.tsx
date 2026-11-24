import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { artikelIds, stammartikelIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

const STAMM = `/artikel/stammartikel/${stammartikelIds.kopierpapier}?jahr=2026`

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen an `/api/stammartikel…`. */
function renderSeite(pfad = STAMM) {
  const anfragen: { methode: string; pfad: string; koerper?: unknown }[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (request.method === 'GET' || !url.pathname.startsWith('/api/stammartikel')) return
    const eintrag: (typeof anfragen)[number] = { methode: request.method, pfad: url.pathname }
    anfragen.push(eintrag)
    void request
      .clone()
      .json()
      .then((k: unknown) => (eintrag.koerper = k))
      .catch(() => undefined)
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
  return { router, anfragen, ende: () => server.events.removeListener('request:start', mitschnitt) }
}

const titel = (name: string) => screen.findByRole('heading', { level: 1, name }, { timeout: 5000 })

describe('Artikel-Detail', { timeout: 20000 }, () => {
  it('zeigt Kopf und Kennzahlen des Stammartikels', async () => {
    const { ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    expect(screen.getByText('Stammartikel', { selector: '.chip' })).toBeInTheDocument()
    expect(
      screen.getByText('Basiseinheit Blatt · Grundpreis je 100 Blatt · 2 Artikel von 2 Korrespondenten · 11 Käufe'),
    ).toBeInTheDocument()
    const kennzahlen = within(screen.getByRole('group', { name: 'Kennzahlen' }))
    expect(kennzahlen.getByText('1,00 €')).toBeInTheDocument()
    expect(kennzahlen.getByText('Bürowelt Krämer KG · je 100 Bl.')).toBeInTheDocument()
    expect(kennzahlen.getByText('+8,7 %')).toBeInTheDocument()
    expect(kennzahlen.getByText('0,92 € → 1,00 €')).toBeInTheDocument()
    expect(kennzahlen.getByText('23.500 Bl.')).toBeInTheDocument()
    expect(kennzahlen.getByText('229,49 €')).toBeInTheDocument()
    ende()
  })

  it('zeigt im Diagramm zwei Serien beim Grundpreis und eine beim Einzelpreis', async () => {
    const user = userEvent.setup()
    const { ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    const bild = screen.getByRole('img', { name: /Preisverlauf 2026, Grundpreis/ })
    expect(bild).toHaveAccessibleName(
      /^Preisverlauf 2026, Grundpreis: Bürowelt Krämer KG von 0,92\s€ auf 1,00\s€; Druckerei Weller von 1,05\s€ auf 1,10\s€$/,
    )
    const legende = within(screen.getByRole('list', { name: 'Legende' }))
    expect(legende.getAllByRole('listitem')).toHaveLength(2)
    expect(legende.getByText('Druckerei Weller · je 100 Bl.')).toBeInTheDocument()
    expect(screen.queryByText(/nur innerhalb gleicher Verpackungseinheit/)).not.toBeInTheDocument()
    const tabelle = screen.getByRole('table', { name: 'Preisverlauf 2026, Grundpreis' })
    expect(within(tabelle).getAllByRole('row')).toHaveLength(8)
    expect(within(tabelle).getByText('1,10 €')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Einzelpreis je Karton' }))
    expect(
      await screen.findByRole('img', { name: /Einzelpreis: Bürowelt Krämer KG von 22,99\s€ auf 24,90\s€$/ }),
    ).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'Legende' })).getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByText('Bürowelt Krämer KG · Karton à 2.500 Bl.')).toBeInTheDocument()
    expect(
      screen.getByText('Einzelpreise sind nur innerhalb gleicher Verpackungseinheit vergleichbar.'),
    ).toBeInTheDocument()
    const einzel = screen.getByRole('table', { name: 'Preisverlauf 2026, Einzelpreis' })
    expect(within(einzel).getByText('23,99 €')).toBeInTheDocument()
    ende()
  })

  it('listet sechs Käufe mit Links auf den Beleg', async () => {
    const { ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    const tabelle = screen.getByRole('table', { name: 'Käufe' })
    expect(within(tabelle).getAllByRole('row').slice(1)).toHaveLength(6)
    expect(within(tabelle).getAllByRole('link')).toHaveLength(6)
    expect(within(tabelle).getByRole('link', { name: '2026-48817' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/belege\/[0-9a-f-]+$/),
    )
    ende()
  })

  it('speichert einen geänderten Umrechnungsfaktor', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    const faktor = screen.getByRole('textbox', { name: 'Umrechnung Papier Kopier A4 80g/m² 500 Bl.' })
    expect(faktor).toHaveValue('500')
    await user.clear(faktor)
    await user.type(faktor, '480')
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(anfragen[0]?.koerper).toBeDefined())
    expect(anfragen[0]).toMatchObject({
      methode: 'PUT',
      pfad: `/api/stammartikel/${stammartikelIds.kopierpapier}`,
      koerper: {
        name: 'Kopierpapier A4 80 g',
        basiseinheit: 'Blatt',
        grundpreisMenge: 100,
        artikel: [
          { artikelId: artikelIds.a1, umrechnungFaktor: 2500 },
          { artikelId: artikelIds.a7, umrechnungFaktor: 480 },
        ],
      },
    })
    expect(await screen.findByText('Stammartikel gespeichert')).toBeInTheDocument()
    ende()
  })

  it('ordnet einen Artikel ohne Stammartikel zu', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    await user.click(screen.getByRole('button', { name: '+ Artikel zuordnen' }))
    await user.type(screen.getByRole('searchbox', { name: 'Artikel suchen' }), 'Ordner')
    await user.click(await screen.findByRole('button', { name: /Ordner breit 8 cm, blau/ }))
    await user.type(screen.getByRole('textbox', { name: /1 Stück =/ }), '1')
    await user.click(screen.getByRole('button', { name: 'Zuordnen' }))
    expect(screen.getByRole('textbox', { name: 'Umrechnung Ordner breit 8 cm, blau' })).toHaveValue('1')
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(anfragen[0]?.koerper).toBeDefined())
    expect((anfragen[0].koerper as { artikel: unknown[] }).artikel).toContainEqual({
      artikelId: artikelIds.a2,
      umrechnungFaktor: 1,
    })
    ende()
  })

  it('löst einen Artikel nach Rückfrage heraus', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    await user.click(screen.getByRole('button', { name: 'Artikel herauslösen' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Artikel herauslösen' })
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Artikel' }), artikelIds.a1)
    expect(anfragen).toHaveLength(0)
    await user.click(within(dialog).getByRole('button', { name: 'Herauslösen' }))
    await waitFor(() => expect(anfragen).toHaveLength(1))
    expect(anfragen[0]).toMatchObject({
      methode: 'DELETE',
      pfad: `/api/stammartikel/${stammartikelIds.kopierpapier}/artikel/${artikelIds.a1}`,
    })
    expect(await screen.findByText('„Kopierpapier A4 80 g“ herausgelöst')).toBeInTheDocument()
    ende()
  })

  it('zeigt beim Artikel ohne Stammartikel den Hinweis', async () => {
    const { ende } = renderSeite(`/artikel/${artikelIds.a2}?jahr=2026`)
    await titel('Ordner breit 8 cm, blau')
    expect(await screen.findByText(/Keinem Stammartikel zugeordnet/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zur Artikelliste' })).toHaveAttribute('href', '/artikel')
    expect(screen.queryByText('Stammartikel', { selector: '.chip' })).not.toBeInTheDocument()
    ende()
  })

  it('verlinkt beim zugeordneten Artikel den Stammartikel', async () => {
    const { ende } = renderSeite(`/artikel/${artikelIds.a1}?jahr=2026`)
    await titel('Kopierpapier A4 80 g')
    expect(await screen.findByRole('link', { name: 'Kopierpapier A4 80 g' })).toHaveAttribute(
      'href',
      `/artikel/stammartikel/${stammartikelIds.kopierpapier}`,
    )
    ende()
  })

  it('zeigt NurLesen keine Bearbeiten-Elemente', async () => {
    szenario.rolle('NurLesen')
    const { ende } = renderSeite()
    await titel('Kopierpapier A4 80 g')
    expect(screen.queryByRole('button', { name: 'Artikel herauslösen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Speichern' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Artikel zuordnen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.getByText('1 Karton = 2.500 Bl.')).toBeInTheDocument()
    ende()
  })

  it('meldet eine unbekannte ID', async () => {
    const { ende } = renderSeite('/artikel/00000000-0000-4000-8000-000000000000')
    await titel('Artikel nicht gefunden')
    ende()
  })
})
