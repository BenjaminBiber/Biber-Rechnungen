import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { szenario } from '@/mocks/szenarien'
import { passwortStaerke } from './RegistrierenFormular'
import { renderAnmeldung } from './testHilfe'

describe('passwortStaerke', () => {
  it('bewertet in vier Stufen, unter 12 Zeichen zu kurz', () => {
    expect(passwortStaerke('kurz')).toBe(0)
    expect(passwortStaerke('abcdefghijkl')).toBe(1)
    expect(passwortStaerke('abcdefghijklmn')).toBe(2)
    expect(passwortStaerke('einlangespasswort')).toBe(3)
    expect(passwortStaerke('Ein-langes-Passwort-7')).toBe(4)
  })
})

describe('RegistrierenFormular', () => {
  it('zeigt die Stärke und wechselt nach dem Registrieren zur Freigabe', async () => {
    szenario.abgemeldet()
    const { user, router } = renderAnmeldung('/anmelden?schritt=registrieren')
    await user.type(await screen.findByLabelText('Name'), 'Jonas Wagner')
    await user.type(screen.getByLabelText('E-Mail-Adresse'), 'jonas.neu@firma.de')
    await user.type(screen.getByLabelText('Passwort'), 'kurz')
    expect(screen.getByText('Zu kurz – mindestens 12 Zeichen')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Passwort'), 'einlangespasswort')
    expect(screen.getByText('Starkes Passwort')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Konto erstellen' }))
    expect(await screen.findByRole('heading', { name: 'Konto wartet auf Freigabe' })).toBeInTheDocument()
    expect(screen.getByText('jonas.neu@firma.de')).toBeInTheDocument()
    expect(router.state.location.search).toMatchObject({ schritt: 'freigabe' })
  })

  it('prüft Name und Passwortlänge vor dem Absenden', async () => {
    szenario.abgemeldet()
    const { user } = renderAnmeldung('/anmelden?schritt=registrieren')
    await user.type(await screen.findByLabelText('E-Mail-Adresse'), 'a@b.de')
    await user.type(screen.getByLabelText('Passwort'), 'kurz')
    await user.click(screen.getByRole('button', { name: 'Konto erstellen' }))
    expect(screen.getByLabelText('Name')).toHaveAccessibleDescription('Bitte einen Namen eingeben.')
    expect(screen.getByLabelText('Passwort')).toHaveAccessibleDescription('Mindestens 12 Zeichen.')
  })

  it('zeigt Serverfehler am Feld', async () => {
    szenario.abgemeldet()
    const { user } = renderAnmeldung('/anmelden?schritt=registrieren')
    await user.type(await screen.findByLabelText('Name'), 'Martin')
    await user.type(screen.getByLabelText('E-Mail-Adresse'), 'm.keller@firma.de')
    await user.type(screen.getByLabelText('Passwort'), 'ein-langes-passwort')
    await user.click(screen.getByRole('button', { name: 'Konto erstellen' }))
    expect(await screen.findByText('E-Mail ist bereits vergeben.')).toBeInTheDocument()
    expect(screen.getByLabelText('E-Mail-Adresse')).toHaveAccessibleDescription('E-Mail ist bereits vergeben.')
  })

  it('„Mit Google registrieren“ nur bei aktiver Google-Anmeldung', async () => {
    szenario.abgemeldet()
    db.einstellungen.zugriff.googleAnmeldung = false
    renderAnmeldung('/anmelden?schritt=registrieren')
    await screen.findByRole('heading', { name: 'Konto erstellen' })
    expect(screen.queryByRole('link', { name: 'Mit Google registrieren' })).not.toBeInTheDocument()
  })
})
