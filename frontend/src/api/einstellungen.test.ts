import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import {
  useDiFortsetzen,
  useDiVerbrauch,
  useDiVerbindungTesten,
  useEinstellungen,
  useEinstellungenSpeichern,
  useMailJetztAbrufen,
  useMailStatus,
} from './einstellungen'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Einstellungen-Hooks', () => {
  it('useEinstellungen liefert alle Gruppen', async () => {
    const { result } = renderMitQuery(() => useEinstellungen())
    const werte = await erwarteDaten(() => result.current)
    expect(Object.keys(werte)).toEqual(
      expect.arrayContaining([
        'allgemein',
        'ust',
        'pruefungen',
        'di',
        'mail',
        'ordner',
        'aufbewahrung',
        'zugriff',
        'umgebung',
      ]),
    )
  })

  it('useEinstellungenSpeichern speichert eine Gruppe und prüft die Aufbewahrungsfrist', async () => {
    const { result } = renderMitQuery(() => ({ werte: useEinstellungen(), speichern: useEinstellungenSpeichern() }))
    await erwarteDaten(() => result.current.werte)
    await act(() => result.current.speichern.mutateAsync({ gruppe: 'aufbewahrung', werte: { jahre: 10 } }))
    await waitFor(() => expect(result.current.werte.data?.aufbewahrung.jahre).toBe(10))

    const fehler = await act(() =>
      result.current.speichern.mutateAsync({ gruppe: 'aufbewahrung', werte: { jahre: 5 } }).catch((e: unknown) => e),
    )
    expect(fehler).toMatchObject({ status: 400, fehler: { jahre: expect.any(Array) } })
  })

  it('useEinstellungenSpeichern ist nur für Admins erlaubt', async () => {
    szenario.rolle('Benutzer')
    const { result } = renderMitQuery(() => useEinstellungenSpeichern())
    const fehler = await act(() =>
      result.current
        .mutateAsync({ gruppe: 'zugriff', werte: { offeneRegistrierung: false, googleAnmeldung: false } })
        .catch((e: unknown) => e),
    )
    expect(fehler).toMatchObject({ status: 403 })
  })

  it('Document Intelligence: Verbrauch, Verbindungstest, Fortsetzen', async () => {
    const { result } = renderMitQuery(() => ({
      verbrauch: useDiVerbrauch(),
      testen: useDiVerbindungTesten(),
      fortsetzen: useDiFortsetzen(),
    }))
    expect((await erwarteDaten(() => result.current.verbrauch)).limit).toBeGreaterThan(0)
    expect((await act(() => result.current.testen.mutateAsync())).erfolg).toBe(true)
    await act(() => result.current.fortsetzen.mutateAsync())
    await waitFor(() => expect(result.current.verbrauch.data?.pausiert).toBe(false))
  })

  it('Mail: Status und jetzt abrufen', async () => {
    const { result } = renderMitQuery(() => ({ status: useMailStatus(), abrufen: useMailJetztAbrufen() }))
    const vorher = await erwarteDaten(() => result.current.status)
    await act(() => result.current.abrufen.mutateAsync())
    await waitFor(() => expect(result.current.status.data?.letzterAbruf).not.toBe(vorher.letzterAbruf))
  })
})
