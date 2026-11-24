import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { BelegListe } from '@/api/typen'
import { db } from '@/mocks/db'
import { kategorieIds, korrespondentIds, tagIds } from '@/mocks/fixtures/ids'
import { listeneintrag } from '@/mocks/handlers/belege'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { SPALTEN_SPEICHER } from './spaltenDefinitionen'

/** Rendert die echte App ab `pfad` und merkt sich die Query-Parameter jeder Anfrage an `GET /api/belege`. */
function renderBelege(pfad = '/belege') {
  const anfragen: URLSearchParams[] = []
  // Gibt nichts zurück – die Anfrage geht an den eigentlichen Handler weiter.
  server.use(
    http.get('*/api/belege', ({ request }) => {
      anfragen.push(new URL(request.url).searchParams)
    }),
  )
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [pfad] }),
  })
  const ergebnis = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  const letzte = () => anfragen.at(-1)!
  return { ...ergebnis, router, anfragen, letzte, nutzer: userEvent.setup() }
}

/** Wie das Design: 248 Belege auf 21 Seiten, Summe 20.845,00 €; jede Seite zeigt die zwölf Fixtures. */
function vieleBelege() {
  server.use(
    http.get('*/api/belege', ({ request }) => {
      const seite = Number(new URL(request.url).searchParams.get('seite') ?? 1)
      return HttpResponse.json<BelegListe>({
        eintraege: db.belege.map(listeneintrag),
        gesamt: 248,
        seite,
        seitengroesse: 12,
        summeBrutto: 20845,
      })
    }),
  )
}

const tabellenzeilen = () => screen.getAllByRole('row').slice(1)
const filterleiste = () => screen.getByRole('search', { name: 'Filter' })

async function tabelleGeladen() {
  await screen.findByRole('table')
  await waitFor(() => expect(tabellenzeilen().length).toBeGreaterThan(0))
}

