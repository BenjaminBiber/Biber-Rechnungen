import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { belegIds } from '@/mocks/fixtures/ids'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

const zahlung = () => screen.getByRole('region', { name: 'Zahlung' })

async function oeffne(pfad?: string, name?: string) {
  const ergebnis = renderBelegDetail(pfad)
  await kopfGeladen(name)
  await screen.findByRole('form', { name: 'Daten' })
  return ergebnis
}

describe('Bereich „Zahlung“', () => {
  it('erfasst Skonto mit Prozent und Frist und zeigt die Ersparnis', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffne()
    expect(within(zahlung()).getByText('Kein Skonto erkannt')).toBeInTheDocument()

    await nutzer.click(within(zahlung()).getByRole('button', { name: 'Skonto erfassen' }))
    await nutzer.type(within(zahlung()).getByLabelText('Skonto %'), '2,5')
    await nutzer.type(within(zahlung()).getByLabelText('Zahlung bis'), '10.12.2026')
    await nutzer.click(within(zahlung()).getByRole('button', { name: 'Speichern' }))

    await waitFor(() => expect(within(zahlung()).getByText(/2,5 % Skonto/)).toBeInTheDocument())
    expect(within(zahlung()).getByText(/spart 6,60/)).toBeInTheDocument()
    const put = (await anfragen()).find((a) => a.pfad === `/api/belege/${b2}/skonto`)
    expect(put?.body).toEqual({ prozent: 2.5, bis: '2026-12-10' })
  })

  it('lehnt einen ungültigen Prozentsatz im Browser ab', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffne()
    await nutzer.click(within(zahlung()).getByRole('button', { name: 'Skonto erfassen' }))
    await nutzer.type(within(zahlung()).getByLabelText('Skonto %'), 'zwei')
    await nutzer.click(within(zahlung()).getByRole('button', { name: 'Speichern' }))

    expect(within(zahlung()).getByRole('alert')).toHaveTextContent('Prozentsatz wie 2 oder 2,5 eingeben')
    expect((await anfragen()).some((a) => a.pfad.endsWith('/skonto'))).toBe(false)
  })

  it('ordnet eine Gutschrift einer Rechnung desselben Korrespondenten zu', async () => {
    const rechnung = db.belege.find((b) => b.id === b2)!
    const gutschriftId = belegIds.b3
    db.belege = db.belege.map((b) =>
      b.id === gutschriftId
        ? {
            ...b,
            belegart: 'Gutschrift',
            skonto: null,
            bezug: {
              rechnung: null,
              gutschriften: [],
              restbetrag: null,
              kandidaten: [
                {
                  id: rechnung.id,
                  rechnungsnummer: rechnung.rechnungsnummer ?? null,
                  rechnungsdatum: rechnung.rechnungsdatum ?? null,
                  brutto: rechnung.brutto ?? null,
                  waehrung: 'EUR',
                },
              ],
            },
          }
        : b,
    )
    const name = db.belege.find((b) => b.id === gutschriftId)!.korrespondent!.name
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffne(`/belege/${gutschriftId}`, name)

    const auswahl = within(zahlung()).getByLabelText('Bezieht sich auf Rechnung')
    await nutzer.selectOptions(auswahl, rechnung.id)

    await waitFor(async () =>
      expect((await anfragen()).find((a) => a.pfad === `/api/belege/${gutschriftId}/bezug`)?.body).toEqual({
        bezugBelegId: rechnung.id,
      }),
    )
    await waitFor(() => expect(within(zahlung()).getByRole('link', { name: 'Öffnen' })).toBeInTheDocument())
  })

  it('zeigt bei einer Rechnung die Gutschriften und den verbleibenden Betrag', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2
        ? {
            ...b,
            bezug: {
              rechnung: null,
              gutschriften: [
                {
                  id: belegIds.b3,
                  rechnungsnummer: 'GS-77',
                  rechnungsdatum: '2026-11-21',
                  brutto: -64,
                  waehrung: 'EUR',
                },
              ],
              restbetrag: 200,
              kandidaten: [],
            },
          }
        : b,
    )
    await oeffne()

    const gruppe = within(zahlung()).getByRole('group', { name: 'Gutschriften zu dieser Rechnung' })
    expect(within(gruppe).getByRole('link', { name: /GS-77 vom 21\.11\.2026/ })).toBeInTheDocument()
    expect(gruppe).toHaveTextContent('Verbleibender Betrag: 200,00 €')
  })
})
