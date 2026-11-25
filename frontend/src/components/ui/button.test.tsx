import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Button } from './button'

describe('Button', () => {
  it('ist mit `laedt` gesperrt und als beschäftigt markiert', () => {
    render(<Button laedt>Speichern</Button>)
    const knopf = screen.getByRole('button', { name: 'Speichern' })
    expect(knopf).toBeDisabled()
    expect(knopf).toHaveAttribute('aria-busy', 'true')
  })

  it('sperrt sich nach dem Klick, bis das Promise aus `onClick` fertig ist', async () => {
    const user = userEvent.setup()
    let fertig: () => void = () => undefined
    let klicks = 0
    render(
      <Button
        onClick={() => {
          klicks++
          return new Promise<void>((r) => (fertig = r))
        }}
      >
        Senden
      </Button>,
    )
    const knopf = screen.getByRole('button', { name: 'Senden' })
    await user.click(knopf)
    expect(knopf).toBeDisabled()
    expect(knopf).toHaveAttribute('aria-busy', 'true')
    await user.click(knopf)
    expect(klicks).toBe(1)

    fertig()
    await screen.findByRole('button', { name: 'Senden' })
    await expect.poll(() => knopf.hasAttribute('aria-busy')).toBe(false)
    expect(knopf).toBeEnabled()
  })
})
