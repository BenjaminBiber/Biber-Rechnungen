import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Chip, type ChipTon } from './Chip'

describe('Chip', () => {
  it.each<ChipTon>(['blau', 'orange', 'gruen', 'rot', 'grau', 'petrol', 'lila'])(
    'setzt für den Ton „%s“ die passende Klasse',
    (ton) => {
      render(<Chip ton={ton}>Status</Chip>)
      expect(screen.getByText('Status')).toHaveClass('chip', `chip-${ton}`)
    },
  )

  it('zeigt ein optionales Icon dekorativ vor dem Text', () => {
    render(
      <Chip ton="orange" icon="error">
        zu prüfen
      </Chip>,
    )
    const chip = screen.getByText('zu prüfen')
    const icon = chip.querySelector('.material-symbols-outlined')
    expect(icon).toHaveAttribute('data-icon', 'error')
    expect(icon).toHaveAttribute('aria-hidden', 'true')
  })

  it('hat ohne onEntfernen keinen Knopf', () => {
    render(<Chip ton="grau">Strom</Chip>)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('ruft onEntfernen über den Entfernen-Knopf auf', async () => {
    const onEntfernen = vi.fn()
    render(
      <Chip ton="blau" onEntfernen={onEntfernen}>
        Büro
      </Chip>,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Büro entfernen' }))
    expect(onEntfernen).toHaveBeenCalledOnce()
  })
})
