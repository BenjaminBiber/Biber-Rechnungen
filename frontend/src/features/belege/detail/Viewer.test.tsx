import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { belegIds } from '@/mocks/fixtures/ids'
import { b2, kopfGeladen, pdfMock, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

const viewer = () => screen.getByRole('region', { name: 'Original' })
const seite = () => within(viewer()).getByTestId('pdf-seite')

describe('Viewer', () => {
  beforeEach(() => {
    pdfMock.seiten = 1
  })

  it('zeigt das PDF mit Seitenanzeige, Zoom, Drehen und Fußzeile', async () => {
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    expect(seite()).toHaveAttribute('data-seite', '1')
    expect(within(viewer()).getByText('Seite 1 / 1')).toBeInTheDocument()
    expect(within(viewer()).getByLabelText('Zoom')).toHaveTextContent('100 %')
    const breite = Number(seite().getAttribute('data-breite'))

    await nutzer.click(within(viewer()).getByRole('button', { name: 'Vergrößern' }))
    expect(within(viewer()).getByLabelText('Zoom')).toHaveTextContent('125 %')
    expect(Number(seite().getAttribute('data-breite'))).toBeCloseTo(breite * 1.25)

    for (let i = 0; i < 4; i++) await nutzer.click(within(viewer()).getByRole('button', { name: 'Verkleinern' }))
    expect(within(viewer()).getByLabelText('Zoom')).toHaveTextContent('50 %')
    expect(within(viewer()).getByRole('button', { name: 'Verkleinern' })).toBeDisabled()

    await nutzer.click(within(viewer()).getByRole('button', { name: 'Drehen' }))
    expect(seite()).toHaveAttribute('data-drehung', '90')

    expect(within(viewer()).getByRole('link', { name: 'In neuem Tab öffnen' })).toHaveAttribute(
      'href',
      expect.stringMatching(new RegExp(`/api/belege/${b2}/dateien/.+\\?inline=true$`)),
    )
    expect(within(viewer()).getByRole('link', { name: 'Original herunterladen' })).toHaveAttribute('download')
    expect(viewer()).toHaveTextContent(
      'Unveränderbares Original · SHA-256 geprüft 23.11.2026, 03:00 · Löschsperre bis 31.12.2034',
    )
  })

  it('blättert durch mehrseitige PDFs', async () => {
    pdfMock.seiten = 3
    const { nutzer } = renderBelegDetail(`/belege/${belegIds.b3}`)
    await kopfGeladen('Autohaus Brenner GmbH')
    expect(await within(viewer()).findByText('Seite 1 / 3')).toBeInTheDocument()
    expect(within(viewer()).getByRole('button', { name: 'Vorherige Seite' })).toBeDisabled()
    await nutzer.click(within(viewer()).getByRole('button', { name: 'Nächste Seite' }))
    await nutzer.click(within(viewer()).getByRole('button', { name: 'Nächste Seite' }))
    expect(within(viewer()).getByText('Seite 3 / 3')).toBeInTheDocument()
    expect(seite()).toHaveAttribute('data-seite', '3')
    expect(within(viewer()).getByRole('button', { name: 'Nächste Seite' })).toBeDisabled()
  })

  it('schaltet auf „Erkannte Felder“ mit Quelle und Konfidenz um', async () => {
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    await nutzer.click(within(viewer()).getByRole('button', { name: 'Erkannte Felder' }))
    expect(within(viewer()).getByRole('button', { name: 'Erkannte Felder' })).toHaveAttribute('aria-pressed', 'true')
    expect(within(viewer()).queryByTestId('pdf-seite')).not.toBeInTheDocument()
    const tabelle = within(viewer()).getByRole('table', { name: 'Erkannte Felder' })
    const zeile = within(tabelle).getByText('Rechnungsnummer').closest('tr')!
    expect(zeile).toHaveTextContent('2026-48817')
    expect(zeile).toHaveTextContent('Document Intelligence')
    expect(zeile).toHaveTextContent('0,74')
    expect(within(tabelle).getByText('Brutto').closest('tr')).toHaveTextContent('264,00 €')

    await nutzer.click(within(viewer()).getByRole('button', { name: 'Original (PDF)' }))
    expect(seite()).toBeInTheDocument()
  })

  it('zeigt Bilder als <img>', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2
        ? { ...b, dateien: b.dateien.map((d) => (d.rolle === 'Original' ? { ...d, mimeType: 'image/jpeg' } : d)) }
        : b,
    )
    renderBelegDetail()
    await kopfGeladen()
    expect(within(viewer()).getByRole('img', { name: 'Original 2026-48817' })).toBeInTheDocument()
    expect(within(viewer()).queryByTestId('pdf-seite')).not.toBeInTheDocument()
  })

  it('zeigt eine reine XML-E-Rechnung als Visualisierung im sandboxed iframe', async () => {
    const b1 = belegIds.b1
    db.belege = db.belege.map((b) =>
      b.id === b1
        ? {
            ...b,
            dateien: b.dateien
              .filter((d) => d.rolle !== 'EingebettetesXml')
              .map((d) => (d.rolle === 'Original' ? { ...d, mimeType: 'application/xml' } : d)),
          }
        : b,
    )
    renderBelegDetail(`/belege/${b1}`)
    await kopfGeladen('Hostwerk Süd GmbH')
    expect(within(viewer()).getByRole('button', { name: 'Visualisierung' })).toHaveAttribute('aria-pressed', 'true')
    expect(within(viewer()).queryByRole('button', { name: 'Original (PDF)' })).not.toBeInTheDocument()
    const rahmen = within(viewer()).getByTitle('Visualisierung der E-Rechnung')
    expect(rahmen.tagName).toBe('IFRAME')
    expect(rahmen).toHaveAttribute('sandbox', '')
    expect(rahmen).toHaveAttribute('src', `/api/belege/${b1}/visualisierung`)
    expect(within(viewer()).queryByRole('button', { name: 'Drehen' })).not.toBeInTheDocument()
  })
})
