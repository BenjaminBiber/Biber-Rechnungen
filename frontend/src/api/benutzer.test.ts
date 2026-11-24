import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import { useBenutzer, useBenutzerAnlegen, useBenutzerFreigeben } from './benutzer'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Benutzer-Hooks', () => {
  it('useBenutzer liefert die vier Benutzer, Martin Keller zuerst', async () => {
    const { result } = renderMitQuery(() => useBenutzer())
    const liste = await erwarteDaten(() => result.current)
    expect(liste).toHaveLength(4)
    expect(liste[0].name).toBe('Martin Keller')
  })

  it('useBenutzerFreigeben gibt eine wartende Registrierung mit Rolle frei', async () => {
    const { result } = renderMitQuery(() => ({ liste: useBenutzer(), freigeben: useBenutzerFreigeben() }))
    const wartend = (await erwarteDaten(() => result.current.liste)).find((b) => !b.freigegeben)!
    expect(wartend).toBeDefined()
    await act(() => result.current.freigeben.mutateAsync({ id: wartend.id, rolle: 'Benutzer' }))
    await waitFor(() =>
      expect(result.current.liste.data?.find((b) => b.id === wartend.id)).toMatchObject({
        freigegeben: true,
        rolle: 'Benutzer',
      }),
    )
  })

  it('useBenutzerAnlegen ist nur für Admins erlaubt', async () => {
    szenario.rolle('Benutzer')
    const { result } = renderMitQuery(() => useBenutzerAnlegen())
    const fehler = await act(() =>
      result.current
        .mutateAsync({ name: 'Neu', email: 'neu@firma.de', passwort: 'lang-genug-123', rolle: 'NurLesen' })
        .catch((e: unknown) => e),
    )
    expect(fehler).toMatchObject({ status: 403 })
  })
})
