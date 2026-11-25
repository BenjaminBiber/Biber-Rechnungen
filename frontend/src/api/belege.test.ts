import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { belegIds, tagIds } from '@/mocks/fixtures/ids'
import {
  belegDateiUrl,
  belegVisualisierungUrl,
  belegVorschauUrl,
  useAnsichten,
  useAnsichtLoeschen,
  useBeleg,
  useBelegBestaetigen,
  useBelege,
  useBelegHistorie,
  useBelegNachbarn,
  useBelegZaehler,
  useTagHinzufuegen,
} from './belege'
import { useDashboard } from './dashboard'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Belege-Hooks', () => {
  it('useBelege filtert nach Ansicht „Zu prüfen“', async () => {
    const { result } = renderMitQuery(() => useBelege({ ansicht: 'ZuPruefen' }))
    const liste = await erwarteDaten(() => result.current)
    expect(liste.gesamt).toBe(5)
    expect(liste.eintraege[0]).toMatchObject({ rechnungsnummer: '2026-48817', brutto: 264 })
  })

  it('useBelegBestaetigen entfernt „zu prüfen“ und aktualisiert Zähler, Detail und Dashboard', async () => {
    const { result } = renderMitQuery(() => ({
      zaehler: useBelegZaehler(),
      beleg: useBeleg(belegIds.b2),
      dashboard: useDashboard(),
      bestaetigen: useBelegBestaetigen(),
    }))
    expect((await erwarteDaten(() => result.current.zaehler)).zuPruefen).toBe(5)
    expect((await erwarteDaten(() => result.current.dashboard)).zuPruefenListe).toHaveLength(5)
    await erwarteDaten(() => result.current.beleg)

    await act(() => result.current.bestaetigen.mutateAsync({ id: belegIds.b2, begruendung: null }))

    await waitFor(() => expect(result.current.zaehler.data?.zuPruefen).toBe(4))
    await waitFor(() => expect(result.current.beleg.data?.tags.map((t) => t.name)).toEqual(['Inbox']))
    await waitFor(() => expect(result.current.dashboard.data?.zuPruefenListe).toHaveLength(4))
  })

  it('useTagHinzufuegen setzt „bezahlt“ – der Beleg ist danach nicht mehr überfällig', async () => {
    const { result } = renderMitQuery(() => ({ beleg: useBeleg(belegIds.b9), tag: useTagHinzufuegen() }))
    expect((await erwarteDaten(() => result.current.beleg)).ueberfaellig).toBe(true)
    await act(() => result.current.tag.mutateAsync({ id: belegIds.b9, tagId: tagIds.bez }))
    await waitFor(() => expect(result.current.beleg.data?.ueberfaellig).toBe(false))
  })

  it('useBelegNachbarn und useBelegHistorie', async () => {
    const { result } = renderMitQuery(() => ({
      nachbarn: useBelegNachbarn(belegIds.b2, { ansicht: 'ZuPruefen' }),
      historie: useBelegHistorie(belegIds.b2),
    }))
    expect(await erwarteDaten(() => result.current.nachbarn)).toEqual({
      vorherId: null,
      naechsteId: belegIds.b5,
      position: 1,
      gesamt: 5,
    })
    expect((await erwarteDaten(() => result.current.historie))[0]).toMatchObject({
      wer: 'A. Lehmann',
      text: 'Kategorie geändert',
    })
  })

  it('useAnsichtLoeschen entfernt eine gespeicherte Ansicht', async () => {
    const { result } = renderMitQuery(() => ({ ansichten: useAnsichten(), loeschen: useAnsichtLoeschen() }))
    const [erste] = await erwarteDaten(() => result.current.ansichten)
    await act(() => result.current.loeschen.mutateAsync(erste.id))
    await waitFor(() => expect(result.current.ansichten.data?.some((a) => a.id === erste.id)).toBe(false))
  })

  it('baut die Datei-URLs', () => {
    expect(belegDateiUrl('b', 'd')).toBe('/api/belege/b/dateien/d')
    expect(belegDateiUrl('b', 'd', true)).toBe('/api/belege/b/dateien/d?inline=true')
    expect(belegVorschauUrl('b', 2)).toBe('/api/belege/b/vorschau?seite=2')
    expect(belegVorschauUrl('b', 2, 1200)).toBe('/api/belege/b/vorschau?seite=2&breite=1200')
    expect(belegVisualisierungUrl('b')).toBe('/api/belege/b/visualisierung')
  })
})
