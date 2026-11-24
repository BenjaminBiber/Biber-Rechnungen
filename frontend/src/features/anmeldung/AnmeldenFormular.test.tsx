import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { mockZugang } from '@/mocks/fixtures/auth'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { renderAnmeldung } from './testHilfe'

async function anmelden(email: string, passwort: string, pfad?: string) {
  const app = renderAnmeldung(pfad)
  await app.user.type(await screen.findByLabelText('E-Mail-Adresse'), email)
  await app.user.type(screen.getByLabelText('Passwort'), passwort)
  await app.user.click(screen.getByRole('button', { name: 'Weiter' }))
  return app
}

describe('AnmeldenFormular', () => {
  it('Ok führt zum Dashboard', async () => {
    szenario.abgemeldet()
    db.benutzer = { ...db.benutzer, totpAktiv: false }
    const { router } = await anmelden(mockZugang.email, mockZugang.passwort)
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('TotpErforderlich wechselt zum Schritt totp', async () => {
    szenario.abgemeldet()
    const { router } = await anmelden(mockZugang.email, mockZugang.passwort)
    expect(await screen.findByRole('heading', { name: 'Bestätigungscode' })).toBeInTheDocument()
    expect(router.state.location.search).toMatchObject({ schritt: 'totp' })
  })

  it('WartetAufFreigabe zeigt den Freigabe-Hinweis mit E-Mail', async () => {
    szenario.abgemeldet()
    await anmelden('j.wagner@firma.de', 'irgendein-passwort')
    expect(await screen.findByRole('heading', { name: 'Konto wartet auf Freigabe' })).toBeInTheDocument()
    expect(screen.getByText('j.wagner@firma.de')).toBeInTheDocument()
  })

  it('Gesperrt zeigt die Sperrmeldung', async () => {
    szenario.abgemeldet()
    server.use(http.post('*/api/auth/anmelden', () => HttpResponse.json({ status: 'Gesperrt' })))
    await anmelden(mockZugang.email, mockZugang.passwort)
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Zu viele Fehlversuche. Bitte in 10 Minuten erneut versuchen.',
    )
  })

  it('Ungueltig zeigt „E-Mail-Adresse oder Passwort ist falsch.“', async () => {
    szenario.abgemeldet()
    await anmelden(mockZugang.email, 'falsch')
    expect(await screen.findByRole('alert')).toHaveTextContent('E-Mail-Adresse oder Passwort ist falsch.')
  })

  it('prüft die E-Mail vor dem Absenden', async () => {
    szenario.abgemeldet()
    await anmelden('kein-mail', 'x')
    const feld = screen.getByLabelText('E-Mail-Adresse')
    expect(feld).toHaveAttribute('aria-invalid', 'true')
    expect(feld).toHaveAccessibleDescription('Bitte eine gültige E-Mail-Adresse eingeben.')
  })

  it('„Vergessen?“ zeigt den Hinweis auf den Admin', async () => {
    szenario.abgemeldet()
    const { user } = renderAnmeldung()
    await user.click(await screen.findByRole('button', { name: 'Vergessen?' }))
    expect(screen.getByText(/Wende dich an einen Admin/)).toBeInTheDocument()
  })

  it('zeigt Google und Registrieren nur, wenn im Status aktiv', async () => {
    szenario.abgemeldet()
    renderAnmeldung()
    expect(await screen.findByRole('link', { name: 'Mit Google anmelden' })).toHaveAttribute(
      'href',
      '/api/auth/google/start',
    )
    expect(screen.getByRole('button', { name: 'Registrieren' })).toBeInTheDocument()
  })

  it('blendet Google und Registrieren aus, wenn beides aus ist', async () => {
    szenario.abgemeldet()
    db.einstellungen.zugriff.googleAnmeldung = false
    db.einstellungen.zugriff.offeneRegistrierung = false
    renderAnmeldung()
    await screen.findByRole('heading', { name: 'Anmelden' })
    expect(screen.queryByRole('link', { name: 'Mit Google anmelden' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrieren' })).not.toBeInTheDocument()
  })

  it('fehler=google-unbekannt zeigt die Meldung', async () => {
    szenario.abgemeldet()
    renderAnmeldung('/anmelden?fehler=google-unbekannt')
    expect(await screen.findByRole('alert')).toHaveTextContent('Dieses Google-Konto ist keinem Benutzer zugeordnet.')
  })
})

describe('Route /anmelden', () => {
  it('leitet angemeldete Benutzer nach /', async () => {
    const { router } = renderAnmeldung()
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('erzwingt ohne Einrichtung die Ersteinrichtung', async () => {
    szenario.nichtEingerichtet()
    renderAnmeldung('/anmelden?schritt=totp')
    expect(await screen.findByRole('heading', { name: 'Admin-Konto anlegen' })).toBeInTheDocument()
  })

  it('leitet eingerichtet von der Ersteinrichtung zur Anmeldung', async () => {
    szenario.abgemeldet()
    renderAnmeldung('/anmelden?schritt=ersteinrichtung')
    expect(await screen.findByRole('heading', { name: 'Anmelden' })).toBeInTheDocument()
  })
})
