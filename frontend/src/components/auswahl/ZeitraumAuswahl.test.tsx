import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { renderAuswahl } from './testHilfe'
import { ZeitraumAuswahl, type Zeitraum } from './ZeitraumAuswahl'

function oeffnen(wert: Zeitraum = { von: null, bis: null }) {
  const onAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <ZeitraumAuswahl
      wert={wert}
      onAendern={onAendern}
      heute={new Date(2026, 10, 24)}
      ausloeser={<button type="button">Zeitraum</button>}
    />,
  )
  return { ...ergebnis, onAendern }
}

describe('ZeitraumAuswahl', () => {
  it('übernimmt eine Vorgabe und schließt', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Zeitraum' }))
    await nutzer.click(screen.getByRole('button', { name: 'Letztes Quartal' }))
    expect(onAendern).toHaveBeenCalledWith({ von: '2026-07-01', bis: '2026-09-30' })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('zeigt den aktuellen Wert und wendet geänderte Felder per Enter an', async () => {
    const { nutzer, onAendern } = oeffnen({ von: '2026-01-01', bis: null })
    await nutzer.click(screen.getByRole('button', { name: 'Zeitraum' }))
    expect(screen.getByLabelText('Von')).toHaveValue('01.01.2026')
    await nutzer.type(screen.getByLabelText('Bis'), '31.03.2026{Enter}')
    expect(onAendern).toHaveBeenCalledWith({ von: '2026-01-01', bis: '2026-03-31' })
  })

  it('lehnt ein ungültiges Datum ab', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Zeitraum' }))
    await nutzer.type(screen.getByLabelText('Von'), '31.02.2026')
    await nutzer.click(screen.getByRole('button', { name: 'Anwenden' }))
    expect(screen.getByRole('alert')).toHaveTextContent('TT.MM.JJJJ')
    expect(screen.getByLabelText('Von')).toHaveAttribute('aria-invalid', 'true')
    expect(onAendern).not.toHaveBeenCalled()
  })

  it('lehnt „Von“ nach „Bis“ ab; „Leeren“ setzt beides zurück', async () => {
    const { nutzer, onAendern } = oeffnen({ von: '2026-05-01', bis: '2026-04-01' })
    await nutzer.click(screen.getByRole('button', { name: 'Zeitraum' }))
    await nutzer.click(screen.getByRole('button', { name: 'Anwenden' }))
    expect(screen.getByRole('alert')).toBeInTheDocument()
    await nutzer.click(screen.getByRole('button', { name: 'Leeren' }))
    expect(onAendern).toHaveBeenCalledWith({ von: null, bis: null })
  })

  it('Esc schließt ohne Änderung', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Zeitraum' }))
    expect(screen.getByRole('dialog', { name: 'Zeitraum' })).toBeInTheDocument()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(onAendern).not.toHaveBeenCalled()
  })
})
