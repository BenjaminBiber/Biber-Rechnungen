import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { tagIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'

/** Rendert die echte Belegliste; merkt sich gesendete Tag-Zuordnungen und Bestätigungen. */
function renderBelege(pfad = '/belege') {
  const anfragen: { pfad: string; koerper: unknown }[] = []
  server.use(
    http.post('*/api/belege/:id/:aktion', async ({ request }) => {
      const text = await request.clone().text()
      anfragen.push({ pfad: new URL(request.url).pathname, koerper: text ? JSON.parse(text) : null })
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
  return { router, anfragen, nutzer: userEvent.setup() }
}

const zeilen = () => screen.getAllByRole('row').slice(1)

async function ersteZeile() {
  await screen.findByRole('table', {}, { timeout: 5000 })
  await waitFor(() => expect(zeilen().length).toBeGreaterThan(0))
  const zeile = zeilen()[0]
  const titel = db.belege.find((b) => zeile.textContent?.includes(b.titel))!
  return { zeile, beleg: titel }
}

describe('Zeilenmenü', { timeout: 20_000 }, () => {
  it('öffnet per ⋮-Knopf mit allen Einträgen', async () => {
    const { nutzer } = renderBelege()
    const { zeile } = await ersteZeile()
    await nutzer.click(within(zeile).getByRole('button', { name: /^Aktionen für/ }))
    const menue = await screen.findByRole('menu')
    for (const name of [
      /Öffnen/,
      /In neuem Tab/,
      /Tags …/,
      /Kategorie …/,
      /Als bezahlt markieren/,
      /Erneut auslesen/,
      /Original herunterladen/,
    ])
      expect(within(menue).getByRole('menuitem', { name })).toBeInTheDocument()
  })

  it('öffnet per Rechtsklick auf die Zeile', async () => {
    renderBelege()
    const { zeile } = await ersteZeile()
    fireEvent.contextMenu(within(zeile).getAllByRole('cell')[2])
    expect(await screen.findByRole('menu')).toBeInTheDocument()
  })

  it('Untermenü „Als bezahlt markieren“ setzt den Zahlungs-Tag', async () => {
    const { nutzer, anfragen } = renderBelege()
    const { zeile, beleg } = await ersteZeile()
    await nutzer.click(within(zeile).getByRole('button', { name: /^Aktionen für/ }))
    // Untermenü per Tastatur – das Zeigerverhalten von Radix (Hover-Korridor) bildet jsdom nicht nach.
    screen.getByRole('menuitem', { name: /Als bezahlt markieren/ }).focus()
    await nutzer.keyboard('{ArrowRight}')
    const eintrag = await screen.findByRole('menuitem', { name: 'Lastschrift' })
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'bezahlt' })).toHaveFocus())
    await nutzer.keyboard('{ArrowDown}{ArrowDown}')
    expect(eintrag).toHaveFocus()
    await nutzer.keyboard('{Enter}')
    expect(await screen.findByText('Als „Lastschrift“ markiert')).toBeInTheDocument()
    expect(anfragen).toContainEqual({ pfad: `/api/belege/${beleg.id}/tags`, koerper: { tagId: tagIds.ls } })
  })

  it('„Bestätigen“ nur bei Belegen mit „zu prüfen“', async () => {
    const { nutzer, anfragen } = renderBelege('/belege?ansicht=ZuPruefen')
    const { zeile, beleg } = await ersteZeile()
    await nutzer.click(within(zeile).getByRole('button', { name: /^Aktionen für/ }))
    await nutzer.click(await screen.findByRole('menuitem', { name: /Bestätigen/ }))
    expect(await screen.findByText('Beleg bestätigt')).toBeInTheDocument()
    expect(anfragen.map((a) => a.pfad)).toContain(`/api/belege/${beleg.id}/bestaetigen`)
  })

  it('zeigt „Bestätigen“ nicht ohne „zu prüfen“', async () => {
    const { nutzer } = renderBelege('/belege')
    await screen.findByRole('table', {}, { timeout: 5000 })
    await waitFor(() => expect(zeilen().length).toBeGreaterThan(0))
    const ohne = zeilen().find((z) => !within(z).queryByText('zu prüfen'))!
    await nutzer.click(within(ohne).getByRole('button', { name: /^Aktionen für/ }))
    const menue = await screen.findByRole('menu')
    expect(within(menue).queryByRole('menuitem', { name: /Bestätigen/ })).not.toBeInTheDocument()
  })

  it('„Erneut auslesen“ zeigt den Hinweis', async () => {
    const { nutzer } = renderBelege()
    const { zeile } = await ersteZeile()
    await nutzer.click(within(zeile).getByRole('button', { name: /^Aktionen für/ }))
    await nutzer.click(await screen.findByRole('menuitem', { name: /Erneut auslesen/ }))
    expect(await screen.findByText('Beleg wird erneut ausgelesen')).toBeInTheDocument()
  })

  it('Tastatur: Pfeile, Enter und Esc', async () => {
    const { nutzer, router } = renderBelege()
    const { zeile, beleg } = await ersteZeile()
    const knopf = within(zeile).getByRole('button', { name: /^Aktionen für/ })
    knopf.focus()
    await nutzer.keyboard('{Enter}')
    expect(await screen.findByRole('menu')).toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
    knopf.focus()
    await nutzer.keyboard('{Enter}')
    await screen.findByRole('menu')
    await waitFor(() => expect(screen.getByRole('menuitem', { name: /Öffnen/ })).toHaveFocus())
    await nutzer.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: /In neuem Tab/ })).toHaveFocus()
    await nutzer.keyboard('{ArrowUp}{Enter}')
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${beleg.id}`))
  })

  it('Kürzel T und K öffnen die Auswahl, Enter öffnet, Leertaste wählt aus', async () => {
    const { nutzer, router } = renderBelege()
    const { zeile, beleg } = await ersteZeile()
    zeile.focus()
    await nutzer.keyboard('t')
    expect(await screen.findByPlaceholderText('Tag suchen')).toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByPlaceholderText('Tag suchen')).not.toBeInTheDocument())
    zeile.focus()
    await nutzer.keyboard('k')
    expect(await screen.findByPlaceholderText('Kategorie suchen')).toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByPlaceholderText('Kategorie suchen')).not.toBeInTheDocument())
    zeile.focus()
    await nutzer.keyboard(' ')
    expect(within(zeile).getByRole('checkbox')).toBeChecked()
    zeile.focus()
    await nutzer.keyboard('{Enter}')
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${beleg.id}`))
  })

  it('Kürzel wirken nicht während der Eingabe im Suchfeld', async () => {
    const { nutzer } = renderBelege()
    await ersteZeile()
    const suchfeld = screen.getAllByRole('searchbox')[0] ?? screen.getAllByRole('textbox')[0]
    await nutzer.click(suchfeld)
    await nutzer.keyboard('tk')
    expect(screen.queryByPlaceholderText('Tag suchen')).not.toBeInTheDocument()
    expect(screen.queryByPlaceholderText('Kategorie suchen')).not.toBeInTheDocument()
  })

  it('NurLesen sieht keine ändernden Einträge', async () => {
    szenario.rolle('NurLesen')
    const { nutzer } = renderBelege()
    const { zeile } = await ersteZeile()
    await nutzer.click(within(zeile).getByRole('button', { name: /^Aktionen für/ }))
    const menue = await screen.findByRole('menu')
    expect(within(menue).queryByRole('menuitem', { name: /Tags …/ })).not.toBeInTheDocument()
    expect(within(menue).getByRole('menuitem', { name: /Öffnen/ })).toBeInTheDocument()
  })
})
