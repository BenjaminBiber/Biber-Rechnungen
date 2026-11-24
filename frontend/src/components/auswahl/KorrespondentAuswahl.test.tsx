import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import type { KorrespondentListeneintrag } from '@/api/typen'
import { server } from '@/mocks/server'
import { KorrespondentAuswahl } from './KorrespondentAuswahl'
import { renderAuswahl } from './testHilfe'

function eintrag(id: string, name: string, ustIdNr: string | null): KorrespondentListeneintrag {
  return {
    id,
    name,
    ustIdNr,
    logo: { url: null, initialen: name.slice(0, 2).toUpperCase(), quelle: 'Keine' },
    matching: { algorithmus: 'Keiner', muster: null, grossKlein: false },
    anzahlBelege: 1,
    kostenJahr: 0,
    istNeu: false,
  }
}

function oeffnen(props: { wert?: string | null; onNeuAusBelegdaten?: () => void } = {}) {
  const onAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <KorrespondentAuswahl
      wert={props.wert ?? null}
      onAendern={onAendern}
      onNeuAusBelegdaten={props.onNeuAusBelegdaten}
      ausloeser={<button type="button">Korrespondent</button>}
    />,
  )
  return { ...ergebnis, onAendern }
}

describe('KorrespondentAuswahl', () => {
  it('sucht entprellt am Server und wählt einen Treffer', async () => {
    const suchen = vi.fn()
    server.events.on('request:start', ({ request }) => {
      const url = new URL(request.url)
      if (url.pathname === '/api/korrespondenten') suchen(url.searchParams.get('suche'))
    })
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Korrespondent' }))
    await screen.findAllByRole('option')
    await nutzer.type(screen.getByRole('combobox', { name: 'Korrespondent suchen' }), 'krä')
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1))
    // Pro Tastendruck keine eigene Anfrage – nur die leere Startsuche und „krä“.
    expect(suchen.mock.calls.map(([s]) => s)).toEqual([null, 'krä'])
    const option = screen.getByRole('option', { name: /Bürowelt Krämer KG/ })
    expect(option).toHaveTextContent('DE 284117560')
    await nutzer.click(option)
    expect(onAendern).toHaveBeenCalledWith(expect.any(String))
    server.events.removeAllListeners()
  })

  it('kennzeichnet ähnliche Namen ohne USt-IdNr. als mögliche Dublette', async () => {
    server.use(
      http.get('*/api/korrespondenten', () =>
        HttpResponse.json([
          eintrag('a', 'Bürowelt Krämer KG', 'DE 284117560'),
          eintrag('b', 'Buerowelt Kraemer', null),
        ]),
      ),
    )
    const { nutzer } = oeffnen({ wert: 'a' })
    await nutzer.click(screen.getByRole('button', { name: 'Korrespondent' }))
    expect(await screen.findByRole('option', { name: /Buerowelt Kraemer/ })).toHaveTextContent('mögliche Dublette')
    const gewaehlt = screen.getByRole('option', { name: /Bürowelt Krämer KG/ })
    expect(gewaehlt).not.toHaveTextContent('mögliche Dublette')
    expect(gewaehlt).toHaveAttribute('aria-selected', 'true')
  })

  it('„Neu aus Belegdaten anlegen“ per Tastatur', async () => {
    server.use(http.get('*/api/korrespondenten', () => HttpResponse.json([eintrag('a', 'Hostwerk Süd', null)])))
    const onNeu = vi.fn()
    const { nutzer } = oeffnen({ onNeuAusBelegdaten: onNeu })
    await nutzer.click(screen.getByRole('button', { name: 'Korrespondent' }))
    await screen.findByRole('option', { name: /Hostwerk/ })
    await nutzer.keyboard('{ArrowUp}{Enter}')
    expect(onNeu).toHaveBeenCalledOnce()
  })
})
