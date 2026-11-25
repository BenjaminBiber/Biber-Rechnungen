import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { korrespondentIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte App ab `pfad` und merkt sich die Anfragen an `/api/korrespondenten…`. */
function renderSeite(pfad = '/korrespondenten') {
  const anfragen: { methode: string; url: URL; koerper?: unknown }[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (!url.pathname.startsWith('/api/korrespondenten')) return
    const eintrag: (typeof anfragen)[number] = { methode: request.method, url }
    anfragen.push(eintrag)
    if (request.method !== 'GET')
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

describe('Korrespondenten', { timeout: 20000 }, () => {
  it('zeigt die zehn Korrespondenten der Fixtures mit „neu“ bei Elektro Haas', async () => {
    const { ende } = renderSeite()
    expect(await zeilen()).toHaveLength(10)
    const haas = screen.getByRole('link', { name: /Elektro Haas/ }).closest('tr')!
    expect(within(haas).getByText('neu')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Autohaus Brenner GmbH' })).toHaveAttribute(
      'href',
      `/korrespondenten/${korrespondentIds.autohausBrenner}`,
    )
    expect(screen.getByText('Steuernr. 06/123/45678')).toBeInTheDocument()
    ende()
  })

  it('setzt Suche und Typ als Query-Parameter', async () => {
    const { router, anfragen, ende } = renderSeite()
    await zeilen()
    await userEvent.type(screen.getByPlaceholderText('Name, Alias, USt-IdNr. oder IBAN'), 'hostwerk')
    await waitFor(() => expect(router.state.location.search).toMatchObject({ suche: 'hostwerk' }))
    await waitFor(async () => expect(await zeilen()).toHaveLength(1))
    expect(anfragen.some((a) => a.url.searchParams.get('suche') === 'hostwerk')).toBe(true)

    await userEvent.click(screen.getByRole('button', { name: 'Alle' }))
    await waitFor(() => expect(router.state.location.search).toMatchObject({ typ: 'Alle', suche: 'hostwerk' }))
    expect(screen.getByRole('button', { name: 'Alle' })).toHaveAttribute('aria-pressed', 'true')
    ende()
  })

  it('zeigt bei „Kunden“ den Leerzustand und bei leerer Suche „Keine Treffer“', async () => {
    const { ende } = renderSeite()
    await zeilen()
    await userEvent.click(screen.getByRole('button', { name: 'Kunden' }))
    expect(await screen.findByText('Noch keine Kunden')).toBeInTheDocument()
    expect(
      screen.getByText('Kunden kommen mit den Ausgangsrechnungen in einer späteren Ausbaustufe.'),
    ).toBeInTheDocument()
    ende()

    const zweiter = renderSeite('/korrespondenten?suche=gibtsnicht')
    expect(await screen.findAllByText('Keine Treffer')).not.toHaveLength(0)
    zweiter.ende()
  })

  it('führt zwei gewählte Korrespondenten mit Vorauswahl des größeren zusammen', async () => {
    const { router, anfragen, ende } = renderSeite()
    await zeilen()
    const knopf = screen.getByRole('button', { name: /zusammenführen/i })
    expect(knopf).toBeDisabled()
    const gross = db.korrespondenten.find((k) => k.id === korrespondentIds.bueroweltKraemer)!
    const klein = db.korrespondenten.find((k) => k.id === korrespondentIds.bueroweltKraemerDublette)!
    expect(gross.anzahlBelege).toBeGreaterThan(klein.anzahlBelege)

    await userEvent.click(screen.getByRole('checkbox', { name: `${klein.name} auswählen` }))
    await userEvent.click(screen.getByRole('checkbox', { name: `${gross.name} auswählen` }))
    expect(screen.getByRole('button', { name: '2 zusammenführen' })).toBeEnabled()
    await userEvent.click(screen.getByRole('button', { name: '2 zusammenführen' }))

    const dialog = await screen.findByRole('dialog', { name: '2 Korrespondenten zusammenführen' })
    expect(within(dialog).getByRole('radio', { name: gross.name })).toBeChecked()
    expect(dialog).toHaveTextContent('Ablagepfade der Dateien bleiben, wo sie sind.')
    expect(router.state.location.search).toMatchObject({ zusammenfuehren: `${klein.id},${gross.id}` })

    await userEvent.click(within(dialog).getByRole('button', { name: 'Zusammenführen' }))
    expect(await screen.findByText('Korrespondenten zusammengeführt')).toBeInTheDocument()
    const post = anfragen.find((a) => a.url.pathname === '/api/korrespondenten/zusammenfuehren')
    await waitFor(() => expect(post?.koerper).toEqual({ behaltenId: gross.id, quellIds: [klein.id] }))
    await waitFor(async () => expect(await zeilen()).toHaveLength(9))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Zusammenführen' })).toBeDisabled()
    ende()
  })

  it('öffnet den Dialog über den Suchparameter „zusammenfuehren“', async () => {
    const { ende } = renderSeite(
      `/korrespondenten?zusammenfuehren=${korrespondentIds.hostwerkSued},${korrespondentIds.autohausBrenner}`,
    )
    const dialog = await screen.findByRole('dialog', { name: '2 Korrespondenten zusammenführen' }, { timeout: 5000 })
    expect(within(dialog).getByRole('radio', { name: 'Autohaus Brenner GmbH' })).toBeChecked()
    ende()
  })

  it('zeigt bei ungültiger IBAN den Feldfehler ohne Anfrage', async () => {
    const { anfragen, ende } = renderSeite('/korrespondenten?neu=true')
    const dialog = await screen.findByRole('dialog', { name: 'Korrespondent anlegen' }, { timeout: 5000 })
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Neue Firma GmbH')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Bankverbindung hinzufügen' }))
    await userEvent.type(within(dialog).getByLabelText('IBAN 1'), 'DE89 3704 0044 0532 0130 01')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    expect(await within(dialog).findByText('IBAN ist ungültig (Prüfziffer).')).toBeInTheDocument()
    expect(anfragen.filter((a) => a.methode === 'POST')).toHaveLength(0)
    ende()
  })

  it('legt mit gültiger IBAN an', async () => {
    const { anfragen, ende } = renderSeite('/korrespondenten?neu=true')
    const dialog = await screen.findByRole('dialog', { name: 'Korrespondent anlegen' }, { timeout: 5000 })
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Neue Firma GmbH')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Bankverbindung hinzufügen' }))
    await userEvent.type(within(dialog).getByLabelText('IBAN 1'), 'de89370400440532013000')
    await userEvent.type(within(dialog).getByLabelText('BIC 1'), 'cobadeff xxx')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    expect(await screen.findByText('Korrespondent angelegt')).toBeInTheDocument()
    const post = anfragen.find((a) => a.methode === 'POST')
    await waitFor(() =>
      expect(post?.koerper).toMatchObject({
        name: 'Neue Firma GmbH',
        bankverbindungen: [{ iban: 'DE89370400440532013000', bic: 'COBADEFFXXX' }],
      }),
    )
    ende()
  })

  it('zeigt bei ungültigem BIC den Feldfehler ohne Anfrage', async () => {
    const { anfragen, ende } = renderSeite('/korrespondenten?neu=true')
    const dialog = await screen.findByRole('dialog', { name: 'Korrespondent anlegen' }, { timeout: 5000 })
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Neue Firma GmbH')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Bankverbindung hinzufügen' }))
    await userEvent.type(within(dialog).getByLabelText('IBAN 1'), 'DE89 3704 0044 0532 0130 00')
    await userEvent.type(within(dialog).getByLabelText('BIC 1'), 'COBA')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    expect(await within(dialog).findByText('BIC ist ungültig (8 oder 11 Stellen).')).toBeInTheDocument()
    expect(anfragen.filter((a) => a.methode === 'POST')).toHaveLength(0)
    ende()
  })

  it('zeigt bei 409 einen Link auf den vorhandenen Korrespondenten', async () => {
    const { ende } = renderSeite('/korrespondenten?neu=true')
    // Liste geladen (hinter dem modalen Dialog, daher Text- statt Rollenabfrage)
    await screen.findByText('Autohaus Brenner GmbH', undefined, { timeout: 5000 })
    const dialog = await screen.findByRole('dialog', { name: 'Korrespondent anlegen' })
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Doppelt')
    await userEvent.type(within(dialog).getByLabelText('USt-IdNr.'), 'DE 811 402 377')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    const link = await within(dialog).findByRole('link', { name: 'Zu Autohaus Brenner GmbH' })
    expect(link).toHaveAttribute('href', `/korrespondenten/${korrespondentIds.autohausBrenner}`)
    ende()
  })

  it('verlinkt bei 409 die korrespondentId aus der Antwort, auch ohne Eintrag in einer geladenen Liste', async () => {
    const unbekannt = '0f9e8d7c-6b5a-4c3d-8e2f-1a0b9c8d7e6f'
    server.use(
      http.post('*/api/korrespondenten', () =>
        HttpResponse.json(
          { title: 'Konflikt', status: 409, detail: 'USt-IdNr. vergeben', korrespondentId: unbekannt },
          { status: 409 },
        ),
      ),
    )
    const { ende } = renderSeite('/korrespondenten?neu=true')
    const dialog = await screen.findByRole('dialog', { name: 'Korrespondent anlegen' }, { timeout: 5000 })
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Doppelt')
    await userEvent.type(within(dialog).getByLabelText('USt-IdNr.'), 'DE999999999')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    const link = await within(dialog).findByRole('link', { name: 'Vorhandenen Korrespondenten öffnen' })
    expect(link).toHaveAttribute('href', `/korrespondenten/${unbekannt}`)
    ende()
  })

  it('zeigt NurLesen weder Auswahl noch Aktionen', async () => {
    szenario.rolle('NurLesen')
    const { ende } = renderSeite()
    expect(await zeilen()).toHaveLength(10)
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Neu anlegen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /zusammenführen/i })).not.toBeInTheDocument()
    ende()
  })
})
