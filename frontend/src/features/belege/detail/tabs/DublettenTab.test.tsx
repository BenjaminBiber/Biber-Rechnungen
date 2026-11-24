import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { belegIds } from '@/mocks/fixtures/ids'
import { szenario } from '@/mocks/szenarien'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from '../testHilfe'

vi.mock('react-pdf', () => import('../testHilfe').then((m) => m.reactPdfMock))

const pfad = `/belege/${b2}/dubletten`

function abgleich(titel: string) {
  return screen.getByText(titel).closest('div')!
}

describe('Tab Dubletten', () => {
  it('zeigt beide Abgleiche ohne Treffer', async () => {
    renderBelegDetail(pfad)
    await kopfGeladen()
    expect(await screen.findByText('Keine identische Datei im Archiv.')).toBeInTheDocument()
    expect(screen.getByText('Kein anderer Beleg mit Korrespondent + Rechnungsnr. + Betrag.')).toBeInTheDocument()
    expect(within(abgleich('Datei-Abgleich (SHA-256)')).getByRole('img', { name: 'Kein Treffer' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Als Dublette markieren …' })).toBeInTheDocument()
  })

  it('zeigt Treffer als Link auf den anderen Beleg', async () => {
    const b3 = belegIds.b3
    db.belege = db.belege.map((b) =>
      b.id === b2
        ? {
            ...b,
            dubletten: { ...b.dubletten, hashTreffer: true, duplikatVon: { id: b3, name: 'RE-ANDERER' } },
          }
        : b,
    )
    renderBelegDetail(pfad)
    await kopfGeladen()
    const datei = abgleich('Datei-Abgleich (SHA-256)')
    expect(await within(datei).findByRole('img', { name: 'Treffer' })).toBeInTheDocument()
    expect(within(datei).getByRole('link', { name: 'RE-ANDERER' })).toHaveAttribute('href', `/belege/${b3}`)
    expect(within(abgleich('Fachlicher Abgleich')).getByRole('img', { name: 'Kein Treffer' })).toBeInTheDocument()
  })

  it('Markieren-Dialog (auch über ?markieren=1) setzt die Verknüpfung', async () => {
    // Der offene Dialog blendet den Kopf für Screenreader aus – direkt auf den Dialog warten.
    const { nutzer } = renderBelegDetail(`${pfad}?markieren=1`)
    const mitschnitt = anfragenMitschneiden()
    const dialog = await screen.findByRole('dialog', { name: 'Als Dublette markieren' }, { timeout: 5000 })
    const treffer = await within(dialog).findAllByRole('radio')
    expect(treffer.length).toBeGreaterThan(0)
    expect(within(dialog).queryByText('2026-48817')).not.toBeInTheDocument()
    await nutzer.click(treffer[0])
    await nutzer.click(within(dialog).getByRole('button', { name: 'Verknüpfen' }))
    expect(await screen.findByText(/^Als Dublette von .+ markiert$/)).toBeInTheDocument()
    const [anfrage] = await mitschnitt.anfragen()
    expect(anfrage).toMatchObject({ methode: 'POST', pfad: `/api/belege/${b2}/dublette` })
    expect((anfrage.body as { duplikatVonId: string }).duplikatVonId).toMatch(/^[0-9a-f-]{36}$/)
    expect(await screen.findByText(/^Dublette von/)).toBeInTheDocument()
  })

  it('„Verknüpfung aufheben“ setzt die Dublette zurück', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2
        ? { ...b, istDublette: true, dubletten: { ...b.dubletten, duplikatVon: { id: belegIds.b3, name: 'RE-1' } } }
        : b,
    )
    const { nutzer } = renderBelegDetail(pfad)
    await kopfGeladen()
    const mitschnitt = anfragenMitschneiden()
    await nutzer.click(await screen.findByRole('button', { name: 'Verknüpfung aufheben' }))
    expect(await screen.findByText('Verknüpfung aufgehoben')).toBeInTheDocument()
    const [anfrage] = await mitschnitt.anfragen()
    expect(anfrage.body).toEqual({ duplikatVonId: null })
  })

  it('zeigt NurLesen keinen Markieren-Knopf', async () => {
    szenario.rolle('NurLesen')
    renderBelegDetail(pfad)
    await kopfGeladen()
    await screen.findByText('Keine identische Datei im Archiv.')
    expect(screen.queryByRole('button', { name: 'Als Dublette markieren …' })).not.toBeInTheDocument()
  })
})
