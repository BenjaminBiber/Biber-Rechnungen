import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { importVorgangId } from '@/mocks/fixtures/ids'
import { laeuftNoch, useImportVorgaenge, useImportVorgang, useUpload } from './import'
import { erwarteDaten, nodeFormDataVerwenden, renderMitQuery } from './testHilfe'

describe('Import-Hooks', () => {
  it('useImportVorgaenge liefert die sieben Vorgänge des Designs, neueste zuerst', async () => {
    const { result } = renderMitQuery(() => useImportVorgaenge())
    const seite = await erwarteDaten(() => result.current)
    expect(seite.gesamt).toBe(7)
    expect(seite.eintraege[0].dateiname).toBe('Rechnung_Okt_Hosting.pdf')
    expect(seite.eintraege.some(laeuftNoch)).toBe(true)
  })

  it('useImportVorgaenge filtert nach Quelle', async () => {
    const { result } = renderMitQuery(() => useImportVorgaenge({ quelle: 'Mail' }))
    const seite = await erwarteDaten(() => result.current)
    expect(seite.eintraege.length).toBeGreaterThan(0)
    expect(seite.eintraege.every((v) => v.quelle === 'Mail')).toBe(true)
  })

  it('useImportVorgang liefert einen Vorgang', async () => {
    const { result } = renderMitQuery(() => useImportVorgang(importVorgangId(2)))
    expect(await erwarteDaten(() => result.current)).toMatchObject({ quelle: 'Mail', status: 'InVerarbeitung' })
  })

  it('useUpload lädt per XMLHttpRequest hoch, meldet Fortschritt und legt Vorgänge an', async () => {
    await nodeFormDataVerwenden()
    const { result } = renderMitQuery(() => ({ upload: useUpload(), liste: useImportVorgaenge() }))
    await erwarteDaten(() => result.current.liste)
    const datei = new File(['%PDF-1.4 Test'], 'Rechnung_Test.pdf', { type: 'application/pdf' })

    const vorgaenge = await act(() => result.current.upload.mutateAsync([datei]))

    expect(vorgaenge).toHaveLength(1)
    expect(vorgaenge[0]).toMatchObject({ dateiname: 'Rechnung_Test.pdf', quelle: 'Upload', status: 'Angenommen' })
    expect(result.current.upload.fortschritt).toEqual([100])
    await waitFor(() => expect(result.current.liste.data?.gesamt).toBe(8))
  })
})
