import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { Matching } from '@/api/typen'
import { tagIds } from '@/mocks/fixtures/ids'
import { server } from '@/mocks/server'
import { MatchingEditor } from './MatchingEditor'
import { renderAuswahl } from './testHilfe'

function Editor({ start, onAendern }: { start: Matching; onAendern?: (m: Matching) => void }) {
  const [wert, setWert] = useState(start)
  return (
    <MatchingEditor
      wert={wert}
      onAendern={(m) => {
        setWert(m)
        onAendern?.(m)
      }}
      ziel="Tag"
      zielId={tagIds.pp}
      zielName="bezahlt PayPal"
    />
  )
}

describe('MatchingEditor', () => {
  it('bietet die Algorithmen mit den Texten des Designs an', () => {
    renderAuswahl(<Editor start={{ algorithmus: 'Keiner', muster: null, grossKlein: false }} />)
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Ein Wort trifft zu',
      'Alle Wörter treffen zu',
      'Exakte Zeichenfolge',
      'Regulärer Ausdruck',
      'Unscharf (ähnlich)',
      'Auto (ML.NET, aus bisherigen Zuweisungen)',
      'Keiner',
    ])
  })

  it('blendet das Muster bei „Keiner“ und „Auto“ aus', async () => {
    const { nutzer } = renderAuswahl(<Editor start={{ algorithmus: 'EinWort', muster: 'paypal', grossKlein: false }} />)
    expect(screen.getByLabelText('Muster')).toHaveValue('paypal')
    await nutzer.selectOptions(screen.getByLabelText('Algorithmus'), 'Keiner')
    expect(screen.queryByLabelText('Muster')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Testen' })).toBeDisabled()
    await nutzer.selectOptions(screen.getByLabelText('Algorithmus'), 'Auto')
    expect(screen.queryByLabelText('Muster')).not.toBeInTheDocument()
    await nutzer.selectOptions(screen.getByLabelText('Algorithmus'), 'Regex')
    expect(screen.getByLabelText('Muster')).toBeInTheDocument()
  })

  it('meldet Änderungen an Muster und Groß-/Kleinschreibung', async () => {
    const onAendern = vi.fn()
    const { nutzer } = renderAuswahl(
      <Editor start={{ algorithmus: 'EinWort', muster: '', grossKlein: false }} onAendern={onAendern} />,
    )
    await nutzer.type(screen.getByLabelText('Muster'), 'pp')
    await nutzer.click(screen.getByLabelText('Groß-/Kleinschreibung beachten'))
    expect(onAendern).toHaveBeenLastCalledWith({ algorithmus: 'EinWort', muster: 'pp', grossKlein: true })
  })

  it('zeigt das Testergebnis entprellt an', async () => {
    const anfragen = vi.fn()
    server.use(
      http.post('*/api/matching/testen', async ({ request }) => {
        anfragen(await request.json())
        return HttpResponse.json({ treffer: 37, gesamt: 248, ohneZuweisung: 2, letzterTreffer: 'HVN-552190' })
      }),
    )
    const { nutzer } = renderAuswahl(<Editor start={{ algorithmus: 'EinWort', muster: 'pay', grossKlein: false }} />)
    expect(await screen.findByText('Trifft auf 37 von 248 Belegen zu – zuletzt HVN-552190.')).toBeInTheDocument()
    await nutzer.type(screen.getByLabelText('Muster'), 'pal')
    // Beim Öffnen und nach dem Tippen je eine Anfrage – nicht pro Tastendruck.
    await waitFor(() => expect(anfragen).toHaveBeenCalledTimes(2), { timeout: 2000 })
    await new Promise((fertig) => setTimeout(fertig, 500))
    expect(anfragen).toHaveBeenCalledTimes(2)
    expect(anfragen).toHaveBeenLastCalledWith({
      ziel: 'Tag',
      zielId: tagIds.pp,
      matching: { algorithmus: 'EinWort', muster: 'paypal', grossKlein: false },
    })
  })

  it('„Testen“ öffnet den Testdialog', async () => {
    const { nutzer } = renderAuswahl(<Editor start={{ algorithmus: 'EinWort', muster: 'paypal', grossKlein: false }} />)
    await nutzer.click(screen.getByRole('button', { name: 'Testen' }))
    expect(await screen.findByRole('dialog', { name: 'Regel testen: „bezahlt PayPal“' })).toBeInTheDocument()
  })
})
