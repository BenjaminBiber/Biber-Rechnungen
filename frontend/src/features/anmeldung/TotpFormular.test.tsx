import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { mockZugang } from '@/mocks/fixtures/auth'
import { szenario } from '@/mocks/szenarien'
import { renderAnmeldung } from './testHilfe'

function totpSchritt() {
  szenario.abgemeldet()
  db.wartetAufTotp = true
  return renderAnmeldung('/anmelden?schritt=totp')
}

const ziffern = () => [1, 2, 3, 4, 5, 6].map((n) => screen.getByLabelText(`Ziffer ${n}`))

describe('TotpFormular', () => {
  it('springt beim Tippen weiter und meldet mit gültigem Code an', async () => {
    const { user, router } = totpSchritt()
    await user.click(await screen.findByLabelText('Ziffer 1'))
    await user.keyboard(mockZugang.totpCode)
    expect(ziffern().map((f) => (f as HTMLInputElement).value)).toEqual(mockZugang.totpCode.split(''))
    expect(ziffern()[0]).toHaveAttribute('inputmode', 'numeric')
    expect(ziffern()[0]).toHaveAttribute('autocomplete', 'one-time-code')
    await user.click(screen.getByRole('button', { name: 'Anmelden' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('verteilt einen eingefügten Code auf alle Felder', async () => {
    const { user } = totpSchritt()
    await user.click(await screen.findByLabelText('Ziffer 3'))
    await user.paste('654 321')
    expect(ziffern().map((f) => (f as HTMLInputElement).value)).toEqual(['6', '5', '4', '3', '2', '1'])
  })

  it('Rücktaste im leeren Feld springt zurück und löscht', async () => {
    const { user } = totpSchritt()
    await user.click(await screen.findByLabelText('Ziffer 1'))
    await user.keyboard('12')
    expect(ziffern()[2]).toHaveFocus()
    await user.keyboard('{Backspace}')
    expect(ziffern()[1]).toHaveFocus()
    expect(ziffern()[1]).toHaveValue('')
  })

  it('falscher Code zeigt eine Fehlermeldung', async () => {
    const { user } = totpSchritt()
    await user.click(await screen.findByLabelText('Ziffer 1'))
    await user.keyboard('000000')
    await user.click(screen.getByRole('button', { name: 'Anmelden' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Der Code ist ungültig.')
  })

  it('meldet mit Wiederherstellungscode an', async () => {
    const { user, router } = totpSchritt()
    await user.click(await screen.findByRole('button', { name: 'Wiederherstellungscode verwenden' }))
    await user.type(screen.getByLabelText('Wiederherstellungscode'), mockZugang.wiederherstellungscode)
    await user.click(screen.getByRole('button', { name: 'Anmelden' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('„Zurück“ führt zur Anmeldung', async () => {
    const { user } = totpSchritt()
    await user.click(await screen.findByRole('button', { name: 'Zurück' }))
    expect(await screen.findByRole('heading', { name: 'Anmelden' })).toBeInTheDocument()
  })
})
