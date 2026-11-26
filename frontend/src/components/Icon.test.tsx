import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Icon } from './Icon'

describe('Icon', () => {
  it('zeichnet das animierte Icon und behält Namen und Klassen', () => {
    const { container } = render(<Icon name="receipt_long" className="text-akzent" />)
    const span = container.querySelector('span')!
    expect(span).toHaveAttribute('data-icon', 'receipt_long')
    expect(span).toHaveClass('material-symbols-outlined', 'symbol', 'text-akzent')
    expect(span.querySelector('svg')).toBeInTheDocument()
    expect(span).toHaveTextContent('')
  })

  it('fällt bei einem Namen ohne eigenes Icon auf das Schriftzeichen zurück', () => {
    const { container } = render(<Icon name="gibt_es_nicht" />)
    const span = container.querySelector('span')!
    expect(span).toHaveTextContent('gibt_es_nicht')
    expect(span).toHaveClass('material-symbols-outlined')
    expect(span).not.toHaveClass('symbol')
    expect(span.querySelector('svg')).not.toBeInTheDocument()
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

  it('zeichnet beim Namenswechsel das neue Icon', () => {
    const { container, rerender } = render(<Icon name="content_copy" />)
    const vorher = container.querySelector('svg')!.innerHTML
    rerender(<Icon name="check" />)
    expect(container.querySelector('span')).toHaveAttribute('data-icon', 'check')
    expect(container.querySelector('svg')!.innerHTML).not.toBe(vorher)
  })

  it('übersteht Klick und Hover im Button ohne Fehler', () => {
    render(
      <button type="button" aria-label="Kopieren">
        <Icon name="content_copy" />
      </button>,
    )
    const knopf = screen.getByRole('button', { name: 'Kopieren' })
    fireEvent.click(knopf)
    fireEvent.pointerEnter(knopf, { pointerType: 'mouse' })
    fireEvent.pointerLeave(knopf, { pointerType: 'mouse' })
    expect(knopf.querySelector('svg')).toBeInTheDocument()
  })
})
