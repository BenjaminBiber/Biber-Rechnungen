import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockZugang } from '@/mocks/fixtures/auth'
import { szenario } from '@/mocks/szenarien'
import { googleStartUrl, useAnmelden, useAuthStatus, useErsteinrichtung, useTotp, useTotpVorschlag } from './auth'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Auth-Hooks', () => {
  it('useAuthStatus meldet Martin Keller als angemeldeten Admin mit 2FA', async () => {
    const { result } = renderMitQuery(() => useAuthStatus())
    const status = await erwarteDaten(() => result.current)
    expect(status).toMatchObject({ eingerichtet: true, angemeldet: true })
    expect(status.benutzer).toMatchObject({ name: 'Martin Keller', rolle: 'Admin', totpAktiv: true })
  })

  it('Anmelden mit Passwort und TOTP-Code macht die Sitzung angemeldet', async () => {
    szenario.abgemeldet()
    const { result, queryClient } = renderMitQuery(() => ({
      status: useAuthStatus(),
      anmelden: useAnmelden(),
      totp: useTotp(),
    }))
    expect((await erwarteDaten(() => result.current.status)).angemeldet).toBe(false)

    let ergebnis = await act(() =>
      result.current.anmelden.mutateAsync({
        email: mockZugang.email,
        passwort: mockZugang.passwort,
        angemeldetBleiben: true,
      }),
    )
    expect(ergebnis.status).toBe('TotpErforderlich')

    ergebnis = await act(() => result.current.totp.mutateAsync({ code: mockZugang.totpCode, geraetVertrauen: false }))
    expect(ergebnis.status).toBe('Ok')
    // Die Mutation lädt den Status neu.
    await waitFor(() => expect(result.current.status.data?.angemeldet).toBe(true))
    expect(queryClient.isMutating()).toBe(0)
  })

  it('falsches Passwort ergibt „Ungueltig“', async () => {
    szenario.abgemeldet()
    const { result } = renderMitQuery(() => useAnmelden())
    const ergebnis = await act(() =>
      result.current.mutateAsync({ email: mockZugang.email, passwort: 'falsch', angemeldetBleiben: false }),
    )
    expect(ergebnis.status).toBe('Ungueltig')
  })

  it('Ersteinrichtung: Vorschlag laden und ersten Admin anlegen', async () => {
    szenario.nichtEingerichtet()
    const { result } = renderMitQuery(() => ({ vorschlag: useTotpVorschlag(), einrichten: useErsteinrichtung() }))
    const vorschlag = await erwarteDaten(() => result.current.vorschlag)
    const benutzer = await act(() =>
      result.current.einrichten.mutateAsync({
        name: 'Martin Keller',
        email: 'm.keller@firma.de',
        passwort: 'sehr-geheim-123',
        totpSchluessel: vorschlag.schluessel,
      }),
    )
    expect(benutzer).toMatchObject({ name: 'Martin Keller', rolle: 'Admin' })
  })

  it('googleStartUrl baut die Weiterleitungs-URL', () => {
    expect(googleStartUrl()).toBe('/api/auth/google/start')
    expect(googleStartUrl(true)).toBe('/api/auth/google/start?verknuepfen=true')
  })
})
