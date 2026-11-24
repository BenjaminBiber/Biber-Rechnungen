import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { szenario } from '@/mocks/szenarien'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from '../detail/testHilfe'

vi.mock('react-pdf', () => import('../detail/testHilfe').then((m) => m.reactPdfMock))

/** Beleg b2 mit vier Seiten. */
function vierSeiten() {
  db.belege = db.belege.map((b) => (b.id === b2 ? { ...b, seitenanzahl: 4 } : b))
}

async function oeffnen() {
  const app = renderBelegDetail(`/belege/${b2}/aufteilen`)
  // Der modale Dialog verbirgt die Detailseite vor Screenreadern, daher direkt auf den Dialog warten.
  const dialog = await screen.findByRole('dialog', { name: 'Beleg aufteilen' }, { timeout: 5000 })
  return { ...app, dialog }
}

describe('Beleg aufteilen', () => {
  it('zeigt alle Seiten als Miniaturen', async () => {
    vierSeiten()
    const { dialog } = await oeffnen()
    expect(dialog).toHaveTextContent(
      '2026-11-19_2026-48817.pdf · 4 Seiten. Klicke zwischen zwei Seiten, um einen Schnitt zu setzen.',
    )
    const bilder = within(dialog).getAllByRole('img', { name: /^Seite \d$/ })
    expect(bilder).toHaveLength(4)
    expect(bilder[1]).toHaveAttribute('src', `/api/belege/${b2}/vorschau?seite=2&breite=200`)
    expect(bilder[1]).toHaveAttribute('loading', 'lazy')
    expect(within(dialog).getByRole('button', { name: 'In 1 Belege aufteilen' })).toBeDisabled()
  })

  it('setzt und entfernt Schnitte und passt Chips und Zusammenfassung an', async () => {
    vierSeiten()
    const { dialog, nutzer } = await oeffnen()
    const schnitt = within(dialog).getByRole('button', { name: 'Schnitt nach Seite 2' })
    await nutzer.click(schnitt)
    expect(schnitt).toHaveAttribute('aria-pressed', 'true')
    const teile = within(dialog).getByRole('list', { name: 'Teile' })
    expect(
      within(teile)
        .getAllByRole('listitem')
        .map((l) => l.textContent),
    ).toEqual(['A Seiten 1–2 → neuer Beleg', 'B Seiten 3–4 → neuer Beleg'])
    expect(within(dialog).getByRole('button', { name: 'In 2 Belege aufteilen' })).toBeEnabled()

    // Tastatur: Schnitt nach Seite 3 per Leertaste, dann Schnitt nach Seite 2 wieder entfernen.
    within(dialog).getByRole('button', { name: 'Schnitt nach Seite 3' }).focus()
    await nutzer.keyboard(' ')
    expect(within(teile).getAllByRole('listitem')).toHaveLength(3)
    await nutzer.click(schnitt)
    expect(schnitt).toHaveAttribute('aria-pressed', 'false')
    expect(
      within(teile)
        .getAllByRole('listitem')
        .map((l) => l.textContent),
    ).toEqual(['A Seiten 1–3 → neuer Beleg', 'B Seite 4 → neuer Beleg'])
  })

  it('sendet die Teile, meldet den Erfolg und wechselt zu „Hochladen & Import“', async () => {
    vierSeiten()
    const mitschnitt = anfragenMitschneiden()
    const { dialog, nutzer, router } = await oeffnen()
    await nutzer.click(within(dialog).getByRole('button', { name: 'Schnitt nach Seite 2' }))
    await nutzer.click(within(dialog).getByRole('button', { name: 'In 2 Belege aufteilen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/hochladen'))
    expect(await screen.findByText('Beleg wird in 2 Belege aufgeteilt')).toBeInTheDocument()
    expect(await mitschnitt.anfragen()).toContainEqual({
      methode: 'POST',
      pfad: `/api/belege/${b2}/aufteilen`,
      body: { teile: [{ seiten: [1, 2] }, { seiten: [3, 4] }] },
    })
  })

  it('zeigt einen Serverfehler im Dialog', async () => {
    vierSeiten()
    szenario.fehler('/api/belege/{id}/aufteilen', 409, 'POST')
    const { dialog, nutzer, router } = await oeffnen()
    await nutzer.click(within(dialog).getByRole('button', { name: 'Schnitt nach Seite 1' }))
    await nutzer.click(within(dialog).getByRole('button', { name: 'In 2 Belege aufteilen' }))
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe(`/belege/${b2}/aufteilen`)
  })

  it('nennt bei einem einseitigen Beleg den Grund und bietet nur „Schließen“', async () => {
    const { dialog, nutzer, router } = await oeffnen()
    expect(dialog).toHaveTextContent('Der Beleg hat nur eine Seite und kann nicht aufgeteilt werden.')
    expect(within(dialog).queryByRole('button', { name: /aufteilen/ })).not.toBeInTheDocument()
    await nutzer.click(within(dialog).getAllByRole('button', { name: 'Schließen' }).at(-1)!)
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}`))
  })

  it('schließt mit Esc und führt zurück zum Beleg', async () => {
    vierSeiten()
    const { nutzer, router } = await oeffnen()
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}`))
  })

  it('leitet NurLesen zum Beleg weiter', async () => {
    vierSeiten()
    szenario.rolle('NurLesen')
    const { router } = renderBelegDetail(`/belege/${b2}/aufteilen`)
    await kopfGeladen()
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}`))
    expect(screen.queryByRole('dialog', { name: 'Beleg aufteilen' })).not.toBeInTheDocument()
  })
})
