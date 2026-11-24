import { act, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import { belegIds } from '@/mocks/fixtures/ids'
import { useBeleg, useBelegAendern } from './belege'
import { anmeldeWeiterleitung, ApiFehler, istApiFehler, zuApiFehler } from './client'
import { useKonto } from './konto'
import { renderMitQuery } from './testHilfe'

afterEach(() => window.history.replaceState(null, '', '/'))

describe('ApiFehler', () => {
  it('bildet ProblemDetails mit Validierungsfehlern ab', () => {
    const fehler = zuApiFehler(400, { title: 'Die Eingabe ist ungültig.', errors: { titel: ['Pflichtfeld'] } })
    expect(fehler).toBeInstanceOf(ApiFehler)
    expect(fehler).toMatchObject({
      status: 400,
      titel: 'Die Eingabe ist ungültig.',
      fehler: { titel: ['Pflichtfeld'] },
    })
  })

  it('fällt ohne ProblemDetails auf Text, Statustext oder Status zurück', () => {
    expect(zuApiFehler(502, 'Bad Gateway vom Proxy').titel).toBe('Bad Gateway vom Proxy')
    expect(zuApiFehler(503, undefined, 'Service Unavailable').titel).toBe('Service Unavailable')
    expect(zuApiFehler(500, undefined).titel).toBe('Fehler 500')
  })

  it('wirft bei Fehlerantwort einer Query einen ApiFehler mit Status', async () => {
    szenario.fehler('/api/belege/{id}', 404)
    const { result } = renderMitQuery(() => useBeleg(belegIds.b1))
    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(istApiFehler(result.current.error)).toBe(true)
    expect(result.current.error).toMatchObject({ status: 404 })
  })

  it('liefert Validierungsfehler einer Mutation unter `fehler`', async () => {
    const { result } = renderMitQuery(() => useBelegAendern())
    const fehler = await act(() =>
      result.current
        .mutateAsync({ id: belegIds.b1, titel: '', belegart: 'Rechnung', waehrung: 'EUR', steuerzeilen: [] })
        .catch((e: unknown) => e),
    )
    expect(fehler).toBeInstanceOf(ApiFehler)
    expect(fehler).toMatchObject({ status: 400, fehler: { titel: ['Titel ist erforderlich.'] } })
    await waitFor(() => expect(result.current.error).toBe(fehler))
  })
})

describe('Weiterleitung bei 401', () => {
  it('leitet ohne Sitzung nach /anmelden weiter', async () => {
    const weiter = vi.spyOn(anmeldeWeiterleitung, 'ausfuehren').mockImplementation(() => {})
    szenario.abgemeldet()
    const { result } = renderMitQuery(() => useKonto())
    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.error).toMatchObject({ status: 401 })
    expect(weiter).toHaveBeenCalledTimes(1)
  })

  it('leitet auf /anmelden selbst nicht erneut weiter', async () => {
    window.history.replaceState(null, '', '/anmelden')
    const weiter = vi.spyOn(anmeldeWeiterleitung, 'ausfuehren').mockImplementation(() => {})
    szenario.abgemeldet()
    const { result } = renderMitQuery(() => useKonto())
    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(weiter).not.toHaveBeenCalled()
  })
})
