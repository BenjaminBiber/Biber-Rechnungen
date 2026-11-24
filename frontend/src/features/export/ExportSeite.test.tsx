import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import type { ExportAnfrage } from '@/api/typen'
import { ToastBereich } from '@/components/toast'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { renderMitApp } from '@/test/render'
import { ExportSeite, type ExportVorbelegung } from './ExportSeite'

const heute = new Date(2026, 10, 23)
const q3: ExportVorbelegung = { von: '2026-07-01', bis: '2026-09-30', kategorien: [], korrespondenten: [], tags: [] }

/** Merkt sich die Körper aller POST-Anfragen an `pfad`. */
function mitschneiden(pfad: string) {
  const koerper: ExportAnfrage[] = []
  const hoerer = ({ request }: { request: Request }) => {
    if (request.method === 'POST' && new URL(request.url).pathname === pfad)
      void request
        .clone()
        .json()
        .then((k: ExportAnfrage) => koerper.push(k))
  }
  server.events.on('request:start', hoerer)
  return { koerper, ende: () => server.events.removeListener('request:start', hoerer) }
}

function rendern(vorbelegung: ExportVorbelegung = q3) {
  return renderMitApp(
    <>
      <ExportSeite vorbelegung={vorbelegung} heute={heute} />
      <ToastBereich />
    </>,
  )
}

const vorschau = () => within(screen.getByRole('complementary'))

