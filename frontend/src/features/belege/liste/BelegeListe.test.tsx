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

/** Wartet, bis die Kartenansicht ihre Belege zeigt. */
async function kartenGeladen() {
  await screen.findAllByRole('article')
}

/** Fügt eine Filter-Pill hinzu; ihre Auswahl öffnet sich dabei von selbst. */
async function filterHinzufuegen(nutzer: ReturnType<typeof userEvent.setup>, titel: string) {
  await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter hinzufügen' }))
  await nutzer.click(await screen.findByRole('menuitem', { name: titel }))
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

  it('zeigt in keiner Darstellung ein Menü „Ansicht“', async () => {
    const tabelle = renderBelege()
    await tabelleGeladen()
    expect(screen.queryByRole('button', { name: /^Ansicht/ })).not.toBeInTheDocument()
    tabelle.unmount()

    renderBelege('/belege?darstellung=klein')
    await kartenGeladen()
    expect(screen.queryByRole('button', { name: /^Ansicht/ })).not.toBeInTheDocument()
  })

  describe('Filterleiste (Kartenansicht) setzt Query-Parameter', () => {
    it('Tags', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Tags')
      await nutzer.click(await screen.findByRole('option', { name: /^Inbox/ }))
      await waitFor(() => expect(letzte().getAll('tagIds')).toEqual([tagIds.inbox]))
    })

    it('Kartenansichten zeigen keine Spaltenwahl', async () => {
      renderBelege('/belege?darstellung=gross')
      await kartenGeladen()
      expect(screen.queryByRole('button', { name: /^Spalten/ })).not.toBeInTheDocument()
    })

    it('Tags: System-Tag „zu prüfen“ ist zum Filtern wählbar', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Tags')
      const zuPruefen = await screen.findByRole('option', { name: /^zu prüfen/ })
      expect(zuPruefen).not.toHaveAttribute('aria-disabled')
      await nutzer.click(zuPruefen)
      await waitFor(() => expect(letzte().getAll('tagIds')).toEqual([tagIds.pruef]))
    })

    it('Korrespondent (Mehrfachauswahl mit Suche)', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Korrespondent')
      await nutzer.type(await screen.findByRole('combobox', { name: 'Korrespondent suchen' }), 'Hostwerk')
      await nutzer.click(await screen.findByRole('option', { name: /Hostwerk Süd/ }))
      await waitFor(() => expect(letzte().getAll('korrespondentIds')).toEqual([korrespondentIds.hostwerkSued]))
    })

    it('Kategorie', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Kategorie')
      await nutzer.click(await screen.findByRole('option', { name: /Software & Hosting/ }))
      await waitFor(() => expect(letzte().getAll('kategorieIds')).toEqual([kategorieIds.softwareHosting]))
    })

    it('Rechnungsdatum', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Rechnungsdatum')
      await nutzer.type(await screen.findByLabelText('Von'), '01.11.2026')
      await nutzer.type(screen.getByLabelText('Bis'), '30.11.2026{Enter}')
      await waitFor(() => expect(letzte().get('von')).toBe('2026-11-01'))
      expect(letzte().get('bis')).toBe('2026-11-30')
    })

    it('Betrag', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Betrag')
      await nutzer.click(await screen.findByRole('button', { name: 'Netto' }))
      await nutzer.type(screen.getByLabelText('Von (€)'), '200{Enter}')
      await waitFor(() => expect(letzte().get('betragVon')).toBe('200'))
      expect(letzte().get('betragArt')).toBe('Netto')
    })

    it('Zahlstatus', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Zahlstatus')
      await nutzer.click(await screen.findByRole('menuitemradio', { name: 'Überfällig' }))
      await waitFor(() => expect(letzte().get('zahlstatus')).toBe('Ueberfaellig'))
      await waitFor(() => expect(screen.getAllByRole('article')).toHaveLength(2))
    })

    it('Herkunft', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein')
      await kartenGeladen()
      await filterHinzufuegen(nutzer, 'Herkunft')
      await nutzer.click(await screen.findByRole('menuitemcheckbox', { name: /E-Mail/ }))
      await waitFor(() => expect(letzte().getAll('quellen')).toEqual(['Mail']))
    })

    it('anfangs nur „Filter hinzufügen“; das × einer Pill entfernt ihren Filter', async () => {
      const { nutzer, letzte } = renderBelege('/belege?darstellung=klein&quellen=Mail')
      await kartenGeladen()
      expect(within(filterleiste()).getByRole('button', { name: /^Herkunft: E-Mail/ })).toBeInTheDocument()
      expect(within(filterleiste()).queryByRole('button', { name: /^Tags/ })).not.toBeInTheDocument()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter Herkunft entfernen' }))
      await waitFor(() => expect(letzte().get('quellen')).toBeNull())
      expect(within(filterleiste()).queryByRole('group', { name: 'Filter Herkunft' })).not.toBeInTheDocument()
      expect(within(filterleiste()).queryByRole('button', { name: 'Filter zurücksetzen' })).not.toBeInTheDocument()
      expect(within(filterleiste()).getByRole('button', { name: 'Filter hinzufügen' })).toBeInTheDocument()
    })

    it('aktiver Filter erscheint als Pill mit Wert; „Filter zurücksetzen“ entfernt alles', async () => {
      const { nutzer, letzte, router } = renderBelege('/belege?zahlstatus=Offen&darstellung=klein')
      await screen.findAllByRole('article')
      expect(within(filterleiste()).getByRole('button', { name: /^Zahlstatus: Offen/ })).toBeInTheDocument()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter zurücksetzen' }))
      await waitFor(() => expect(letzte().get('zahlstatus')).toBeNull())
      expect(router.state.location.search).toEqual({ darstellung: 'klein' })
    })
  })

  describe('Filterleiste (Tabelle)', () => {
    it('Tabelle zeigt die Filterleiste, aber kein Sortiermenü und keine Filter in den Spaltenköpfen', async () => {
      renderBelege()
      await tabelleGeladen()
      expect(within(filterleiste()).getByRole('button', { name: 'Filter hinzufügen' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /^Sortierung:/ })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /filtern/ })).not.toBeInTheDocument()
      expect(screen.getByRole('button', { name: /^Spalten/ })).toBeInTheDocument()
      expect(screen.getByRole('search', { name: 'Belege durchsuchen' })).toBeInTheDocument()
    })

    it('Korrespondent per Pill setzen und mit × wieder entfernen', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await filterHinzufuegen(nutzer, 'Korrespondent')
      await nutzer.type(await screen.findByRole('combobox', { name: 'Korrespondent suchen' }), 'Hostwerk')
      await nutzer.click(await screen.findByRole('option', { name: /Hostwerk Süd/ }))
      await waitFor(() => expect(letzte().getAll('korrespondentIds')).toEqual([korrespondentIds.hostwerkSued]))
      await nutzer.keyboard('{Escape}')
      expect(
        within(filterleiste()).getByRole('button', { name: 'Korrespondent: Hostwerk Süd GmbH' }),
      ).toBeInTheDocument()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter Korrespondent entfernen' }))
      await waitFor(() => expect(letzte().get('korrespondentIds')).toBeNull())
    })

    it('Zahlstatus filtert die Tabellenzeilen', async () => {
      const { nutzer, letzte } = renderBelege()
      await tabelleGeladen()
      await filterHinzufuegen(nutzer, 'Zahlstatus')
      await nutzer.click(await screen.findByRole('menuitemradio', { name: 'Überfällig' }))
      await waitFor(() => expect(letzte().get('zahlstatus')).toBe('Ueberfaellig'))
      await waitFor(() => expect(tabellenzeilen()).toHaveLength(2))
    })

    it('gesetzte Filter erscheinen als Pills; „Filter zurücksetzen“ entfernt alles', async () => {
      const { nutzer, router } = renderBelege('/belege?zahlstatus=Offen&quellen=Mail&sortierung=Betrag')
      await tabelleGeladen()
      expect(within(filterleiste()).getByRole('button', { name: /^Zahlstatus: Offen/ })).toBeInTheDocument()
      expect(within(filterleiste()).getByRole('button', { name: /^Herkunft: E-Mail/ })).toBeInTheDocument()
      await nutzer.click(within(filterleiste()).getByRole('button', { name: 'Filter zurücksetzen' }))
      await waitFor(() => expect(router.state.location.search).toEqual({ sortierung: 'Betrag' }))
    })

    it('Pills bleiben beim Wechsel zwischen Tabelle und Karten erhalten', async () => {
      const { nutzer } = renderBelege('/belege?zahlstatus=Offen')
      await tabelleGeladen()
      await nutzer.click(
        within(screen.getByRole('radiogroup', { name: 'Darstellung' })).getByRole('radio', { name: 'Kleine Karten' }),
      )
      await kartenGeladen()
      expect(within(filterleiste()).getByRole('button', { name: /^Zahlstatus: Offen/ })).toBeInTheDocument()
    })
  })

  it('Suche wird entprellt und berücksichtigt den Umschalter „Rechnungsnr.“', async () => {
    const { nutzer, anfragen, letzte } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Suchfeld: Titel & Inhalt' }))
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

  it('Spaltenkopf sortiert erst aufsteigend, der zweite Klick absteigend (leerer Zustand)', async () => {
    const { nutzer, letzte } = renderBelege()
    await tabelleGeladen()
    const kopf = () => screen.getByRole('columnheader', { name: /^Brutto/ })
    const pfeil = () => kopf().querySelector('button[title="Sortieren"] .material-symbols-outlined')!
    expect(screen.getByRole('columnheader', { name: /^Rechnungsdatum/ })).toHaveAttribute('aria-sort', 'descending')
    expect(pfeil()).toHaveAttribute('data-icon', 'arrow_downward')

    await nutzer.click(within(kopf()).getByRole('button', { name: 'Brutto sortieren' }))
    await waitFor(() => expect(letzte().get('sortierung')).toBe('Brutto'))
    expect(letzte().get('absteigend')).toBe('false')
    expect(kopf()).toHaveAttribute('aria-sort', 'ascending')
    expect(pfeil()).toHaveAttribute('data-icon', 'arrow_upward')
    expect(pfeil()).toHaveClass('text-akzent-text')
    await waitFor(() => expect(within(tabellenzeilen()[0]).getByText('118,40 €')).toBeInTheDocument())

    await nutzer.click(within(kopf()).getByRole('button', { name: 'Brutto sortieren, derzeit aufsteigend' }))
    await waitFor(() => expect(letzte().get('absteigend')).toBeNull())
    expect(letzte().get('sortierung')).toBe('Brutto')
    expect(kopf()).toHaveAttribute('aria-sort', 'descending')
    // Absteigend ist der leere Zustand: grauer Pfeil, nur bei Hover sichtbar.
    expect(pfeil()).toHaveAttribute('data-icon', 'arrow_downward')
    expect(pfeil()).toHaveClass('opacity-0')
    await waitFor(() => expect(within(tabellenzeilen()[0]).getByText('534,00 €')).toBeInTheDocument())
  })

  it('jede Standardspalte außer Tags ist sortierbar; Titel aufsteigend, Seiten nach zwei Klicks absteigend', async () => {
    const { nutzer, letzte } = renderBelege()
    await tabelleGeladen()
    const koepfe = screen.getAllByRole('columnheader').slice(1, -1)
    const tags = koepfe.find((kopf) => kopf.textContent === 'Tags')
    expect(tags).not.toHaveAttribute('aria-sort')
    koepfe.filter((kopf) => kopf !== tags).forEach((kopf) => expect(kopf).toHaveAttribute('aria-sort'))

    await nutzer.click(screen.getByRole('button', { name: 'Titel sortieren' }))
    await waitFor(() => expect(letzte().get('sortierung')).toBe('Titel'))
    expect(letzte().get('absteigend')).toBe('false')
    await waitFor(() => expect(within(tabellenzeilen()[0]).getByText('Büromaterial Bestellung')).toBeInTheDocument())

    await nutzer.click(screen.getByRole('button', { name: 'Seiten sortieren' }))
    await waitFor(() => expect(letzte().get('sortierung')).toBe('Seiten'))
    expect(letzte().get('absteigend')).toBe('false')
    await nutzer.click(screen.getByRole('button', { name: 'Seiten sortieren, derzeit aufsteigend' }))
    await waitFor(() => expect(letzte().get('absteigend')).toBeNull())
    await waitFor(() =>
      expect(within(tabellenzeilen()[0]).getByText('Mobilfunk & Internet November')).toBeInTheDocument(),
    )
  })

  it('Korrespondent sortiert beim ersten Klick aufsteigend', async () => {
    const { nutzer, letzte } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Korrespondent sortieren' }))
    await waitFor(() => expect(letzte().get('sortierung')).toBe('Korrespondent'))
    expect(letzte().get('absteigend')).toBe('false')
    expect(screen.getByRole('columnheader', { name: /^Korrespondent/ })).toHaveAttribute('aria-sort', 'ascending')
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
    expect(screen.queryByRole('columnheader', { name: /^Netto/ })).not.toBeInTheDocument()
    await erster.nutzer.click(screen.getByRole('button', { name: /^Spalten/ }))
    const popover = await screen.findByRole('dialog', { name: 'Spalten' })
    expect(within(popover).getByRole('checkbox', { name: 'Korrespondent' })).toBeDisabled()
    await erster.nutzer.click(within(popover).getByRole('checkbox', { name: 'Netto' }))
    expect(screen.getByRole('columnheader', { name: /^Netto/ })).toBeInTheDocument()
    expect(JSON.parse(window.localStorage.getItem(SPALTEN_SPEICHER)!)).toContain('netto')
    erster.unmount()

    renderBelege()
    await tabelleGeladen()
    expect(screen.getByRole('columnheader', { name: /^Netto/ })).toBeInTheDocument()
    expect(within(tabellenzeilen()[0]).getByText('410,92 €')).toBeInTheDocument()
  })

  it('Spaltenreihenfolge per Pfeiltaste', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('button', { name: /^Spalten/ }))
    const popover = await screen.findByRole('dialog', { name: 'Spalten' })
    within(popover).getByRole('button', { name: 'Korrespondent verschieben (Pfeiltasten)' }).focus()
    await nutzer.keyboard('{ArrowUp}')
    const koepfe = screen.getAllByRole('columnheader')
    expect(koepfe[1]).toHaveAccessibleName(/^Korrespondent/)
    expect(koepfe[2]).toHaveAccessibleName(/^Rechnungsnr\./)
  })

  it('Kopf-Kästchen wählt alle Zeilen der Seite, ein Filterwechsel leert die Auswahl', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Alle auswählen' }))
    const leiste = screen.getByRole('toolbar', { name: 'Massenaktionen' })
    expect(leiste).toHaveTextContent('12 Belege ausgewählt')
    expect(screen.queryByRole('search', { name: 'Filter' })).not.toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Alle auswählen' })).toBeChecked()

    void router.navigate({ to: '/belege', search: { ansicht: 'Inbox' } })
    expect(await screen.findByRole('heading', { level: 1, name: 'Inbox' })).toBeInTheDocument()
    expect(screen.queryByRole('toolbar', { name: 'Massenaktionen' })).not.toBeInTheDocument()
  })

  it('Einzelauswahl und „Auswahl aufheben“', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Bürowelt Krämer KG 2026-48817 auswählen' }))
    expect(screen.getByRole('toolbar', { name: 'Massenaktionen' })).toHaveTextContent('1 Beleg ausgewählt')
    await nutzer.click(screen.getByRole('button', { name: 'Auswahl aufheben' }))
    expect(screen.queryByRole('toolbar', { name: 'Massenaktionen' })).not.toBeInTheDocument()
  })

  it('„Bestätigen“ erscheint nur, wenn ein gewählter Beleg „zu prüfen“ ist', async () => {
    const { nutzer } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Autohaus Brenner GmbH R-118342 auswählen' }))
    const leiste = screen.getByRole('toolbar', { name: 'Massenaktionen' })
    expect(within(leiste).queryByRole('button', { name: 'Bestätigen' })).not.toBeInTheDocument()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Bürowelt Krämer KG 2026-48817 auswählen' }))
    expect(within(leiste).getByRole('button', { name: 'Bestätigen' })).toBeInTheDocument()
  })

  it('„Alle 248 auswählen“ wählt alle Belege der Liste, nicht nur die Seite', async () => {
    vieleBelege()
    const { nutzer, anfragen } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(within(tabellenzeilen()[0]).getByRole('checkbox'))
    await nutzer.click(screen.getByRole('button', { name: 'Alle 248 auswählen' }))
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

  it('zeigt eine laufende Skontofrist als Fälligkeit mit ⓘ-Hinweis', async () => {
    renderBelege()
    await tabelleGeladen()
    const mitSkonto = screen.getByRole('row', { name: /HW-2026-11-0381/ })
    expect(within(mitSkonto).getByText('27.11.2026')).toBeInTheDocument()
    expect(within(mitSkonto).queryByText('04.12.2026')).not.toBeInTheDocument()
    expect(
      within(mitSkonto).getByRole('button', {
        name: /^Skonto: 2 % Skonto bis 27\.11\.2026, spart 9,78\s€\. Ohne Skonto fällig am 04\.12\.2026\.$/,
      }),
    ).toBeInTheDocument()
    // Abgelaufenes Skonto (HP-2026-342) und bezahlte Rechnung (7004 1182 3391): normale Fälligkeit, kein Hinweis.
    for (const [nummer, faellig] of [
      [/HP-2026-342/, '24.11.2026'],
      [/7004 1182 3391/, '30.11.2026'],
    ] as const) {
      const zeile = screen.getByRole('row', { name: nummer })
      expect(within(zeile).getByText(faellig)).toBeInTheDocument()
      expect(within(zeile).queryByRole('button', { name: /^Skonto:/ })).not.toBeInTheDocument()
    }
  })

  it('Titel verlinkt ins Detail', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('link', { name: 'Büromaterial Bestellung' }))
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/belege\/[0-9a-f-]+$/))
  })

  it('Klick auf die Zeile öffnet den Beleg – auch ohne Titel-Spalte', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    const zeile = screen.getByRole('row', { name: /EH-4471/ })
    await nutzer.click(within(zeile).getByText('EH-4471'))
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/belege\/[0-9a-f-]+$/))
  })

  it('Klick auf die Auswahl-Checkbox öffnet den Beleg nicht', async () => {
    const { nutzer, router } = renderBelege()
    await tabelleGeladen()
    await nutzer.click(screen.getByRole('checkbox', { name: 'Bürowelt Krämer KG 2026-48817 auswählen' }))
    expect(router.state.location.pathname).toBe('/belege')
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
    // Zoom in Stufen, Prozentanzeige setzt auf „Einpassen“ zurück.
    await nutzer.click(within(dialog).getByRole('button', { name: 'Vergrößern' }))
    expect(within(dialog).getByRole('button', { name: 'Zoom 125 %, einpassen' })).toBeInTheDocument()
    await nutzer.click(within(dialog).getByRole('button', { name: 'Einpassen' }))
    expect(within(dialog).getByRole('button', { name: 'Zoom 100 %, einpassen' })).toBeInTheDocument()
    expect(within(dialog).getByRole('link', { name: 'Beleg öffnen' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/belege\/[0-9a-f-]+$/),
    )
    // Panel neben der Liste statt modal darüber: die Liste bleibt erreichbar.
    expect(screen.getByRole('main')).toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Vorschau' })).not.toBeInTheDocument())
  })

  it('große Karte zeigt ähnliche Belege', async () => {
    const { nutzer } = renderBelege('/belege?darstellung=gross')
    const karte = (await screen.findAllByRole('article', { name: /Hosting & Domains November/ }))[0]
    await nutzer.click(within(karte).getByRole('button', { name: 'Ähnliche' }))
    const popover = await screen.findByRole('dialog', { name: 'Ähnliche Belege' })
    expect(await within(popover).findByRole('link', { name: /Hosting & Domains Oktober/ })).toBeInTheDocument()
  })
})
