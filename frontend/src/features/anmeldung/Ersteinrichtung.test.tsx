import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { szenario } from '@/mocks/szenarien'
import { renderAnmeldung } from './testHilfe'

describe('Ersteinrichtung', () => {
  it('zeigt QR-Code und Schlüssel in Vierergruppen', async () => {
    szenario.nichtEingerichtet()
    renderAnmeldung()
    expect(await screen.findByRole('img', { name: 'QR-Code für die Authenticator-App' })).toBeInTheDocument()
    expect(screen.getByTestId('totp-schluessel')).toHaveTextContent('JBSW Y3DP EHPK 3PXP')
    expect(screen.getByText('Ersteinrichtung')).toBeInTheDocument()
  })

  it('legt den Admin an und führt zum Dashboard', async () => {
    szenario.nichtEingerichtet()
    const { user, router } = renderAnmeldung()
    await screen.findByRole('img', { name: 'QR-Code für die Authenticator-App' })
    await user.type(screen.getByLabelText('Name'), 'Martin Keller')
    await user.type(screen.getByLabelText('E-Mail-Adresse'), 'm.keller@firma.de')
    await user.type(screen.getByLabelText('Passwort'), 'einlangespasswort')
    await user.click(screen.getByRole('button', { name: 'Konto anlegen und fortfahren' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(db.eingerichtet).toBe(true)
  })
})
