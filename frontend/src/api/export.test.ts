import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { exportDateiUrl, useExportErzeugen, useExportLaeufe, useExportVorschau } from './export'
import { erwarteDaten, renderMitQuery } from './testHilfe'
import type { ExportAnfrage } from './typen'

const q3: ExportAnfrage = {
  von: '2026-07-01',
  bis: '2026-09-30',
  bezeichnung: 'Q3 2026',
  belegliste: true,
  beleglisteFormat: 'xlsx',
  summenJeKategorie: true,
  vorsteuer: true,
  zipOriginale: true,
  kategorieIds: [],
  korrespondentIds: [],
  tagIds: [],
  dublettenAusschliessen: true,
  zuPruefenAusschliessen: false,
}

describe('Export-Hooks', () => {
  it('useExportVorschau liefert die Vorschau des Designs für Q3', async () => {
    const { result } = renderMitQuery(() => useExportVorschau(q3))
    const vorschau = await erwarteDaten(() => result.current)
    expect(vorschau.kategorien).toHaveLength(6)
    expect(vorschau.anzahlBelege).toBeGreaterThan(0)
  })

  it('useExportVorschau fragt ohne Anfrage nicht an', () => {
    const { result } = renderMitQuery(() => useExportVorschau(undefined))
    expect(result.current.fetchStatus).toBe('idle')
  })

  it('useExportErzeugen legt einen Lauf an, der in useExportLaeufe erscheint', async () => {
    const { result } = renderMitQuery(() => ({ laeufe: useExportLaeufe(), erzeugen: useExportErzeugen() }))
    const vorher = (await erwarteDaten(() => result.current.laeufe)).length
    const lauf = await act(() => result.current.erzeugen.mutateAsync(q3))
    expect(lauf.downloadUrl).toBe(exportDateiUrl(lauf.id))
    await waitFor(() => expect(result.current.laeufe.data).toHaveLength(vorher + 1))
    expect(result.current.laeufe.data?.[0].id).toBe(lauf.id)
  })
})
