import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { KorrespondentLogo } from './KorrespondentLogo'

describe('KorrespondentLogo', () => {
  it('zeigt das Logo und bei einem Ladefehler die Initialen', () => {
    const { container, rerender } = render(
      <KorrespondentLogo logo={{ url: '/api/korrespondenten/1/logo', initialen: 'BK', quelle: 'Pdf' }} />,
    )
    const bild = container.querySelector('img')!
    expect(bild).toHaveAttribute('src', '/api/korrespondenten/1/logo')
    expect(screen.queryByText('BK')).not.toBeInTheDocument()

    fireEvent.error(bild)
    expect(container.querySelector('img')).not.toBeInTheDocument()
    expect(screen.getByText('BK')).toBeInTheDocument()

    // Eine neue URL (z. B. anderer Korrespondent) wird wieder versucht.
    rerender(<KorrespondentLogo logo={{ url: '/api/korrespondenten/2/logo', initialen: 'HS', quelle: 'Pdf' }} />)
    expect(container.querySelector('img')).toHaveAttribute('src', '/api/korrespondenten/2/logo')
  })

  it('zeigt ohne Logo die Initialen', () => {
    render(<KorrespondentLogo logo={{ url: null, initialen: 'AB', quelle: 'Keine' }} />)
    expect(screen.getByText('AB')).toBeInTheDocument()
  })
})
