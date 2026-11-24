import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { stammartikelIds } from '@/mocks/fixtures/ids'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { useBefehlspalette } from './befehlspalette-store'

// cmdk misst die Liste und scrollt die Auswahl ins Bild – beides kennt jsdom nicht.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
}
Element.prototype.scrollIntoView ??= () => {}

function renderApp(pfad = '/tags') {
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
  return { router, nutzer: userEvent.setup() }
}

const palette = () => screen.queryByRole('dialog', { name: 'Befehlspalette' })
const eingabe = () => within(palette()!).getByRole('combobox')
const gruppe = (name: string) => within(palette()!).queryByRole('group', { name })
const ausgewaehlt = () =>
  within(palette()!)
    .getAllByRole('option')
    .find((o) => o.dataset.selected === 'true')

async function bereit() {
  await screen.findByRole('heading', { level: 1 })
}

async function oeffnen(nutzer: ReturnType<typeof userEvent.setup>) {
  await nutzer.keyboard('{Control>}k{/Control}')
  await waitFor(() => expect(palette()).toBeInTheDocument())
}

beforeEach(() => paletteSchliessen())
afterEach(() => vi.useRealTimers())

/** Der Store lebt über Tests hinweg; vor jedem Test geschlossen. */
function paletteSchliessen() {
  function Greifer() {
    const { offen, schliessen } = useBefehlspalette()
    if (offen) schliessen()
    return null
  }
  render(<Greifer />).unmount()
}

describe('Befehlspalette', () => {
  it('Strg K öffnet und schließt; ohne Eingabe nur Befehle', async () => {
    const { nutzer } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    expect(gruppe('Befehle')).toBeInTheDocument()
    expect(gruppe('Belege')).not.toBeInTheDocument()
    expect(within(palette()!).getByRole('option', { name: /Beleg hochladen/ })).toBeInTheDocument()
    expect(within(palette()!).getByRole('option', { name: /Zu prüfen öffnen/ })).toHaveTextContent('G P')

    await nutzer.keyboard('{Control>}k{/Control}')
    await waitFor(() => expect(palette()).not.toBeInTheDocument())
  })

  it('„toner“ zeigt Belege und den Stammartikel mit Chip; Enter öffnet den Beleg', async () => {
    const { nutzer, router } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    await nutzer.type(eingabe(), 'toner')

    await waitFor(() => expect(gruppe('Belege')).toBeInTheDocument())
    const belege = within(gruppe('Belege')!).getAllByRole('option')
    expect(belege).toHaveLength(2)
    expect(belege[0]).toHaveTextContent(/Bürowelt Krämer KG · 2026-48817/)
    expect(belege[0]).toHaveTextContent(/264,00\s€/)
    const stamm = within(gruppe('Artikel')!).getByRole('option', { name: /^Toner schwarz/ })
    expect(stamm).toHaveTextContent('Stammartikel · 2 Händler')
    // Befehle sind nach der Eingabe gefiltert.
    expect(gruppe('Befehle')).not.toBeInTheDocument()

    await waitFor(() => expect(ausgewaehlt()).toBe(belege[0]))
    await nutzer.keyboard('{ArrowDown}')
    expect(ausgewaehlt()).toBe(within(gruppe('Belege')!).getAllByRole('option')[1])
    await nutzer.keyboard('{ArrowUp}{Enter}')

    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/belege\/[^/]+$/))
    expect(palette()).not.toBeInTheDocument()
    await oeffnen(nutzer)
    expect(eingabe()).toHaveValue('')
  })

  it('Stammartikel führt zur Stammartikel-Seite', async () => {
    const { nutzer, router } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    await nutzer.type(eingabe(), 'toner')
    await nutzer.click(await within(palette()!).findByRole('option', { name: /^Toner schwarz/ }))
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/artikel/stammartikel/${stammartikelIds.tonerSchwarz}`),
    )
  })

  it('ohne Treffer: Hinweis und „In Belegen suchen“', async () => {
    const { nutzer, router } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    await nutzer.type(eingabe(), 'xyzzy')
    expect(await within(palette()!).findByText('Nichts gefunden für ‚xyzzy‘')).toBeInTheDocument()
    await nutzer.click(within(palette()!).getByRole('option', { name: 'In Belegen suchen: ‚xyzzy‘' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/belege'))
    expect(router.state.location.search).toMatchObject({ suche: 'xyzzy' })
  })

  it('Esc schließt', async () => {
    const { nutzer } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(palette()).not.toBeInTheDocument())
  })

  it('Kürzel U navigiert, aber nicht aus einem Eingabefeld', async () => {
    const { router } = renderApp('/korrespondenten')
    await bereit()
    const feld = await screen.findByRole('searchbox')
    fireEvent.keyDown(feld, { key: 'u' })
    expect(router.state.location.pathname).toBe('/korrespondenten')

    fireEvent.keyDown(document.body, { key: 'u' })
    await waitFor(() => expect(router.state.location.pathname).toBe('/hochladen'))
  })

  it('G dann P innerhalb 1 s navigiert, nach 1,5 s nicht', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const { router } = renderApp()
    await bereit()

    fireEvent.keyDown(document.body, { key: 'g' })
    vi.setSystemTime(Date.now() + 1500)
    fireEvent.keyDown(document.body, { key: 'p' })
    expect(router.state.location.pathname).toBe('/tags')

    fireEvent.keyDown(document.body, { key: 'g' })
    vi.setSystemTime(Date.now() + 800)
    fireEvent.keyDown(document.body, { key: 'p' })
    await waitFor(() => expect(router.state.location.pathname).toBe('/belege'))
    expect(router.state.location.search).toMatchObject({ ansicht: 'ZuPruefen' })
  })

  it('NurLesen: kein „Beleg hochladen“, Kürzel U wirkt nicht', async () => {
    szenario.rolle('NurLesen')
    const { nutzer, router } = renderApp()
    await bereit()
    await oeffnen(nutzer)
    expect(within(palette()!).getByRole('option', { name: /Zu prüfen öffnen/ })).toBeInTheDocument()
    expect(within(palette()!).queryByRole('option', { name: /Beleg hochladen/ })).not.toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(palette()).not.toBeInTheDocument())
    fireEvent.keyDown(document.body, { key: 'u' })
    expect(router.state.location.pathname).toBe('/tags')
  })
})