describe('Export', () => {
  it('Schnellwahl „Q3 2026“ setzt den Zeitraum und zeigt die Vorschau-Zahlen', async () => {
    rendern({ ...q3, von: '2026-11-01', bis: '2026-11-30' })
    await userEvent.click(await screen.findByRole('button', { name: 'Q3 2026' }))
    expect(screen.getByLabelText('Von')).toHaveValue('01.07.2026')
    expect(screen.getByLabelText('Bis')).toHaveValue('30.09.2026')
    expect(screen.getByRole('button', { name: 'Q3 2026' })).toHaveAttribute('aria-pressed', 'true')
    expect(await screen.findByRole('heading', { name: 'Vorschau · Q3 2026' })).toBeInTheDocument()
    await waitFor(() => expect(vorschau().getByText('61')).toBeInTheDocument())
    expect(vorschau().getByText('6.482,00 €')).toBeInTheDocument()
    expect(vorschau().getByText('1.012,37 €')).toBeInTheDocument()
    expect(vorschau().getByText('Dubletten ausgeschlossen').nextSibling).toHaveTextContent('2')
    expect(vorschau().getAllByRole('row')).toHaveLength(7)
    expect(vorschau().queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText(/mit Prüfsummenliste · ca\. 24 MB/)).toBeInTheDocument()
  })

  it('„November“ zeigt die Warnung mit fünf Belegen und Link auf die Belegliste', async () => {
    rendern()
    await userEvent.click(await screen.findByRole('button', { name: 'November' }))
    const warnung = await screen.findByRole('alert')
    expect(warnung).toHaveTextContent(
      '5 Belege im Zeitraum sind „zu prüfen“. Diese sind noch nicht bestätigt. Jetzt prüfen',
    )
    const link = within(warnung).getByRole('link', { name: 'Jetzt prüfen' })
    expect(link.getAttribute('href')).toBe('/belege?ansicht=ZuPruefen&von=2026-11-01&bis=2026-11-30')
  })

  it('jede Änderung an Inhalt oder Filter löst entprellt eine neue Vorschau-Anfrage aus', async () => {
    const { koerper, ende } = mitschneiden('/api/export/vorschau')
    rendern()
    await waitFor(() => expect(koerper).toHaveLength(1))
    expect(koerper[0]).toEqual({
      von: '2026-07-01',
      bis: '2026-09-30',
      bezeichnung: 'Q3 2026',
      belegliste: true,
      beleglisteFormat: 'xlsx',
      summenJeKategorie: true,
      vorsteuer: true,
      zipOriginale: false,
      kategorieIds: [],
      korrespondentIds: [],
      tagIds: [],
      dublettenAusschliessen: true,
      zuPruefenAusschliessen: false,
    })

    await userEvent.click(screen.getByLabelText('ZIP der Originale'))
    await userEvent.selectOptions(screen.getByLabelText('Format der Belegliste'), 'csv')
    await userEvent.click(screen.getByLabelText('Belege mit „zu prüfen“ ausschließen'))
    await waitFor(() => expect(koerper).toHaveLength(2))
    expect(koerper[1]).toMatchObject({ zipOriginale: true, beleglisteFormat: 'csv', zuPruefenAusschliessen: true })

    await userEvent.click(screen.getByRole('button', { name: /Kategorien/ }))
    await userEvent.click(await screen.findByLabelText(db.kategorien[0].name))
    await waitFor(() => expect(koerper).toHaveLength(3))
    expect(koerper[2].kategorieIds).toEqual([db.kategorien[0].id])
    ende()
  })

  it('blendet die Vorsteuer-Option bei Kleinunternehmer aus', async () => {
    db.einstellungen = { ...db.einstellungen, ust: { ...db.einstellungen.ust, status: 'Kleinunternehmer' } }
    rendern()
    await screen.findByLabelText('Belegliste')
    await waitFor(() => expect(screen.queryByLabelText('Vorsteuer je UStVA-Zeitraum')).not.toBeInTheDocument())
  })

  it('deaktiviert „Export erstellen“ ohne gewählten Inhalt', async () => {
    rendern()
    const knopf = await screen.findByRole('button', { name: 'Export erstellen' })
    await waitFor(() => expect(knopf).toBeEnabled())
    for (const option of ['Belegliste', 'Summen je Kategorie', 'Vorsteuer je UStVA-Zeitraum'])
      await userEvent.click(screen.getByLabelText(option))
    expect(knopf).toBeDisabled()
  })

  it('meldet ungültige Daten und „Bis“ vor „Von“', async () => {
    rendern()
    const von = await screen.findByLabelText('Von')
    await userEvent.clear(von)
    await userEvent.type(von, '31.13.2026')
    expect(screen.getByText('Datum im Format TT.MM.JJJJ eingeben.')).toBeInTheDocument()
    await userEvent.clear(von)
    await userEvent.type(von, '01.10.2026')
    expect(screen.getByText('„Bis“ liegt vor „Von“.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Export erstellen' })).toBeDisabled()
  })

  it('Erstellen sendet die vollständige Anfrage, startet den Download und ergänzt die Tabelle', async () => {
    const klick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { koerper, ende } = mitschneiden('/api/export')
    rendern()
    const tabelle = within(await screen.findByRole('region', { name: 'Letzte Exporte' }))
    await waitFor(() => expect(tabelle.getAllByRole('row')).toHaveLength(4))

    const knopf = screen.getByRole('button', { name: 'Export erstellen' })
    await waitFor(() => expect(knopf).toBeEnabled())
    await userEvent.click(knopf)
    expect(await screen.findByText('Export Q3 2026 wird erstellt …')).toBeInTheDocument()
    await waitFor(() => expect(klick).toHaveBeenCalled())
    expect(koerper[0]).toEqual({
      von: '2026-07-01',
      bis: '2026-09-30',
      bezeichnung: 'Q3 2026',
      belegliste: true,
      beleglisteFormat: 'xlsx',
      summenJeKategorie: true,
      vorsteuer: true,
      zipOriginale: false,
      kategorieIds: [],
      korrespondentIds: [],
      tagIds: [],
      dublettenAusschliessen: true,
      zuPruefenAusschliessen: false,
    })
    await waitFor(() => expect(tabelle.getAllByRole('row')).toHaveLength(5))
    expect(tabelle.getAllByRole('row')[1]).toHaveTextContent('Belegliste (xlsx), Summen, Vorsteuer')
    ende()
  })

  it('zeigt einen Fehler beim Erstellen als stehenden Toast', async () => {
    szenario.fehler('/api/export', 500)
    rendern()
    const knopf = await screen.findByRole('button', { name: 'Export erstellen' })
    await waitFor(() => expect(knopf).toBeEnabled())
    await userEvent.click(knopf)
    expect(await screen.findByText('Export Q3 2026 konnte nicht erstellt werden.')).toBeInTheDocument()
  })

  it('nennt bei 409 die veränderten Originale', async () => {
    server.use(
      http.post('*/api/export', () =>
        HttpResponse.json(
          { title: 'Originale verändert', status: 409, pfade: ['2026/07/a.pdf', '2026/08/b.pdf'] },
          { status: 409 },
        ),
      ),
    )
    rendern()
    const knopf = await screen.findByRole('button', { name: 'Export erstellen' })
    await waitFor(() => expect(knopf).toBeEnabled())
    await userEvent.click(knopf)
    expect(
      await screen.findByText(/Originale seit dem Import verändert – 2026\/07\/a\.pdf, 2026\/08\/b\.pdf/),
    ).toBeInTheDocument()
  })

  it('meldet bei 410 im Download, dass die Exportdatei fehlt', async () => {
    server.use(
      http.get('*/api/export/laeufe/:id/datei', () =>
        HttpResponse.json({ title: 'Exportdatei fehlt', status: 410 }, { status: 410 }),
      ),
    )
    rendern()
    const tabelle = within(await screen.findByRole('region', { name: 'Letzte Exporte' }))
    const [link] = await tabelle.findAllByRole('link', { name: /herunterladen/ })
    await userEvent.click(link)
    expect(await screen.findByText(/fehlt auf dem Server/)).toBeInTheDocument()
  })

  it('übernimmt die Vorbelegung der Filter', async () => {
    const kategorie = db.kategorien[1]
    rendern({ ...q3, kategorien: [kategorie.id] })
    expect(await screen.findByRole('button', { name: new RegExp(kategorie.name) })).toBeInTheDocument()
  })

  it('NurLesen sieht nur die letzten Exporte mit Download', async () => {
    szenario.rolle('NurLesen')
    rendern()
    const tabelle = within(await screen.findByRole('region', { name: 'Letzte Exporte' }))
    const links = await tabelle.findAllByRole('link', { name: /herunterladen/ })
    expect(links[0].getAttribute('href')).toMatch(/\/api\/export\/laeufe\/.+\/datei$/)
    expect(screen.queryByRole('button', { name: 'Export erstellen' })).not.toBeInTheDocument()
    expect(screen.queryByText('1. Zeitraum')).not.toBeInTheDocument()
  })
})
