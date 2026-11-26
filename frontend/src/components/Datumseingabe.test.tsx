import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Datumseingabe } from './Datumseingabe'

const heute = new Date(2026, 10, 26)

function Feld({
  start = '',
  zeitraum = false,
  onGewaehlt,
}: {
  start?: string
  zeitraum?: boolean
  onGewaehlt?: () => void
}) {
  const [wert, setWert] = useState(start)
  return (
    <>
      <label htmlFor="d">Datum</label>
      <Datumseingabe id="d" wert={wert} onAendern={setWert} onGewaehlt={onGewaehlt} zeitraum={zeitraum} heute={heute} />
    </>
  )
}

const kalenderKnopf = () => screen.getByRole('button', { name: /im Kalender wählen/ })

describe('Datumseingabe', () => {
  it('lässt weiter frei tippen', async () => {
    const nutzer = userEvent.setup()
    render(<Feld />)
    await nutzer.type(screen.getByLabelText('Datum'), '01.02.2026')
    expect(screen.getByLabelText('Datum')).toHaveValue('01.02.2026')
  })

  it('öffnet den Kalender beim Monat des Werts und übernimmt einen Tag', async () => {
    const nutzer = userEvent.setup()
    const onGewaehlt = vi.fn()
    render(<Feld start="15.03.2025" onGewaehlt={onGewaehlt} />)
    await nutzer.click(kalenderKnopf())
    expect(screen.getByRole('group', { name: 'März 2025' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '15. März 2025' })).toHaveAttribute('aria-pressed', 'true')
    await nutzer.click(screen.getByRole('button', { name: '3. März 2025' }))
    expect(screen.getByLabelText('Datum')).toHaveValue('03.03.2025')
    expect(onGewaehlt).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('blättert Monate und wählt per Tastatur', async () => {
    const nutzer = userEvent.setup()
    render(<Feld />)
    await nutzer.click(kalenderKnopf())
    expect(screen.getByRole('group', { name: 'November 2026' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: '26. November 2026' })).toHaveFocus())
    await nutzer.keyboard('{ArrowRight}{ArrowDown}')
    await waitFor(() => expect(screen.getByRole('button', { name: '4. Dezember 2026' })).toHaveFocus())
    await nutzer.keyboard('{Enter}')
    expect(screen.getByLabelText('Datum')).toHaveValue('04.12.2026')
  })

  it('wählt im Zeitraum-Modus Beginn und Ende und meldet erst danach', async () => {
    const nutzer = userEvent.setup()
    const onGewaehlt = vi.fn()
    render(<Feld zeitraum onGewaehlt={onGewaehlt} />)
    await nutzer.click(kalenderKnopf())
    await nutzer.click(screen.getByRole('button', { name: '20. November 2026' }))
    expect(screen.getByLabelText('Datum')).toHaveValue('20.11.2026')
    expect(onGewaehlt).not.toHaveBeenCalled()
    await nutzer.click(screen.getByRole('button', { name: '3. November 2026' }))
    expect(screen.getByLabelText('Datum')).toHaveValue('03.11.2026 – 20.11.2026')
    expect(onGewaehlt).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('übernimmt im Zeitraum-Modus ein einzelnes Datum beim Schließen', async () => {
    const nutzer = userEvent.setup()
    const onGewaehlt = vi.fn()
    render(<Feld zeitraum onGewaehlt={onGewaehlt} />)
    await nutzer.click(kalenderKnopf())
    await nutzer.click(screen.getByRole('button', { name: '20. November 2026' }))
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByLabelText('Datum')).toHaveValue('20.11.2026')
    expect(onGewaehlt).toHaveBeenCalledOnce()
  })

  it('zeigt keinen Kalenderknopf bei schreibgeschützten Feldern', () => {
    render(<Datumseingabe aria-label="Datum" wert="01.01.2026" onAendern={() => {}} readOnly />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
