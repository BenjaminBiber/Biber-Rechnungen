import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from '../testHilfe'
import { gesamtVorschlag } from './PositionenTab'

vi.mock('react-pdf', () => import('../testHilfe').then((m) => m.reactPdfMock))

const pfad = `/belege/${b2}/positionen`
const zeilen = () => within(screen.getByRole('table')).getAllByRole('row').slice(1)
const vergleich = () => screen.getByTestId('summenvergleich')

async function oeffnen() {
  const ergebnis = renderBelegDetail(pfad)
  await kopfGeladen()
  await screen.findByRole('table')
  return ergebnis
}

describe('Tab Positionen', () => {
  it('zeigt sechs Positionen und die Differenz 2,00 € orange', async () => {
    await oeffnen()
    expect(zeilen()).toHaveLength(6)
    expect(zeilen()[0]).toHaveTextContent('Kopierpapier A4 80 g, 5 × 500 Bl.')
    expect(zeilen()[0]).toHaveTextContent('2 Karton')
    expect(within(zeilen()[0]).getByRole('link', { name: 'Kopierpapier A4 80 g' })).toHaveAttribute(
      'href',
      expect.stringMatching(/^\/artikel\//),
    )
    expect(zeilen()[1]).toHaveTextContent('—')
    await waitFor(() => expect(vergleich()).toHaveTextContent(/Summe Positionen 219,85\s€/))
    expect(vergleich()).toHaveTextContent(/Netto laut Beleg 221,85\s€ · Differenz 2,00\s€ \(Toleranz 0,02\s€\)/)
    expect(vergleich()).toHaveAttribute('data-abweichung', 'true')
    expect(vergleich()).toHaveClass('bg-orange-flaeche')
  })

  it('Position hinzufügen und speichern sendet sieben Positionen', async () => {
    const { nutzer } = await oeffnen()
    const mitschnitt = anfragenMitschneiden()
    await nutzer.click(screen.getByRole('button', { name: '+ Position hinzufügen' }))
    await nutzer.type(screen.getByLabelText('Text'), 'Versandkostenpauschale')
    await nutzer.type(screen.getByLabelText('Einzelpreis'), '2,00')
    expect(screen.getByLabelText('Gesamt')).toHaveValue('2,00')
    await nutzer.click(screen.getByRole('button', { name: 'Übernehmen' }))
    expect(zeilen()).toHaveLength(7)
    expect(vergleich()).toHaveTextContent(/Differenz 0,00\s€/)
    expect(vergleich()).toHaveClass('bg-gruen-flaeche')

    await nutzer.click(screen.getByRole('button', { name: 'Änderungen speichern' }))
    expect(await screen.findByText('Positionen gespeichert')).toBeInTheDocument()
    const [anfrage] = await mitschnitt.anfragen()
    expect(anfrage.methode).toBe('PUT')
    expect(anfrage.pfad).toBe(`/api/belege/${b2}/positionen`)
    const positionen = anfrage.body as { nr: number; text: string; gesamt: number }[]
    expect(positionen).toHaveLength(7)
    expect(positionen[6]).toMatchObject({ nr: 7, text: 'Versandkostenpauschale', gesamt: 2, menge: 1, steuersatz: 19 })
  })

  it('schlägt Gesamt aus Menge × Einzelpreis vor', async () => {
    expect(gesamtVorschlag('3', '2,49')).toBe('7,47')
    expect(gesamtVorschlag('', '2,49')).toBeNull()
    const { nutzer } = await oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Position 3 bearbeiten' }))
    const menge = screen.getByLabelText('Menge')
    await nutzer.clear(menge)
    await nutzer.type(menge, '12')
    expect(screen.getByLabelText('Gesamt')).toHaveValue('29,88')
  })

  it('bearbeitet eine Position im Dialog, Abbrechen ändert nichts', async () => {
    const { nutzer } = await oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Position 2 bearbeiten' }))
    const dialog = screen.getByRole('dialog', { name: 'Position 2 bearbeiten' })
    expect(dialog).toContainElement(screen.getByLabelText('Text'))
    await nutzer.type(screen.getByLabelText('Text'), ' geändert')
    await nutzer.click(screen.getByRole('button', { name: 'Abbrechen' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Änderungen speichern' })).not.toBeInTheDocument()

    await nutzer.click(screen.getByRole('button', { name: 'Position 2 bearbeiten' }))
    const text = screen.getByLabelText('Text')
    await nutzer.clear(text)
    await nutzer.click(screen.getByRole('button', { name: 'Übernehmen' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Bitte einen Text eingeben.')
    await nutzer.type(text, 'Neuer Text{Enter}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(zeilen()[1]).toHaveTextContent('Neuer Text')
    expect(screen.getByRole('button', { name: 'Änderungen speichern' })).toBeInTheDocument()
  })

  it('löschen und verwerfen setzt zurück', async () => {
    const { nutzer } = await oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Position 6 löschen' }))
    expect(zeilen()).toHaveLength(5)
    await nutzer.click(screen.getByRole('button', { name: 'Verwerfen' }))
    expect(zeilen()).toHaveLength(6)
    expect(screen.queryByRole('button', { name: 'Änderungen speichern' })).not.toBeInTheDocument()
  })

  it('zeigt NurLesen nur die Tabelle ohne Bearbeiten', async () => {
    szenario.rolle('NurLesen')
    await oeffnen()
    expect(zeilen()).toHaveLength(6)
    expect(screen.queryByRole('button', { name: '+ Position hinzufügen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /bearbeiten$/ })).not.toBeInTheDocument()
  })
})
