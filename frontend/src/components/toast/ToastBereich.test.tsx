import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { alleEntfernen } from './toast-store'
import { TOAST_DAUER_MS, ToastBereich, useToast } from '.'

function Ausloeser({ bei }: { bei: (t: ReturnType<typeof useToast>) => void }) {
  const toast = useToast()
  return (
    <button type="button" onClick={() => bei(toast)}>
      auslösen
    </button>
  )
}

describe('Toasts', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => {
    act(() => alleEntfernen())
    vi.useRealTimers()
  })

  it('Erfolg und Info verschwinden nach 6 s', () => {
    render(
      <>
        <ToastBereich />
        <Ausloeser
          bei={(t) => {
            t.erfolg('3 Belege bestätigt')
            t.info('Export gestartet')
          }}
        />
      </>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'auslösen' }))
    expect(screen.getAllByRole('status')).toHaveLength(2)

    act(() => vi.advanceTimersByTime(TOAST_DAUER_MS - 1))
    expect(screen.getByText('3 Belege bestätigt')).toBeInTheDocument()
    act(() => vi.advanceTimersByTime(1))
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('Fehler bleiben stehen und bieten „Erneut“', () => {
    const erneut = vi.fn()
    render(
      <>
        <ToastBereich />
        <Ausloeser bei={(t) => t.fehler('Speichern fehlgeschlagen: Server nicht erreichbar', { erneut })} />
      </>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'auslösen' }))
    act(() => vi.advanceTimersByTime(60_000))
    expect(screen.getByRole('alert')).toHaveTextContent('Speichern fehlgeschlagen: Server nicht erreichbar')

    fireEvent.click(screen.getByRole('button', { name: 'Erneut' }))
    expect(erneut).toHaveBeenCalledOnce()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('Erfolg mit „Rückgängig“ ruft die Aktion auf', () => {
    const rueckgaengig = vi.fn()
    render(
      <>
        <ToastBereich />
        <Ausloeser bei={(t) => t.erfolg('3 Belege bestätigt', { rueckgaengig })} />
      </>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'auslösen' }))
    fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }))
    expect(rueckgaengig).toHaveBeenCalledOnce()
  })
})
