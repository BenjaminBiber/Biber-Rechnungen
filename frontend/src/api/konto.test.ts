import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { mockZugang } from '@/mocks/fixtures/auth'
import { useAuthStatus } from './auth'
import { useKonto, useKontoAendern, usePasswortAendern, useTotpDeaktivieren } from './konto'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Konto-Hooks', () => {
  it('useKonto liefert das Konto von Martin Keller', async () => {
    const { result } = renderMitQuery(() => useKonto())
    expect(await erwarteDaten(() => result.current)).toMatchObject({ name: 'Martin Keller', totpAktiv: true })
  })

  it('useKontoAendern ändert Name und lädt Konto und Anmeldestatus neu', async () => {
    const { result } = renderMitQuery(() => ({
      konto: useKonto(),
      status: useAuthStatus(),
      aendern: useKontoAendern(),
    }))
    await erwarteDaten(() => result.current.status)
    await act(() => result.current.aendern.mutateAsync({ name: 'Martin K. Keller', email: 'm.keller@firma.de' }))
    await waitFor(() => expect(result.current.status.data?.benutzer?.name).toBe('Martin K. Keller'))
    expect(result.current.konto.data?.name).toBe('Martin K. Keller')
  })

  it('usePasswortAendern meldet ein falsches altes Passwort als Feldfehler', async () => {
    const { result } = renderMitQuery(() => usePasswortAendern())
    const fehler = await act(() =>
      result.current
        .mutateAsync({ altesPasswort: 'falsch', neuesPasswort: 'neues-passwort-1' })
        .catch((e: unknown) => e),
    )
    expect(fehler).toMatchObject({ status: 400, fehler: { altesPasswort: expect.any(Array) } })
    await act(() =>
      result.current.mutateAsync({ altesPasswort: mockZugang.passwort, neuesPasswort: 'neues-passwort-1' }),
    )
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
  })

  it('useTotpDeaktivieren schaltet 2FA ab', async () => {
    const { result } = renderMitQuery(() => ({ konto: useKonto(), aus: useTotpDeaktivieren() }))
    await erwarteDaten(() => result.current.konto)
    await act(() => result.current.aus.mutateAsync())
    await waitFor(() => expect(result.current.konto.data?.totpAktiv).toBe(false))
  })
})
