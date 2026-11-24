import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { kategorien } from '@/mocks/fixtures/tags'
import { KategorieAuswahl } from './KategorieAuswahl'
import { renderAuswahl } from './testHilfe'

function oeffnen(wert: string | null = null) {
  const onAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <KategorieAuswahl wert={wert} onAendern={onAendern} ausloeser={<button type="button">Kategorie</button>} />,
  )
  return { ...ergebnis, onAendern }
}

describe('KategorieAuswahl', () => {
  it('wählt eine Kategorie und schließt', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Kategorie' }))
    await nutzer.click(await screen.findByRole('option', { name: kategorien[1].name }))
    expect(onAendern).toHaveBeenCalledWith(kategorien[1].id)
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('markiert die gewählte Kategorie; erneutes Wählen leert', async () => {
    const { nutzer, onAendern } = oeffnen(kategorien[0].id)
    await nutzer.click(screen.getByRole('button', { name: 'Kategorie' }))
    const option = await screen.findByRole('option', { name: /gewählt/ })
    expect(option).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Leer lassen = Default des Korrespondenten')).toBeInTheDocument()
    await nutzer.click(option)
    expect(onAendern).toHaveBeenCalledWith(null)
  })

  it('filtert über die Suche und wählt per Tastatur', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Kategorie' }))
    await screen.findAllByRole('option')
    await nutzer.type(screen.getByRole('combobox', { name: 'Kategorie suchen' }), 'fahr')
    expect(screen.getAllByRole('option')).toHaveLength(1)
    await nutzer.keyboard('{ArrowDown}{Enter}')
    expect(onAendern).toHaveBeenCalledWith(kategorien.find((k) => k.name === 'Fahrzeug')!.id)
  })
})
