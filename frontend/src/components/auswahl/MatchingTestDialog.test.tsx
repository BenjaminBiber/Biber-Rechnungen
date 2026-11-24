import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { tagIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { MatchingTestDialog } from './MatchingTestDialog'
import { renderAuswahl } from './testHilfe'

const auszug = '… Betrag 214,00 EUR wurde per PayPal beglichen. Vielen Dank …'

function mitErgebnis() {
  const angewendet = vi.fn()
  server.use(
    http.post('*/api/matching/testen', () =>
      HttpResponse.json({
        treffer: 37,
        gesamt: 248,
        ohneZuweisung: 2,
        auszug,
        trefferStart: auszug.indexOf('PayPal'),
        trefferLaenge: 6,
      }),
    ),
    http.post('*/api/matching/anwenden', async ({ request }) => {
      angewendet(await request.json())
      return HttpResponse.json({ geaendert: 2 })
    }),
  )
  return angewendet
}

function oeffnen() {
  const onSpeichern = vi.fn()
  const onOffenAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <MatchingTestDialog
      offen
      onOffenAendern={onOffenAendern}
      wert={{ algorithmus: 'EinWort', muster: 'paypal', grossKlein: false }}
      ziel="Tag"
      zielId={tagIds.pp}
      zielName="bezahlt PayPal"
      onSpeichern={onSpeichern}
    />,
  )
  return { ...ergebnis, onSpeichern, onOffenAendern }
}

describe('MatchingTestDialog', () => {
  it('zeigt Regel, markierten Treffer und Zahlen', async () => {
    mitErgebnis()
    oeffnen()
    const dialog = screen.getByRole('dialog', { name: 'Regel testen: „bezahlt PayPal“' })
    expect(dialog).toHaveTextContent('Ein Wort')
    expect(dialog).toHaveTextContent('paypal')
    expect(await screen.findByText('37 Treffer in 248 Belegen')).toBeInTheDocument()
    expect(screen.getByText(/davon 2 ohne den Tag/)).toBeInTheDocument()
    expect(screen.getByTestId('auszug').querySelector('mark')).toHaveTextContent(/^PayPal$/)
  })

  it('wendet ohne Checkbox nichts an', async () => {
    const angewendet = mitErgebnis()
    const { nutzer, onSpeichern, onOffenAendern } = oeffnen()
    await screen.findByText('37 Treffer in 248 Belegen')
    await nutzer.click(screen.getByRole('button', { name: 'Regel speichern' }))
    await waitFor(() => expect(onOffenAendern).toHaveBeenCalledWith(false))
    expect(onSpeichern).toHaveBeenCalledOnce()
    expect(angewendet).not.toHaveBeenCalled()
  })

  it('wendet bei gesetzter Checkbox auf die bestehenden Belege an', async () => {
    const angewendet = mitErgebnis()
    const { nutzer, onSpeichern } = oeffnen()
    await nutzer.click(await screen.findByLabelText('Tag jetzt auch auf die 2 bestehenden Belege anwenden'))
    await nutzer.click(screen.getByRole('button', { name: 'Regel speichern' }))
    await waitFor(() => expect(angewendet).toHaveBeenCalledWith({ ziel: 'Tag', zielId: tagIds.pp }))
    expect(onSpeichern).toHaveBeenCalledOnce()
  })

  it('„Schließen“ schließt ohne Speichern', async () => {
    mitErgebnis()
    const { nutzer, onSpeichern, onOffenAendern } = oeffnen()
    await nutzer.click(screen.getAllByRole('button', { name: 'Schließen' })[0])
    expect(onOffenAendern).toHaveBeenCalledWith(false)
    expect(onSpeichern).not.toHaveBeenCalled()
  })
})
