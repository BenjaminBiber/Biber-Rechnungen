import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { tagIds } from '@/mocks/fixtures/ids'
import {
  useAutoMatchingStatus,
  useKategorieAnlegen,
  useKategorien,
  useMatchingTesten,
  useTagAnlegen,
  useTagLoeschen,
  useTags,
} from './tags'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Tags-, Kategorien- und Matching-Hooks', () => {
  it('useTags und useKategorien liefern sieben Tags und sechs Kategorien', async () => {
    const { result } = renderMitQuery(() => ({ tags: useTags(), kategorien: useKategorien(2026) }))
    expect((await erwarteDaten(() => result.current.tags)).map((t) => t.name)).toEqual([
      'Inbox',
      'zu prüfen',
      'bezahlt',
      'bezahlt PayPal',
      'Lastschrift',
      'Anlage / AfA',
      'Reisekosten',
    ])
    expect(await erwarteDaten(() => result.current.kategorien)).toHaveLength(6)
  })

  it('useTagAnlegen und useKategorieAnlegen legen an', async () => {
    const { result } = renderMitQuery(() => ({
      tags: useTags(),
      kategorien: useKategorien(),
      tag: useTagAnlegen(),
      kategorie: useKategorieAnlegen(),
    }))
    await erwarteDaten(() => result.current.tags)
    await erwarteDaten(() => result.current.kategorien)
    const keins = { algorithmus: 'Keiner' as const, muster: null, grossKlein: false }
    await act(() =>
      result.current.tag.mutateAsync({
        name: 'Bewirtung',
        farbe: 'lila',
        istInbox: false,
        istZahlungsTag: false,
        matching: keins,
      }),
    )
    await act(() =>
      result.current.kategorie.mutateAsync({ name: 'Reise', farbe: '#D64541', euerZeile: null, matching: keins }),
    )
    await waitFor(() => expect(result.current.tags.data).toHaveLength(8))
    await waitFor(() => expect(result.current.kategorien.data).toHaveLength(7))
  })

  it('useTagLoeschen verweigert den Systemtag „zu prüfen“', async () => {
    const { result } = renderMitQuery(() => useTagLoeschen())
    const fehler = await act(() => result.current.mutateAsync(tagIds.pruef).catch((e: unknown) => e))
    expect(fehler).toMatchObject({ status: 409 })
  })

  it('useMatchingTesten zählt Treffer im Belegtext', async () => {
    const { result } = renderMitQuery(() => useMatchingTesten())
    const ergebnis = await act(() =>
      result.current.mutateAsync({
        ziel: 'Tag',
        zielId: tagIds.ls,
        matching: { algorithmus: 'Regex', muster: 'lastschrift', grossKlein: false },
      }),
    )
    expect(ergebnis).toMatchObject({ treffer: 2, gesamt: 12, letzterTreffer: '7004 1182 3391' })
  })

  it('useAutoMatchingStatus liefert den Trainingsstand', async () => {
    const { result } = renderMitQuery(() => useAutoMatchingStatus())
    expect((await erwarteDaten(() => result.current)).map((s) => s.ziel)).toContain('Kategorie')
  })
})
