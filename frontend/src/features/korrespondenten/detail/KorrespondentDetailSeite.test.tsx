import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { korrespondentIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { dateiFehler, domainAus } from './LogoDialog'

const ID = korrespondentIds.bueroweltKraemer
const SEITE = `/korrespondenten/${ID}?jahr=2026`

type Anfrage = { methode: string; pfad: string; koerper?: unknown }

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen. */
function renderSeite(pfad = SEITE) {
  const anfragen: Anfrage[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    if (request.method === 'GET') return
    const eintrag: Anfrage = { methode: request.method, pfad: new URL(request.url).pathname }
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

const titel = (name = 'Bürowelt Krämer KG') => screen.findByRole('heading', { level: 1, name }, { timeout: 5000 })

describe('Korrespondent-Detail', { timeout: 20000 }, () => {
  it('zeigt das VIES-Ergebnis der USt-IdNr. und prüft auf Knopfdruck neu', async () => {
    db.korrespondenten = db.korrespondenten.map((k) =>
      k.id === ID ? { ...k, ustIdNr: 'DE111222000', ustIdPruefung: { ...k.ustIdPruefung, status: 'Gueltig' } } : k,
    )
    const { anfragen, ende } = renderSeite()
    await titel()
    const stammdaten = screen.getByRole('region', { name: 'Stammdaten' })
    expect(within(stammdaten).getByText('gültig laut VIES')).toBeInTheDocument()

    await userEvent.click(within(stammdaten).getByRole('button', { name: 'Jetzt prüfen' }))

    expect(await within(stammdaten).findByText('ungültig laut VIES')).toBeInTheDocument()
    expect(anfragen.map((a) => a.pfad)).toContain(`/api/korrespondenten/${ID}/ustid-pruefen`)
    ende()
  })

  it('zeigt Kennzahlen und Stammdaten', async () => {
    const { ende } = renderSeite()
    await titel()
    const kennzahlen = within(screen.getByRole('group', { name: 'Kennzahlen' }))
    expect(kennzahlen.getByText('Kosten 2026')).toBeInTheDocument()
    expect(kennzahlen.getByText(/^2\.223,00\s€$/)).toBeInTheDocument()
    expect(kennzahlen.getByText('+14 % ggü. 2025')).toBeInTheDocument()
    expect(kennzahlen.getByText('31')).toBeInTheDocument()
    expect(kennzahlen.getByText('seit 03/2024')).toBeInTheDocument()
    expect(kennzahlen.getByText(/^202,09\s€$/)).toBeInTheDocument()
    expect(screen.getByText('DE12 6805 0101 0012 3456 78')).toBeInTheDocument()
    expect(screen.getByText('DE89 3704 0044 0532 0130 00')).toBeInTheDocument()
    expect(screen.getByText('Sparkasse Freiburg-Nördlicher Breisgau', { exact: false })).toHaveTextContent(
      'Sparkasse Freiburg-Nördlicher Breisgau · BIC FRSPDE66XXX',
    )
    expect(screen.getByText('Commerzbank')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'buerowelt-kraemer.de' })).toHaveAttribute(
      'href',
      'https://buerowelt-kraemer.de',
    )
    expect(screen.getByText('Ausschnitt aus Seite 1 (Upload › PDF › Favicon)')).toBeInTheDocument()
    expect(screen.getByText(/^Auto.* · trainiert 23\.11\.2026/)).toBeInTheDocument()
    ende()
  })

  it('kopiert die IBAN ohne Leerzeichen', async () => {
    const { ende } = renderSeite()
    await titel()
    const nutzer = userEvent.setup()
    await nutzer.click(screen.getByRole('button', { name: 'IBAN DE12 6805 0101 0012 3456 78 kopieren' }))
    expect(await screen.findByText('IBAN kopiert')).toBeInTheDocument()
    expect(await navigator.clipboard.readText()).toBe('DE12680501010012345678')
    ende()
  })

  it('hat zwölf Monate im Kostenverlauf und verlinkt Tabellen', async () => {
    const { ende } = renderSeite()
    await titel()
    const tabelle = screen.getByRole('table', { name: 'Kostenverlauf 2026' })
    expect(within(tabelle).getAllByRole('row')).toHaveLength(13)
    expect(within(tabelle).getByRole('rowheader', { name: 'Jan' }).nextSibling).toHaveTextContent('312,00')
    expect(screen.getByRole('link', { name: /^Alle 31 in der Belegliste$/ })).toHaveAttribute(
      'href',
      expect.stringContaining(`/belege?korrespondenten=`),
    )
    expect(screen.getByRole('link', { name: /^Alle \d+ Artikel$/ })).toHaveAttribute(
      'href',
      `/artikel?korrespondentId=${ID}`,
    )
    const belege = screen.getByRole('table', { name: 'Belege' })
    expect(within(belege).getAllByRole('link')[0]).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/belege\/[0-9a-f-]+$/),
    )
    const artikel = screen.getByRole('table', { name: 'Artikel dieses Korrespondenten' })
    expect(within(artikel).getAllByRole('link')[0]).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/artikel\/[0-9a-f-]+$/),
    )
    expect(screen.getByRole('link', { name: /Zusammenführen/ })).toHaveAttribute(
      'href',
      `/korrespondenten?zusammenfuehren=${ID}`,
    )
    ende()
  })

  it('fügt Aliase hinzu und entfernt sie', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel()
    await user.click(screen.getByRole('button', { name: '+ Alias' }))
    await user.type(screen.getByRole('textbox', { name: 'Neuer Alias' }), 'Krämer Büro{Enter}')
    await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
    expect(anfragen.at(-1)).toMatchObject({
      methode: 'PUT',
      pfad: `/api/korrespondenten/${ID}`,
      koerper: { aliase: ['Bürowelt Krämer', 'Buerowelt Kraemer KG', 'Krämer Büro'] },
    })
    await user.click(await screen.findByRole('button', { name: 'Bürowelt Krämer entfernen' }))
    await waitFor(() =>
      expect(anfragen.at(-1)?.koerper).toMatchObject({ aliase: ['Buerowelt Kraemer KG', 'Krämer Büro'] }),
    )
    ende()
  })

  it('speichert das Matching aus dem Dialog', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel()
    await user.click(screen.getByRole('button', { name: 'Matching bearbeiten' }))
    const dialog = await screen.findByRole('dialog', { name: 'Matching bearbeiten' })
    await user.selectOptions(within(dialog).getAllByRole('combobox')[0], 'Keiner')
    await user.click(within(dialog).getByRole('button', { name: 'Speichern' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() => expect(anfragen.at(-1)?.koerper).toMatchObject({ matching: { algorithmus: 'Keiner' } }))
    ende()
  })

  it('Logo-Dialog: Ausschnitt sendet Anteile, Reiter wechseln, Upload und Favicon', async () => {
    const user = userEvent.setup()
    const { anfragen, ende } = renderSeite()
    await titel()
    await user.click(screen.getByRole('button', { name: 'Logo ändern' }))
    const dialog = await screen.findByRole('dialog', { name: 'Logo festlegen' })
    const rechteck = within(dialog).getByRole('slider', { name: 'Auswahlrechteck' })
    rechteck.focus()
    await user.keyboard('{ArrowRight}{ArrowDown}{Shift>}{ArrowRight}{/Shift}')
    await user.click(within(dialog).getByRole('button', { name: 'Als Logo speichern' }))
    await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
    const letzte = anfragen.at(-1)!
    expect(letzte.pfad).toMatch(/^\/api\/belege\/[0-9a-f-]+\/logo-uebernehmen$/)
    const k = letzte.koerper as Record<string, number>
    expect(k).toMatchObject({ seite: 1, x: 0.06, y: 0.04, breite: 0.31, hoehe: 0.12 })
    for (const v of [k.x, k.y, k.breite, k.hoehe]) expect(v >= 0 && v <= 1).toBe(true)
    await screen.findByText('Logo aus dem Beleg übernommen')

    await user.click(screen.getByRole('button', { name: 'Logo ändern' }))
    const neu = await screen.findByRole('dialog', { name: 'Logo festlegen' })
    await user.click(within(neu).getByRole('tab', { name: 'Datei hochladen' }))
    const eingabe = within(neu).getByLabelText(/Logo-Datei/)
    const gross = new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'gross.png', { type: 'image/png' })
    fireEvent.change(eingabe, { target: { files: [gross] } })
    expect(within(neu).getByRole('alert')).toHaveTextContent('Die Datei ist größer als 2 MB.')
    expect(within(neu).getByRole('button', { name: 'Logo speichern' })).toBeDisabled()

    await user.click(within(neu).getByRole('tab', { name: 'Favicon' }))
    expect(within(neu).getByRole('button', { name: 'Favicon von buerowelt-kraemer.de abrufen' })).toBeEnabled()
    expect(within(neu).getByRole('button', { name: 'Logo entfernen' })).toBeInTheDocument()
    ende()
  })

  it('deaktiviert den Favicon-Abruf ohne Website', async () => {
    db.korrespondenten = db.korrespondenten.map((k) => (k.id === ID ? { ...k, website: null } : k))
    const user = userEvent.setup()
    const { ende } = renderSeite()
    await titel()
    await user.click(screen.getByRole('button', { name: 'Logo ändern' }))
    const dialog = await screen.findByRole('dialog', { name: 'Logo festlegen' })
    await user.click(within(dialog).getByRole('tab', { name: 'Favicon' }))
    expect(within(dialog).getByRole('button', { name: 'Favicon abrufen' })).toBeDisabled()
    expect(within(dialog).getByText(/Ohne Website lässt sich kein Favicon abrufen/)).toBeInTheDocument()
    ende()
  })

  it('öffnet mit `logoAus` den Ausschnitt-Reiter mit diesem Beleg', async () => {
    const beleg = db.belege.find((b) => b.korrespondent?.id === ID)!
    const { ende } = renderSeite(`/korrespondenten/${ID}?jahr=2026&logoAus=${beleg.id}`)
    const dialog = await screen.findByRole('dialog', { name: 'Logo festlegen' }, { timeout: 5000 })
    expect(within(dialog).getByRole('tab', { name: 'Aus Beleg ausschneiden' })).toHaveAttribute('aria-selected', 'true')
    expect(within(dialog).getByRole('combobox', { name: 'Beleg' })).toHaveValue(beleg.id)
    expect(within(dialog).getByRole('img', { name: 'Seite 1 des Belegs' })).toHaveAttribute(
      'src',
      expect.stringContaining(`/api/belege/${beleg.id}/vorschau?seite=1&breite=1200`),
    )
    ende()
  })

  it('zeigt NurLesen keine Bearbeiten-Elemente', async () => {
    szenario.rolle('NurLesen')
    const { ende } = renderSeite()
    await titel()
    for (const name of ['Logo ändern', 'Bearbeiten', '+ Alias', 'Matching bearbeiten', 'Bürowelt Krämer entfernen'])
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Zusammenführen/ })).not.toBeInTheDocument()
    ende()
  })

  it('meldet einen unbekannten Korrespondenten', async () => {
    const { ende } = renderSeite('/korrespondenten/00000000-0000-0000-0000-000000000000')
    await titel('Korrespondent nicht gefunden')
    ende()
  })
})

describe('Logo-Hilfen', () => {
  it('prüft Dateien und liest die Domain', () => {
    expect(dateiFehler(new File(['x'], 'a.gif', { type: 'image/gif' }))).toBe('Nur PNG, JPG oder WebP.')
    expect(dateiFehler(new File(['x'], 'a.webp', { type: 'image/webp' }))).toBeNull()
    expect(domainAus('https://www.example.de/kontakt')).toBe('www.example.de')
    expect(domainAus('  ')).toBeNull()
  })
})