describe('Belegliste', () => {
  it('zeigt in der Standardansicht 12 Zeilen und „248 Belege“', async () => {
    vieleBelege()
    renderBelege()
    expect(await screen.findByRole('heading', { level: 1, name: 'Alle Belege' })).toBeInTheDocument()
    await tabelleGeladen()
    expect(tabellenzeilen()).toHaveLength(12)
    expect(screen.getByText(/248 Belege · Seite 1 von 21/)).toBeInTheDocument()
    expect(screen.getByText(/Summe 20\.845,00\s€/)).toBeInTheDocument()
  })

  it('Ansicht „Zu prüfen“ zeigt die fünf Belege', async () => {
    const { letzte } = renderBelege('/belege?ansicht=ZuPruefen')
    expect(await screen.findByRole('heading', { level: 1, name: 'Zu prüfen' })).toBeInTheDocument()
    await tabelleGeladen()
    expect(tabellenzeilen()).toHaveLength(5)
    expect(letzte().get('ansicht')).toBe('ZuPruefen')
  })

  it('wechselt die Ansicht über das Menü „Ansicht“ mit Zählern', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: /^Ansicht/ }))
    const zuPruefen = await screen.findByRole('menuitemradio', { name: /^Zu prüfen/ })
    expect(zuPruefen).toHaveTextContent('5')
    await nutzer.click(zuPruefen)
    expect(await screen.findByRole('heading', { level: 1, name: 'Zu prüfen' })).toBeInTheDocument()
    expect(router.state.location.search).toMatchObject({ ansicht: 'ZuPruefen' })
  })

  describe('Filter setzen Query-Parameter', () => {
    it('Tags', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Tags/ }))
      await nutzer.click(await screen.findByRole('option', { name: /^Inbox/ }))
      await waitFor(() => expect(letzte().getAll('tagIds')).toEqual([tagIds.inbox]))
    })

    it('Korrespondent (Mehrfachauswahl mit Suche)', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Korrespondent/ }))
      await nutzer.type(await screen.findByRole('combobox', { name: 'Korrespondent suchen' }), 'Hostwerk')
      await nutzer.click(await screen.findByRole('option', { name: /Hostwerk Süd/ }))
      await waitFor(() => expect(letzte().getAll('korrespondentIds')).toEqual([korrespondentIds.hostwerkSued]))
    })

    it('Kategorie', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Kategorie/ }))
      await nutzer.click(await screen.findByRole('option', { name: /Software & Hosting/ }))
      await waitFor(() => expect(letzte().getAll('kategorieIds')).toEqual([kategorieIds.softwareHosting]))
    })

    it('Rechnungsdatum', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Rechnungsdatum/ }))
      await nutzer.type(await screen.findByLabelText('Von'), '01.11.2026')
      await nutzer.type(screen.getByLabelText('Bis'), '30.11.2026{Enter}')
      await waitFor(() => expect(letzte().get('von')).toBe('2026-11-01'))
      expect(letzte().get('bis')).toBe('2026-11-30')
    })

    it('Betrag', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Betrag/ }))
      await nutzer.click(await screen.findByRole('button', { name: 'Netto' }))
      await nutzer.type(screen.getByLabelText('Von (€)'), '200{Enter}')
      await waitFor(() => expect(letzte().get('betragVon')).toBe('200'))
      expect(letzte().get('betragArt')).toBe('Netto')
    })

    it('Zahlstatus', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Zahlstatus/ }))
      await nutzer.click(await screen.findByRole('menuitemradio', { name: 'Überfällig' }))
      await waitFor(() => expect(letzte().get('zahlstatus')).toBe('Ueberfaellig'))
      await waitFor(() => expect(tabellenzeilen()).toHaveLength(2))
    })

    it('Herkunft', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: /^Herkunft/ }))
      await nutzer.click(await screen.findByRole('menuitemcheckbox', { name: /E-Mail/ }))
      await waitFor(() => expect(letzte().getAll('quellen')).toEqual(['Mail']))
    })

    it('aktiver Filter zeigt seine Anzahl; „Filter zurücksetzen“ entfernt alles', async () => {
      const { nutzer, letzte, router } = renderBelege('/belege?zahlstatus=Offen&darstellung=klein')
      await screen.findAllByRole('article')
      expect(within(filterleiste()).getByRole('button', { name: 'Zahlstatus, aktiv: 1' })).toBeInTheDocument()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter zurücksetzen' }))
      await waitFor(() => expect(letzte().get('zahlstatus')).toBeNull())
      expect(router.state.location.search).toEqual({ darstellung: 'klein' })
    })
  })

  it('Suche wird entprellt und berücksichtigt den Umschalter „Rechnungsnr.“', async () => {
    const { nutzer, anfragen, letzte } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Suchfeld: Titel & Inhalt' }))
    await nutzer.click(await screen.findByRole('menuitemradio', { name: 'Rechnungsnr.' }))
    await waitFor(() => expect(letzte().get('suchfeld')).toBeNull())
    await nutzer.type(screen.getByRole('searchbox', { name: 'Rechnungsnummer durchsuchen' }), 'EH-4471')
    await waitFor(() => expect(letzte().get('suche')).toBe('EH-4471'))
    // Nur der fertige Begriff wurde angefragt, keine Zwischenstände.
    expect(anfragen.filter((a) => a.has('suche')).map((a) => a.get('suche'))).toEqual(['EH-4471'])
    expect(letzte().get('suchfeld')).toBe('Rechnungsnummer')
    await waitFor(() => expect(tabellenzeilen()).toHaveLength(1))
  })

  it('wechselt die Darstellung und hält sie in der URL', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    const darstellung = screen.getByRole('radiogroup', { name: 'Darstellung' })
    expect(within(darstellung).getByRole('radio', { name: 'Tabelle' })).toHaveAttribute('aria-checked', 'true')

    await nutzer.click(within(darstellung).getByRole('radio', { name: 'Kleine Karten' }))
    expect(await screen.findAllByRole('article')).toHaveLength(12)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(router.state.location.search).toEqual({ darstellung: 'klein' })

    await nutzer.click(within(darstellung).getByRole('radio', { name: 'Große Karten' }))
    await waitFor(() => expect(router.state.location.search).toEqual({ darstellung: 'gross' }))
    expect(screen.getAllByRole('button', { name: 'Ähnliche' })).toHaveLength(12)
    expect(screen.getByText(/Leistungszeitraum 01\.11\.–30\.11\.2026/)).toBeInTheDocument()

    router.history.back()
    await waitFor(() => expect(router.state.location.search).toEqual({ darstellung: 'klein' }))
  })

  it('sortiert per Klick auf den Spaltenkopf und kehrt die Richtung beim zweiten Klick um', async () => {
    const { nutzer, letzte } = renderBelege()
    await tabelleGeladen()
    const kopf = () => screen.getByRole('columnheader', { name: /^Brutto/ })
    expect(screen.getByRole('columnheader', { name: /^Rechnungsdatum/ })).toHaveAttribute('aria-sort', 'descending')

    await nutzer.click(within(kopf()).getByRole('button'))
    await waitFor(() => expect(letzte().get('sortierung')).toBe('Brutto'))
    expect(kopf()).toHaveAttribute('aria-sort', 'descending')
    await waitFor(() => expect(within(tabellenzeilen()[0]).getByText('534,00 €')).toBeInTheDocument())

    await nutzer.click(within(kopf()).getByRole('button'))
    await waitFor(() => expect(letzte().get('absteigend')).toBe('false'))
    expect(kopf()).toHaveAttribute('aria-sort', 'ascending')
  })

  it('blättert über die Seitennavigation', async () => {
    vieleBelege()
    const { nutzer, letzte, router } = renderBelege()
    await tabelleGeladen()
    const seiten = screen.getByRole('navigation', { name: 'Seiten' })
    expect(within(seiten).getByRole('button', { name: 'Seite 1' })).toHaveAttribute('aria-current', 'page')
    expect(within(seiten).getByRole('button', { name: 'Vorherige Seite' })).toBeDisabled()

    await nutzer.click(within(seiten).getByRole('button', { name: 'Seite 2' }))
    await waitFor(() => expect(letzte().get('seite')).toBe('2'))
    expect(router.state.location.search).toEqual({ seite: 2 })
    expect(await within(seiten).findByRole('button', { name: 'Seite 2' })).toHaveAttribute('aria-current', 'page')

    await nutzer.click(within(seiten).getByRole('button', { name: 'Letzte Seite' }))
    await waitFor(() => expect(letzte().get('seite')).toBe('21'))
    expect(letzte().get('seitengroesse')).toBe('12')
  })

  it('Spaltenwahl blendet „Netto“ ein und bleibt nach erneutem Rendern erhalten', async () => {
    const erster = renderBelege()
    await tabelleGeladen()
    expect(screen.queryByRole('columnheader', { name: 'Netto' })).not.toBeInTheDocument()
    await erster.nutzer.click(screen.getByRole('button', { name: /^Anzeigen/ }))
    const popover = await screen.findByRole('dialog', { name: 'Spalten' })
    expect(within(popover).getByRole('checkbox', { name: 'Korrespondent' })).toBeDisabled()
    await erster.nutzer.click(within(popover).getByRole('checkbox', { name: 'Netto' }))
    expect(screen.getByRole('columnheader', { name: 'Netto' })).toBeInTheDocument()
    expect(JSON.parse(window.localStorage.getItem(SPALTEN_SPEICHER)!)).toContain('netto')
    erster.unmount()

    renderBelege()
    await tabelleGeladen()
    expect(screen.getByRole('columnheader', { name: 'Netto' })).toBeInTheDocument()
    expect(within(tabellenzeilen()[0]).getByText('410,92 €')).toBeInTheDocument()
  })

  it('Spaltenreihenfolge per Pfeiltaste', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: /^Anzeigen/ }))
    const popover = await screen.findByRole('dialog', { name: 'Spalten' })
    within(popover).getByRole('button', { name: 'Korrespondent verschieben (Pfeiltasten)' }).focus()
    await nutzer.keyboard('{ArrowUp}')
    const koepfe = screen.getAllByRole('columnheader').map((k) => k.textContent)
    expect(koepfe.slice(1, 3)).toEqual(['Korrespondent', 'Rechnungsnr.'])
  })

  it('„Auswählen: Seite“ wählt alle Zeilen, ein Filterwechsel leert die Auswahl', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Alle Belege dieser Seite auswählen' }))
    const leiste = screen.getByRole('toolbar', { name: 'Massenaktionen' })
    expect(leiste).toHaveTextContent('12 Belege ausgewählt')
    expect(screen.queryByRole('search', { name: 'Filter' })).not.toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Alle auswählen' })).toBeChecked()

    await nutzer.click(screen.getByRole('button', { name: /^Ansicht/ }))
    await nutzer.click(await screen.findByRole('menuitemradio', { name: /^Inbox/ }))
    expect(await screen.findByRole('search', { name: 'Filter' })).toBeInTheDocument()
    expect(screen.queryByRole('toolbar', { name: 'Massenaktionen' })).not.toBeInTheDocument()
  })

  it('Einzelauswahl und „Auswahl aufheben“', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Bürowelt Krämer KG 2026-48817 auswählen' }))
    expect(screen.getByRole('toolbar', { name: 'Massenaktionen' })).toHaveTextContent('1 Beleg ausgewählt')
    await nutzer.click(screen.getByRole('button', { name: 'Auswahl aufheben' }))
    expect(screen.getByRole('search', { name: 'Filter' })).toBeInTheDocument()
  })

  it('„Auswählen: Alle“ wählt alle Belege der Liste, nicht nur die Seite', async () => {
    vieleBelege()
    const { nutzer, anfragen } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Alle Belege der Liste auswählen' }))
    await waitFor(() => expect(anfragen.some((a) => a.get('seitengroesse') === '248')).toBe(true))
    expect(await screen.findByRole('toolbar', { name: 'Massenaktionen' })).toHaveTextContent('Belege ausgewählt')
  })

  it('überfällige Zeile trägt den Text „überfällig“', async () => {
    renderBelege()
    await tabelleGeladen()
    const zeile = screen.getByRole('row', { name: /EH-4471/ })
    expect(within(zeile).getByText('28.10.2026 · überfällig')).toBeInTheDocument()
    const puenktlich = screen.getByRole('row', { name: /HW-2026-11-0381/ })
    expect(within(puenktlich).queryByText(/überfällig/)).not.toBeInTheDocument()
  })

  it('Titel verlinkt ins Detail', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('link', { name: 'Büromaterial Bestellung' }))
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/belege\/[0-9a-f-]+$/))
  })

  it('speichert die aktuelle Ansicht und wendet eine gespeicherte an', async () => {
    const { nutzer, router } = renderBelege('/belege?ansicht=Offen&darstellung=gross')
    await screen.findAllByRole('article')

    await nutzer.click(screen.getByRole('button', { name: /^Ansicht/ }))
    await nutzer.click(await screen.findByRole('menuitem', { name: /Aktuelle Ansicht speichern/ }))
    const dialog = await screen.findByRole('dialog', { name: 'Ansicht speichern' })
    await nutzer.type(within(dialog).getByLabelText('Name'), 'Meine offenen')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Ansicht speichern' })).not.toBeInTheDocument())
    const neu = db.ansichten.find((a) => a.name === 'Meine offenen')!
    expect(neu.filter.ansicht).toBe('Offen')
    expect(neu.darstellung).toBe('gross')

    await nutzer.click(screen.getByRole('button', { name: /^Ansicht/ }))
    expect(await screen.findByRole('menuitem', { name: 'Meine offenen' })).toBeInTheDocument()
    await nutzer.click(screen.getByRole('menuitem', { name: 'Software & Hosting' }))
    await waitFor(() =>
      expect(router.state.location.search).toEqual({
        darstellung: 'klein',
        kategorien: [kategorieIds.softwareHosting],
      }),
    )
    expect(await screen.findByRole('heading', { level: 1, name: 'Alle Belege' })).toBeInTheDocument()
  })

  it('löscht eine gespeicherte Ansicht', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: /^Ansicht/ }))
    await nutzer.click(await screen.findByRole('menuitem', { name: 'Ansicht „Offen 2026“ löschen' }))
    await waitFor(() => expect(db.ansichten.map((a) => a.name)).not.toContain('Offen 2026'))
  })

  it('stellt den Zustand aus der URL wieder her', async () => {
    const { letzte } = renderBelege(
      `/belege?darstellung=klein&zahlstatus=Offen&sortierung=Brutto&absteigend=false&kategorien=${encodeURIComponent(
        JSON.stringify([kategorieIds.buerobedarf]),
      )}`,
    )
    await waitFor(() => expect(screen.getAllByRole('article')).toHaveLength(1))
    expect(letzte().get('zahlstatus')).toBe('Offen')
    expect(letzte().get('sortierung')).toBe('Brutto')
    expect(letzte().get('absteigend')).toBe('false')
    expect(letzte().getAll('kategorieIds')).toEqual([kategorieIds.buerobedarf])
    expect(screen.getByRole('radio', { name: 'Kleine Karten' })).toHaveAttribute('aria-checked', 'true')
  })

  it('Leerzustand mit „Filter zurücksetzen“', async () => {
    const { nutzer, router } = renderBelege('/belege?suche=gibtsnicht')
    expect(await screen.findByText('Keine Belege für diese Filter')).toBeInTheDocument()
    const knoepfe = screen.getAllByRole('button', { name: 'Filter zurücksetzen' })
    await nutzer.click(knoepfe[knoepfe.length - 1])
    await waitFor(() => expect(router.state.location.search).toEqual({}))
    await tabelleGeladen()
  })

  it('Fehlerzustand mit „Erneut versuchen“', async () => {
    szenario.fehler('/api/belege', 500, 'GET')
    const { nutzer } = renderBelege()
    expect(await screen.findByText('Die Belege konnten nicht geladen werden.')).toBeInTheDocument()
    db.fehler = []
    await nutzer.click(screen.getByRole('button', { name: 'Erneut versuchen' }))
    await tabelleGeladen()
  })

  it('kleine Karte öffnet die Vorschau', async () => {
    const { nutzer } = renderBelege('/belege?darstellung=klein')
    const karte = (await screen.findAllByRole('article'))[2]
    await nutzer.click(within(karte).getByRole('button', { name: 'Vorschau' }))
    const dialog = await screen.findByRole('dialog', { name: 'Vorschau' })
    expect(within(dialog).getByRole('img', { name: 'Seite 1 von 2' })).toHaveAttribute(
      'src',
      expect.stringContaining('/vorschau?seite=1'),
    )
    await nutzer.click(within(dialog).getByRole('button', { name: 'Nächste Seite' }))
    expect(within(dialog).getByRole('img', { name: 'Seite 2 von 2' })).toHaveAttribute(
      'src',
      expect.stringContaining('seite=2'),
    )
  })

  it('große Karte zeigt ähnliche Belege', async () => {
    const { nutzer } = renderBelege('/belege?darstellung=gross')
    const karte = (await screen.findAllByRole('article', { name: /Hosting & Domains November/ }))[0]
    await nutzer.click(within(karte).getByRole('button', { name: 'Ähnliche' }))
    const popover = await screen.findByRole('dialog', { name: 'Ähnliche Belege' })
    expect(await within(popover).findByRole('link', { name: /Hosting & Domains Oktober/ })).toBeInTheDocument()
  })
})
