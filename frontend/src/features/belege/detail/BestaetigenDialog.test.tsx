import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

const kopfChips = () => screen.getByRole('heading', { level: 1 }).parentElement!

describe('Bestätigen', () => {
  it('bestätigt ohne fehlgeschlagene Befunde sofort', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2 ? { ...b, befunde: b.befunde.filter((x) => x.ergebnis !== 'Fehlgeschlagen') } : b,
    )
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Bestätigen' }))

    expect(await screen.findByText('Beleg bestätigt')).toBeInTheDocument()
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect((await anfragen()).map((a) => [a.methode, a.pfad, a.body])).toEqual([
      ['POST', `/api/belege/${b2}/bestaetigen`, {}],
    ])
    await waitFor(() => expect(within(kopfChips()).queryByText('zu prüfen')).not.toBeInTheDocument())
    expect(screen.queryByRole('button', { name: 'Bestätigen' })).not.toBeInTheDocument()
  })

  it('fragt bei offenen Befunden nach und sendet die Begründung', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Bestätigen' }))

    const dialog = await screen.findByRole('alertdialog', { name: 'Beleg mit 2 offenen Befunden bestätigen?' })
    const befunde = within(dialog).getAllByRole('listitem')
    expect(befunde.map((b) => b.textContent)).toEqual([
      'Positionen 219,85 € statt 221,85 € · Differenz 2,00 €, Toleranz 0,02 €',
      'Konfidenz 0,74 liegt unter der Schwelle 0,80',
    ])
    expect(dialog).toHaveTextContent(
      '„Zu prüfen“ wird entfernt. Befunde und deine Bestätigung bleiben in der Historie.',
    )

    await nutzer.type(
      within(dialog).getByLabelText('Begründung (optional, wird protokolliert)'),
      'Versandkostenpauschale fehlt in den Positionen',
    )
    await nutzer.click(within(dialog).getByRole('button', { name: 'Trotzdem bestätigen' }))

    expect(await screen.findByText('Beleg bestätigt')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect((await anfragen()).map((a) => [a.methode, a.pfad, a.body])).toEqual([
      ['POST', `/api/belege/${b2}/bestaetigen`, { begruendung: 'Versandkostenpauschale fehlt in den Positionen' }],
    ])
    await waitFor(() => expect(within(kopfChips()).queryByText('zu prüfen')).not.toBeInTheDocument())
  })

  it('„Zurück zum Beleg“ schließt den Dialog ohne Anfrage', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Bestätigen' }))
    const dialog = await screen.findByRole('alertdialog')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Zurück zum Beleg' }))
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(await anfragen()).toEqual([])
    expect(within(kopfChips()).getByText('zu prüfen')).toBeInTheDocument()
  })
})
