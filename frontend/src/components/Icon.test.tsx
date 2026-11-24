import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Icon } from './Icon'

describe('Icon', () => {
  it('rendert den Ligaturnamen mit der Klasse von Material Symbols Outlined', () => {
    const { container } = render(<Icon name="receipt_long" className="text-akzent" />)
    const span = container.querySelector('span')!
    expect(span).toHaveTextContent('receipt_long')
    expect(span).toHaveClass('material-symbols-outlined', 'text-akzent')
  })

  it('ist ohne label dekorativ und für Screenreader verborgen', () => {
    const { container } = render(<Icon name="home" />)
    const span = container.querySelector('span')!
    expect(span).toHaveAttribute('aria-hidden', 'true')
    expect(span).not.toHaveAttribute('role')
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('wird mit label als Bild mit Namen vorgelesen', () => {
    render(<Icon name="delete" label="Löschen" />)
    const bild = screen.getByRole('img', { name: 'Löschen' })
    expect(bild).not.toHaveAttribute('aria-hidden')
  })
})
