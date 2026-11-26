import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

type Anfrage = { methode: string; pfad: string; koerper?: unknown }

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen an `/api/einstellungen…` und `/api/aufbewahrung…`. */
function renderSeite(pfad: string) {
  const anfragen: Anfrage[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (request.method === 'GET' || !/^\/api\/(einstellungen|aufbewahrung)/.test(url.pathname)) return
    const eintrag: Anfrage = { methode: request.method, pfad: url.pathname }
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
  const user = userEvent.setup()
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { user, router, anfragen, ende: () => server.events.removeListener('request:start', mitschnitt) }
}

const formular = (name: string) => screen.findByRole('form', { name }, { timeout: 5000 })
const speichernKnopf = () => screen.getByRole('button', { name: 'Speichern' })

async function gespeichert(anfragen: Anfrage[]) {
  await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
  // Toasts bleiben über Tests hinweg im Speicher, daher „mindestens einer“.
  expect((await screen.findAllByText('Gespeichert')).length).toBeGreaterThan(0)
  return anfragen.at(-1)!
}

describe('Einstellungen', { timeout: 20000 }, () => {
  it('Navigation: dreizehn Einträge in drei Gruppen, Chips für USt-Status und DI-Verbrauch, aktiver Eintrag', async () => {
    const { ende } = renderSeite('/einstellungen')
    const nav = await screen.findByRole('navigation', { name: 'Einstellungsbereiche' }, { timeout: 5000 })
    expect(within(nav).getAllByRole('link')).toHaveLength(13)
    expect(
      within(nav)
        .getAllByRole('group')
        .map((g) => g.getAttribute('aria-label')),
    ).toEqual(['Fachlich', 'Eingang', 'Zugriff & Betrieb'])
    await within(nav).findByText('unbekannt')
    await within(nav).findByText('82 %')
    expect(within(nav).getByText('82 %')).toHaveClass('chip-orange')
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: 'Prüfungen' })).toHaveAttribute('aria-current', 'page'),
    )
    expect(within(nav).getByRole('link', { name: /Mein Konto/ })).toHaveAttribute('href', '/einstellungen/konto')
    expect(screen.getByRole('combobox', { name: 'Bereich' })).toHaveValue('pruefungen')
    ende()
  })

  it('Allgemein lädt die Fixtures und sendet die geänderte Gruppe', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/allgemein')
    await formular('Allgemein')
    const firma = screen.getByLabelText('Firmenname (für Exporte)')
    expect(firma).toHaveValue('Musterfirma GmbH')
    expect(screen.getByLabelText('Ablage')).toHaveValue('/srv/rechnungen/ablage')
    expect(screen.getByLabelText('Ablage')).toHaveAttribute('readonly')
    expect(speichernKnopf()).toBeDisabled()
    await user.clear(firma)
    await user.type(firma, 'Biber GmbH')
    expect(speichernKnopf()).toBeEnabled()
    await user.click(speichernKnopf())
    const anfrage = await gespeichert(anfragen)
    expect(anfrage).toMatchObject({
      methode: 'PUT',
      pfad: '/api/einstellungen/allgemein',
      koerper: { firmenname: 'Biber GmbH', standardwaehrung: 'EUR', zeitzone: 'Europe/Berlin' },
    })
    ende()
  })

  it('Umsatzsteuer: Statuswechsel wird gesendet und erscheint als Chip in der Navigation', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/umsatzsteuer')
    await formular('Umsatzsteuer')
    expect(screen.getByRole('radio', { name: /^Unbekannt/ })).toBeChecked()
    await user.click(screen.getByRole('radio', { name: /Kleinunternehmer/ }))
    await user.selectOptions(screen.getByLabelText('UStVA-Zeitraum'), 'Monatlich')
    await user.click(speichernKnopf())
    const anfrage = await gespeichert(anfragen)
    expect(anfrage).toMatchObject({
      pfad: '/api/einstellungen/ust',
      koerper: { status: 'Kleinunternehmer', zeitraum: 'Monatlich' },
    })
    const nav = screen.getByRole('navigation', { name: 'Einstellungsbereiche' })
    await within(nav).findByText('Kleinunternehmer')
    ende()
  })

  it('Prüfungen: Schwelle „0,85“ wird als 0.85 gesendet, Schalter aus als false', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/pruefungen')
    await formular('Prüfungen & Schwellen')
    expect(screen.getAllByRole('switch')).toHaveLength(12)
    const konfidenz = screen.getByLabelText('Erkennungssicherheit (Konfidenz) Schwelle')
    expect(konfidenz).toHaveValue('0,80')
    expect(screen.getByLabelText('Datum plausibel Schwelle')).toHaveValue('2')
    await user.clear(konfidenz)
    await user.type(konfidenz, '0,85')
    const kosit = screen.getByRole('switch', { name: 'KoSIT-Validierung' })
    await user.click(kosit)
    expect(kosit).toHaveAttribute('aria-checked', 'false')
    await user.click(speichernKnopf())
    const anfrage = await gespeichert(anfragen)
    expect(anfrage.pfad).toBe('/api/einstellungen/pruefungen')
    expect(anfrage.koerper).toMatchObject({
      konfidenzSchwelle: 0.85,
      summenToleranz: 0.02,
      maxAlterJahre: 2,
      kleinbetragGrenze: 250,
      ustIdOnlinePruefen: true,
      skontoVorlaufTage: 3,
      aktiv: { Kosit: false, Dublette: true, UstIdNr: true },
    })
    ende()
  })

  it('Prüfungen: „1,5“ als Konfidenz wird im Browser abgelehnt', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/pruefungen')
    await formular('Prüfungen & Schwellen')
    const konfidenz = screen.getByLabelText('Erkennungssicherheit (Konfidenz) Schwelle')
    await user.clear(konfidenz)
    await user.type(konfidenz, '1,5')
    await user.click(speichernKnopf())
    expect(await screen.findByText('Wert zwischen 0 und 1 eingeben.')).toBeInTheDocument()
    expect(konfidenz).toHaveAttribute('aria-invalid', 'true')
    expect(anfragen).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Verwerfen' }))
    expect(konfidenz).toHaveValue('0,80')
    ende()
  })

  it('Aufbewahrung: „gesperrt bis“ rechnet live um, Leerzustand ohne löschbare Belege, Speichern sendet Jahre', async () => {
    const jahr = new Date().getFullYear()
    const { user, anfragen, ende } = renderSeite('/einstellungen/aufbewahrung')
    await formular('Aufbewahrung & Löschung')
    const bis = screen.getByLabelText(`Belege ${jahr} gesperrt bis`)
    expect(bis).toHaveValue(`31.12.${jahr + 8}`)
    expect(await screen.findByText('Keine Belege mit abgelaufener Frist')).toBeInTheDocument()
    const jahre = screen.getByLabelText('Löschsperre')
    await user.clear(jahre)
    await user.type(jahre, '10')
    expect(bis).toHaveValue(`31.12.${jahr + 10}`)
    await user.click(speichernKnopf())
    const anfrage = await gespeichert(anfragen)
    expect(anfrage).toMatchObject({ pfad: '/api/einstellungen/aufbewahrung', koerper: { jahre: 10 } })
    ende()
  })

  it('Aufbewahrung: Löschdialog erst nach „LÖSCHEN“ aktiv, danach die Pfadliste', async () => {
    db.loeschbar = [{ jahr: 2016, anzahl: 14, fristEnde: '2024-12-31' }]
    db.belege[0] = { ...db.belege[0], rechnungsdatum: '2016-05-03' }
    const pfad = db.belege[0].dateien[0].pfad
    const { user, anfragen, ende } = renderSeite('/einstellungen/aufbewahrung')
    await screen.findByText('14 Belege aus 2016 · Frist endete am 31.12.2024', undefined, { timeout: 5000 })
    await user.click(screen.getByRole('button', { name: 'Löschung freigeben …' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('14 Belege aus 2016 endgültig löschen?')).toBeInTheDocument()
    const loeschen = within(dialog).getByRole('button', { name: 'Endgültig löschen' })
    expect(loeschen).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Zur Bestätigung „LÖSCHEN“ eingeben'), 'LÖSCHEN')
    expect(loeschen).toBeEnabled()
    await user.click(loeschen)
    const liste = await within(dialog).findByLabelText('Pfadliste der Originale')
    expect(liste).toHaveTextContent(pfad)
    expect(within(dialog).getByRole('button', { name: 'Liste kopieren' })).toBeInTheDocument()
    expect(anfragen.at(-1)).toMatchObject({
      pfad: '/api/aufbewahrung/loeschen',
      koerper: { jahr: 2016, bestaetigung: 'LÖSCHEN' },
    })
    ende()
  })

  it('Nicht-Admins sehen alles schreibgeschützt und keinen Löschen-Button', async () => {
    szenario.rolle('Benutzer')
    db.loeschbar = [{ jahr: 2016, anzahl: 14, fristEnde: '2024-12-31' }]
    const { router, ende } = renderSeite('/einstellungen/aufbewahrung')
    await formular('Aufbewahrung & Löschung')
    expect(screen.getByText('Nur Admins können Einstellungen ändern.')).toBeInTheDocument()
    expect(screen.getByLabelText('Löschsperre')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Speichern' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Löschung freigeben …' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Benutzer & Freigaben \d/ })).not.toBeInTheDocument()

    await router.navigate({ to: '/einstellungen/pruefungen' })
    await formular('Prüfungen & Schwellen')
    for (const schalter of screen.getAllByRole('switch')) expect(schalter).toBeDisabled()
    expect(screen.getByLabelText('Datum plausibel Schwelle')).toBeDisabled()
    ende()
  })

  it('Gelöschte Belege: Leerzustand', async () => {
    const { ende } = renderSeite('/einstellungen/geloeschte-belege')
    expect(await screen.findByText('Keine gelöschten Belege', undefined, { timeout: 5000 })).toBeInTheDocument()
    ende()
  })

  it('Gelöschte Belege: Liste mit Bemerkung, Link zum Original und Wiederherstellen', async () => {
    const beleg = db.belege[0]
    db.belege = db.belege.slice(1)
    db.geloeschteBelege = [
      {
        beleg,
        eintrag: {
          id: beleg.id,
          titel: beleg.titel,
          rechnungsnummer: 'RE-FALSCH',
          rechnungsdatum: '2026-03-01',
          korrespondent: 'Fremde GmbH',
          brutto: 119,
          waehrung: 'EUR',
          grund: 'FalschZugeordnet',
          bemerkung: 'Rechnung vom Nachbarn',
          geloeschtAm: '2026-11-20T09:30:00Z',
          geloeschtVon: 'Martin Keller',
          originalId: beleg.dateien[0].id,
        },
      },
    ]
    const { user, ende } = renderSeite('/einstellungen/geloeschte-belege')
    const liste = await screen.findByRole('list', { name: 'Gelöschte Belege' }, { timeout: 5000 })
    expect(within(liste).getByText(/Fremde GmbH · RE-FALSCH · 01\.03\.2026/)).toBeInTheDocument()
    expect(within(liste).getByText('„Rechnung vom Nachbarn“')).toBeInTheDocument()
    expect(within(liste).getByText(/von Martin Keller · falsch zugeordnet/)).toBeInTheDocument()
    expect(within(liste).getByRole('link', { name: /Original ansehen/ })).toHaveAttribute(
      'href',
      `/api/belege/${beleg.id}/dateien/${beleg.dateien[0].id}`,
    )
    await user.click(within(liste).getByRole('button', { name: /Wiederherstellen/ }))
    expect(await screen.findByText('Keine gelöschten Belege')).toBeInTheDocument()
    expect(db.belege.some((b) => b.id === beleg.id)).toBe(true)
    ende()
  })

  it('Gelöschte Belege: Nicht-Admins sehen nur einen Hinweis', async () => {
    szenario.rolle('Benutzer')
    const { ende } = renderSeite('/einstellungen/geloeschte-belege')
    expect(
      await screen.findByText('Gelöschte Belege sehen und wiederherstellen kann nur ein Admin.', undefined, {
        timeout: 5000,
      }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Gelöschte Belege' })).not.toBeInTheDocument()
    ende()
  })
})
