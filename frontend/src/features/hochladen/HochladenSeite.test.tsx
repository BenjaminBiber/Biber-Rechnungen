import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { nodeFormDataVerwenden } from '@/api/testHilfe'
import { importVorgaenge } from '@/mocks/fixtures/import'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { renderMitApp } from '@/test/render'
import { HochladenSeite } from './HochladenSeite'
import { UploadListe } from './UploadListe'
import { warteschlange, type UploadEintrag } from './uploadWarteschlange'

afterEach(() => warteschlange.zuruecksetzen())

/** Merkt sich alle Anfragen an `pfad` (Methode + Query). */
function mitschneiden(pfad: string) {
  const anfragen: { methode: string; suche: URLSearchParams }[] = []
  const hoerer = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (url.pathname === pfad) anfragen.push({ methode: request.method, suche: url.searchParams })
  }
  server.events.on('request:start', hoerer)
  return { anfragen, ende: () => server.events.removeListener('request:start', hoerer) }
}

const pdf = (name: string) => new File(['%PDF-1.4 Test'], name, { type: 'application/pdf' })

describe('Hochladen', () => {
  it('startet zwei Uploads mit Fortschritt; sie enden als „Fertig“ mit Link zum Beleg', async () => {
    await nodeFormDataVerwenden()
    const { anfragen, ende } = mitschneiden('/api/import/upload')
    renderMitApp(<HochladenSeite />)
    const eingabe = await screen.findByLabelText('Dateien auswählen')
    await userEvent.upload(eingabe, [pdf('Rechnung_A.pdf'), pdf('Rechnung_B.pdf')])

    expect(screen.getAllByTestId('upload-eintrag')).toHaveLength(2)
    await waitFor(() => expect(anfragen.filter((a) => a.methode === 'POST')).toHaveLength(2))
    // Der Mock lässt den Vorgang bei jedem Abruf einen Schritt weiterlaufen; die Zwischenstände prüft „Upload-Liste“.

    const links = await screen.findAllByRole('link', { name: 'Beleg öffnen' }, { timeout: 15000 })
    expect(links).toHaveLength(2)
    expect(links[0].getAttribute('href')).toMatch(/^\/belege\//)
    expect(screen.getAllByText('Fertig')).toHaveLength(2)
    ende()
  }, 30000)

  it('lehnt .heic und zu große Dateien ohne Anfrage ab', async () => {
    const { anfragen, ende } = mitschneiden('/api/import/upload')
    renderMitApp(<HochladenSeite />)
    const eingabe = await screen.findByLabelText('Dateien auswählen')
    const gross = pdf('riesig.pdf')
    Object.defineProperty(gross, 'size', { value: 60 * 1024 * 1024 })
    await userEvent.upload(eingabe, [new File(['x'], 'foto_beleg.heic'), gross], { applyAccept: false })

    const liste = within(await screen.findByRole('region', { name: 'Hochgeladene Dateien' }))
    expect(liste.getByText('Format nicht unterstützt. Erlaubt sind PDF, XML, PNG, JPG und TIFF.')).toBeInTheDocument()
    expect(liste.getByText('Datei ist größer als 50 MB.')).toBeInTheDocument()
    expect(liste.getAllByText('Abgelehnt')).toHaveLength(2)
    expect(anfragen).toHaveLength(0)

    await userEvent.click(screen.getByRole('button', { name: 'foto_beleg.heic entfernen' }))
    expect(screen.getAllByTestId('upload-eintrag')).toHaveLength(1)
    ende()
  })

  it('zeigt NurLesen statt der Dropzone einen Hinweis, die Tabelle bleibt', async () => {
    szenario.rolle('NurLesen')
    renderMitApp(<HochladenSeite />)
    expect(await screen.findByText('Mit deiner Rolle kannst du keine Belege hochladen.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Dateien auswählen')).not.toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Import-Vorgänge' })).toBeInTheDocument()
  })
})

describe('Upload-Liste', () => {
  const basis = { groesse: 371 * 1024, prozent: 100 }
  const renderListe = (eintraege: UploadEintrag[]) =>
    renderMitApp(<UploadListe eintraege={eintraege} onEntfernen={() => {}} onErneut={() => {}} />)

  it('zeigt Fortschritt, Schritt-Chips, XML-Hinweis, Dublette mit Link und „Erneut“', async () => {
    renderListe([
      { ...basis, id: '1', name: 'Rechnung_Okt_Hosting.pdf', status: 'laedt', prozent: 64 },
      { ...basis, id: '2', name: 'xrechnung_2026_1194.xml', status: 'verarbeitung', vorgang: importVorgaenge[1] },
      {
        ...basis,
        id: '3',
        name: 'rechnung_weller_kopie.pdf',
        status: 'dublette',
        vorgang: {
          ...importVorgaenge[0],
          status: 'Dublette',
          ergebnis: 'Identische Datei (SHA-256) wie Beleg 26-0912',
          duplikatVonBelegId: 'b0000000-0000-4000-8000-000000000001',
        },
      },
      {
        ...basis,
        id: '4',
        name: 'netz.pdf',
        status: 'fehler',
        meldung: 'Netzwerkfehler beim Hochladen.',
        wiederholbar: true,
      },
    ])
    expect(await screen.findByText('Hochladen 64 %')).toBeInTheDocument()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '64')

    const schritte = within(screen.getByRole('list', { name: 'Verarbeitungsschritte' })).getAllByRole('listitem')
    expect(schritte.map((s) => s.dataset.status)).toEqual([
      'ok',
      'ok',
      'laeuft',
      'offen',
      'offen',
      'offen',
      'offen',
      'offen',
    ])
    expect(screen.getByText(/E-Rechnung erkannt: XML ist maßgeblich/)).toBeInTheDocument()

    expect(screen.getByText(/Identische Datei \(SHA-256\)/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Original ansehen' })).toHaveAttribute(
      'href',
      '/belege/b0000000-0000-4000-8000-000000000001',
    )
    expect(screen.getByRole('button', { name: 'Erneut' })).toBeInTheDocument()
  })
})

describe('Import-Vorgänge', () => {
  it('zeigt die sieben Vorgänge mit Chips und Quellen-Icons', async () => {
    renderMitApp(<HochladenSeite />)
    await userEvent.click(await screen.findByRole('button', { name: 'Letzte 30 Tage' }))
    const tabelle = await screen.findByRole('table')
    await waitFor(() => expect(within(tabelle).getAllByRole('row')).toHaveLength(8))
    const text = tabelle.textContent ?? ''
    for (const chip of ['läuft', 'fertig', 'ohne Beleg', 'wartet', 'abgelehnt'])
      expect(within(tabelle).getAllByText(chip, { exact: true }).length).toBeGreaterThan(0)
    for (const icon of ['upload', 'mail', 'folder_open', 'smart_toy']) expect(text).toContain(icon)
    expect(text).toContain('3 / 5')
    expect(text).toMatch(/nächster Versuch \d\d:\d\d/)
    expect(within(tabelle).getByRole('link', { name: '1 Beleg angelegt' })).toHaveAttribute(
      'href',
      `/belege/${importVorgaenge[6].belegId}`,
    )
  })

  it('setzt Quelle und Zeitraum als Query-Parameter', async () => {
    const { anfragen, ende } = mitschneiden('/api/import/vorgaenge')
    renderMitApp(<HochladenSeite />)
    await waitFor(() => expect(anfragen.at(-1)?.suche.get('tage')).toBe('7'))
    expect(anfragen.at(-1)?.suche.has('quelle')).toBe(false)

    await userEvent.click(screen.getByRole('button', { name: 'E-Mail' }))
    await waitFor(() => expect(anfragen.at(-1)?.suche.get('quelle')).toBe('Mail'))
    await userEvent.click(screen.getByRole('button', { name: 'Heute' }))
    await waitFor(() => expect(anfragen.at(-1)?.suche.get('tage')).toBe('1'))
    expect(anfragen.at(-1)?.suche.get('quelle')).toBe('Mail')
    expect(screen.getByRole('button', { name: 'Heute' })).toHaveAttribute('aria-pressed', 'true')
    ende()
  })
})
