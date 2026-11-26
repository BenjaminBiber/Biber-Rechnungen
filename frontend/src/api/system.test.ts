import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { benachrichtigungId } from '@/mocks/fixtures/ids'
import { useAufbewahrungLoeschen, useLoeschbar } from './aufbewahrung'
import {
  useAlleBenachrichtigungenGelesen,
  useBenachrichtigungen,
  useBenachrichtigungGelesen,
  useBenachrichtigungZaehler,
} from './benachrichtigungen'
import { useIntegritaetsLaeufe, useIntegritaetStarten } from './integritaet'
import { useSuche } from './suche'
import { erwarteDaten, renderMitQuery } from './testHilfe'
import { useTokenAnlegen, useTokens, useTokenWiderrufen } from './tokens'
import { useVerfahrensdoku, verfahrensdokuPdfUrl } from './verfahrensdoku'

describe('Benachrichtigungen', () => {
  it('Zähler zeigt drei ungelesene; „alle gelesen“ setzt ihn auf 0', async () => {
    const { result } = renderMitQuery(() => ({
      liste: useBenachrichtigungen(),
      zaehler: useBenachrichtigungZaehler(),
      alle: useAlleBenachrichtigungenGelesen(),
    }))
    expect((await erwarteDaten(() => result.current.liste)).gesamt).toBe(5)
    expect((await erwarteDaten(() => result.current.zaehler)).ungelesen).toBe(3)
    await act(() => result.current.alle.mutateAsync())
    await waitFor(() => expect(result.current.zaehler.data?.ungelesen).toBe(0))
  })

  it('useBenachrichtigungGelesen markiert eine', async () => {
    const { result } = renderMitQuery(() => ({
      ungelesen: useBenachrichtigungen({ nurUngelesen: true }),
      gelesen: useBenachrichtigungGelesen(),
    }))
    const [erste] = (await erwarteDaten(() => result.current.ungelesen)).eintraege
    await act(() => result.current.gelesen.mutateAsync(erste.id))
    await waitFor(() => expect(result.current.ungelesen.data?.gesamt).toBe(2))
    expect(erste.id).not.toBe(benachrichtigungId(99))
  })
})

describe('Aufbewahrung und Integrität', () => {
  it('useLoeschbar ist leer; Löschen eines nicht löschbaren Jahrgangs ergibt 409', async () => {
    const { result } = renderMitQuery(() => ({ loeschbar: useLoeschbar(), loeschen: useAufbewahrungLoeschen() }))
    expect(await erwarteDaten(() => result.current.loeschbar)).toEqual([])
    const fehler = await act(() =>
      result.current.loeschen.mutateAsync({ jahr: 2018, bestaetigung: 'LÖSCHEN' }).catch((e: unknown) => e),
    )
    expect(fehler).toMatchObject({ status: 409 })
  })

  it('useIntegritaetStarten fügt einen Lauf hinzu', async () => {
    const { result } = renderMitQuery(() => ({ laeufe: useIntegritaetsLaeufe(), starten: useIntegritaetStarten() }))
    const vorher = (await erwarteDaten(() => result.current.laeufe)).length
    await act(() => result.current.starten.mutateAsync())
    await waitFor(() => expect(result.current.laeufe.data).toHaveLength(vorher + 1))
  })
})

describe('Suche, Tokens, Verfahrensdoku', () => {
  it('useSuche findet Belege und Korrespondenten; leerer Begriff fragt nicht an', async () => {
    const { result } = renderMitQuery(() => ({ treffer: useSuche('Hostwerk'), leer: useSuche('  ') }))
    const ergebnis = await erwarteDaten(() => result.current.treffer)
    expect(ergebnis.belege).toHaveLength(2)
    expect(ergebnis.korrespondenten.map((k) => k.name)).toEqual(['Hostwerk Süd GmbH'])
    expect(result.current.leer.fetchStatus).toBe('idle')
  })

  it('Tokens: drei vorhanden, anlegen liefert Klartext, widerrufen setzt Datum', async () => {
    const { result } = renderMitQuery(() => ({
      tokens: useTokens(),
      anlegen: useTokenAnlegen(),
      widerrufen: useTokenWiderrufen(),
    }))
    expect(await erwarteDaten(() => result.current.tokens)).toHaveLength(3)
    const erstellt = await act(() => result.current.anlegen.mutateAsync({ name: 'Scanner', scopes: ['upload'] }))
    expect(erstellt.klartext.endsWith(erstellt.token.endung)).toBe(true)
    await act(() => result.current.widerrufen.mutateAsync(erstellt.token.id))
    await waitFor(() =>
      expect(result.current.tokens.data?.find((t) => t.id === erstellt.token.id)?.widerrufenAm).toBeTruthy(),
    )
  })

  it('useVerfahrensdoku und PDF-URL', async () => {
    const { result } = renderMitQuery(() => useVerfahrensdoku())
    expect((await erwarteDaten(() => result.current)).version).toBeTruthy()
    expect(verfahrensdokuPdfUrl()).toBe('/api/verfahrensdoku/pdf')
  })
})
