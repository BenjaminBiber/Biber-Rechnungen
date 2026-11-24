import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { BetragAuswahl, type Betragsbereich } from './BetragAuswahl'
import { renderAuswahl } from './testHilfe'

function oeffnen(wert: Betragsbereich = { von: null, bis: null, art: 'Brutto' }) {
  const onAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <BetragAuswahl wert={wert} onAendern={onAendern} ausloeser={<button type="button">Betrag</button>} />,
  )
  return { ...ergebnis, onAendern }
}

describe('BetragAuswahl', () => {
  it('wendet Netto mit deutschen Beträgen an', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Betrag' }))
    await nutzer.click(screen.getByRole('button', { name: 'Netto' }))
    expect(screen.getByRole('button', { name: 'Netto' })).toHaveAttribute('aria-pressed', 'true')
    await nutzer.type(screen.getByLabelText('Von (€)'), '1.000,50')
    await nutzer.type(screen.getByLabelText('Bis (€)'), '2000{Enter}')
    expect(onAendern).toHaveBeenCalledWith({ von: 1000.5, bis: 2000, art: 'Netto' })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('zeigt den aktuellen Wert', async () => {
    const { nutzer } = oeffnen({ von: 100, bis: null, art: 'Netto' })
    await nutzer.click(screen.getByRole('button', { name: 'Betrag' }))
    expect(screen.getByLabelText('Von (€)')).toHaveValue('100,00')
    expect(screen.getByRole('button', { name: 'Netto' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('lehnt ungültige Beträge und Von > Bis ab', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Betrag' }))
    await nutzer.type(screen.getByLabelText('Von (€)'), 'zehn')
    await nutzer.click(screen.getByRole('button', { name: 'Anwenden' }))
    expect(screen.getByRole('alert')).toHaveTextContent('gültigen Betrag')
    await nutzer.clear(screen.getByLabelText('Von (€)'))
    await nutzer.type(screen.getByLabelText('Von (€)'), '50')
    await nutzer.type(screen.getByLabelText('Bis (€)'), '10')
    await nutzer.click(screen.getByRole('button', { name: 'Anwenden' }))
    expect(screen.getByRole('alert')).toHaveTextContent('größer')
    expect(onAendern).not.toHaveBeenCalled()
  })

  it('„Leeren“ entfernt die Grenzen', async () => {
    const { nutzer, onAendern } = oeffnen({ von: 1, bis: 2, art: 'Brutto' })
    await nutzer.click(screen.getByRole('button', { name: 'Betrag' }))
    await nutzer.click(screen.getByRole('button', { name: 'Leeren' }))
    expect(onAendern).toHaveBeenCalledWith({ von: null, bis: null, art: 'Brutto' })
  })
})
