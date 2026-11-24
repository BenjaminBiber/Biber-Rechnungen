import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { kategorieIds, tagIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte App ab `pfad` und merkt sich die ändernden Anfragen an Tags und Kategorien. */
function renderSeite(pfad = '/tags') {
  const anfragen: { methode: string; pfad: string; koerper?: unknown }[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (request.method === 'GET' || !/^\/api\/(tags|kategorien)/.test(url.pathname)) return
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

const zeilen = async () => {
  const tabelle = await screen.findByRole('table', undefined, { timeout: 5000 })
  return within(tabelle).getAllByRole('row').slice(1)
}

const zeile = (name: string) => screen.getAllByText(name)[0].closest('tr')!

describe('Tags & Kategorien', { timeout: 20000 }, () => {
  it('zeigt sieben Tags mit Sonderform und Matching, der System-Tag hat kein „Bearbeiten“', async () => {
    const { ende } = renderSeite()
    expect(await zeilen()).toHaveLength(7)
    expect(screen.getByRole('tab', { name: 'Tags (7)' })).toHaveAttribute('aria-selected', 'true')
    expect(await screen.findByRole('tab', { name: 'Kategorien (6)' })).toBeInTheDocument()
    expect(within(zeile('Inbox')).getByText('Inbox-Tag')).toBeInTheDocument()
    expect(within(zeile('bezahlt PayPal')).getByText('Zahlungs-Tag')).toBeInTheDocument()
    expect(within(zeile('bezahlt PayPal')).getByText('Ein Wort: paypal')).toBeInTheDocument()
    expect(within(zeile('Reisekosten')).getByText('Alle Wörter: hotel übernachtung')).toBeInTheDocument()
    expect(within(zeile('Lastschrift')).getByText('Regex')).toBeInTheDocument()
    const pruef = zeile('zu prüfen')
    expect(within(pruef).getByText('System')).toBeInTheDocument()
    expect(within(pruef).queryByRole('button', { name: /bearbeiten$/ })).not.toBeInTheDocument()
    expect(within(pruef).getByRole('button', { name: /System-Tag/ })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /bearbeiten$/ })).toHaveLength(6)
    expect(screen.getByText(/„zu prüfen“ ist ein System-Tag/)).toBeInTheDocument()
    ende()
  })

  it('zeigt sechs Kategorien mit EÜR-Zeile, „nicht gesetzt“ und Summen', async () => {
    const { ende } = renderSeite('/tags?reiter=kategorien')
    expect(await zeilen()).toHaveLength(6)
    const jahr = new Date().getFullYear()
    expect(screen.getByRole('columnheader', { name: `Summe ${jahr}` })).toBeInTheDocument()
    expect(within(zeile('Sonstiges')).getByText('nicht gesetzt')).toBeInTheDocument()
    expect(within(zeile('Fahrzeug')).getByText('Exakt: Leasingrate')).toBeInTheDocument()
    expect(within(zeile('Hardware')).getByText('3.808,50 €')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Neue Kategorie' })).toBeInTheDocument()
    ende()
  })

  it('öffnet den vorbelegten Dialog für „bezahlt PayPal“ und sendet das TagAendernDto', async () => {
    const { router, anfragen, ende } = renderSeite()
    await zeilen()
    await userEvent.click(screen.getByRole('button', { name: 'bezahlt PayPal bearbeiten' }))
    await waitFor(() => expect(router.state.location.search).toMatchObject({ bearbeiten: tagIds.pp }))
    const dialog = await screen.findByRole('dialog', { name: 'Tag bearbeiten' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('bezahlt PayPal')
    expect(within(dialog).getByRole('radio', { name: 'Petrol' })).toHaveAttribute('aria-checked', 'true')
    expect(within(dialog).getByRole('radio', { name: /Zahlungs-Tag/ })).toBeChecked()
    expect(within(dialog).getByLabelText('Algorithmus')).toHaveDisplayValue('Ein Wort trifft zu')
    expect(within(dialog).getByLabelText('Muster')).toHaveValue('paypal')

    await userEvent.click(within(dialog).getByRole('radio', { name: 'Lila' }))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(anfragen).toContainEqual({
      methode: 'PUT',
      pfad: `/api/tags/${tagIds.pp}`,
      koerper: {
        name: 'bezahlt PayPal',
        farbe: 'lila',
        istInbox: false,
        istZahlungsTag: true,
        matching: { algorithmus: 'EinWort', muster: 'paypal', grossKlein: false },
      },
    })
    ende()
  })

  it('startet „Neuer Tag“ leer und zeigt den Inbox-Hinweis', async () => {
    const { anfragen, ende } = renderSeite()
    await zeilen()
    await userEvent.click(screen.getByRole('button', { name: 'Neuer Tag' }))
    const dialog = await screen.findByRole('dialog', { name: 'Neuer Tag' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('')
    expect(within(dialog).getByRole('radio', { name: 'Keine' })).toBeChecked()
    expect(within(dialog).queryByRole('button', { name: 'Löschen' })).not.toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('radio', { name: /Inbox-Tag/ }))
    expect(within(dialog).getByText('Ersetzt „Inbox“ als Inbox-Tag')).toBeInTheDocument()

    await userEvent.type(within(dialog).getByLabelText('Name'), 'Eingang')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(anfragen.find((a) => a.methode === 'POST')?.koerper).toBeDefined())
    expect(anfragen.find((a) => a.methode === 'POST')).toMatchObject({
      pfad: '/api/tags',
      koerper: { name: 'Eingang', farbe: 'blau', istInbox: true, istZahlungsTag: false },
    })
    ende()
  })

  it('speichert eine Kategorie mit EÜR-Zeile', async () => {
    const { anfragen, ende } = renderSeite(`/tags?reiter=kategorien&bearbeiten=${kategorieIds.sonstiges}`)
    const dialog = await screen.findByRole('dialog', { name: 'Kategorie bearbeiten' })
    expect(within(dialog).getByRole('radio', { name: 'Grau' })).toHaveAttribute('aria-checked', 'false')
    await userEvent.type(within(dialog).getByLabelText('EÜR-Zeile (optional)'), 'Sonstige Kosten')
    await userEvent.click(within(dialog).getByRole('radio', { name: 'Orange' }))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(anfragen).toContainEqual({
      methode: 'PUT',
      pfad: `/api/kategorien/${kategorieIds.sonstiges}`,
      koerper: {
        name: 'Sonstiges',
        farbe: '#E3893B',
        euerZeile: 'Sonstige Kosten',
        matching: { algorithmus: 'Keiner', muster: null, grossKlein: false },
      },
    })
    expect(await screen.findByText('Sonstige Kosten')).toBeInTheDocument()
    ende()
  })

  it('löscht nach Rückfrage mit der Anzahl betroffener Belege', async () => {
    const { anfragen, ende } = renderSeite(`/tags?bearbeiten=${tagIds.reise}`)
    const dialog = await screen.findByRole('dialog', { name: 'Tag bearbeiten' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Löschen' }))
    const rueckfrage = await screen.findByRole('alertdialog', { name: 'Tag „Reisekosten“ löschen?' })
    expect(within(rueckfrage).getByText(/14 Belege sind betroffen/)).toBeInTheDocument()
    await userEvent.click(within(rueckfrage).getByRole('button', { name: 'Löschen' }))
    await waitFor(async () => expect(await zeilen()).toHaveLength(6))
    expect(anfragen).toContainEqual({ methode: 'DELETE', pfad: `/api/tags/${tagIds.reise}` })
    expect(db.tags.some((t) => t.id === tagIds.reise)).toBe(false)
    ende()
  })

  it('zeigt 409 am Namensfeld', async () => {
    server.use(
      http.put('*/api/tags/:id', () =>
        HttpResponse.json(
          { title: 'Konflikt', status: 409 },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )
    const { ende } = renderSeite(`/tags?bearbeiten=${tagIds.ls}`)
    const dialog = await screen.findByRole('dialog', { name: 'Tag bearbeiten' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Name ist bereits vergeben')
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
    ende()
  })

  it('öffnet für den System-Tag keinen Dialog', async () => {
    const { ende } = renderSeite(`/tags?bearbeiten=${tagIds.pruef}`)
    await zeilen()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    ende()
  })

  it('zeigt NurLesen keine Buttons zum Anlegen und Bearbeiten', async () => {
    szenario.rolle('NurLesen')
    const { ende } = renderSeite(`/tags?bearbeiten=${tagIds.pp}`)
    expect(await zeilen()).toHaveLength(7)
    expect(screen.queryByRole('button', { name: 'Neuer Tag' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /bearbeiten$/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    ende()
  })
})
