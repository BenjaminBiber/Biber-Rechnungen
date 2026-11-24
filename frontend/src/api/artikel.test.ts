import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { artikelIds, stammartikelIds } from '@/mocks/fixtures/ids'
import {
  useArtikel,
  useArtikelDetail,
  useStammartikel,
  useStammartikelAnlegen,
  useStammartikelArtikelEntfernen,
  useStammartikelDetail,
} from './artikel'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Artikel-Hooks', () => {
  it('useArtikel liefert die neun Artikel und filtert nach Preisänderung', async () => {
    const { result } = renderMitQuery(() => ({
      alle: useArtikel(),
      gestiegen: useArtikel({ preisaenderung: 'Gestiegen' }),
    }))
    expect((await erwarteDaten(() => result.current.alle)).gesamt).toBe(9)
    const gestiegen = await erwarteDaten(() => result.current.gestiegen)
    expect(gestiegen.eintraege.every((a) => (a.aenderungSeitErstkauf ?? 0) > 0)).toBe(true)
  })

  it('useArtikelDetail und useStammartikelDetail liefern Preisdetails', async () => {
    const { result } = renderMitQuery(() => ({
      artikel: useArtikelDetail(artikelIds.a1),
      stamm: useStammartikelDetail(stammartikelIds.kopierpapier),
    }))
    expect(await erwarteDaten(() => result.current.artikel)).toMatchObject({ name: 'Kopierpapier A4 80 g' })
    expect(await erwarteDaten(() => result.current.stamm)).toMatchObject({ istStammartikel: true })
  })

  it('useStammartikelAnlegen fasst Artikel zusammen', async () => {
    const { result } = renderMitQuery(() => ({
      stamm: useStammartikel(),
      ohne: useArtikel({ ohneStammartikel: true }),
      anlegen: useStammartikelAnlegen(),
    }))
    expect(await erwarteDaten(() => result.current.stamm)).toHaveLength(4)
    const ohneVorher = (await erwarteDaten(() => result.current.ohne)).gesamt
    await act(() =>
      result.current.anlegen.mutateAsync({
        name: 'Ordner breit',
        basiseinheit: 'Stück',
        grundpreisMenge: 1,
        artikel: [{ artikelId: artikelIds.a2, umrechnungFaktor: null }],
      }),
    )
    await waitFor(() => expect(result.current.stamm.data).toHaveLength(5))
    await waitFor(() => expect(result.current.ohne.data?.gesamt).toBe(ohneVorher - 1))
  })

  it('useStammartikelArtikelEntfernen löst einen Artikel', async () => {
    const { result } = renderMitQuery(() => ({ artikel: useArtikel(), entfernen: useStammartikelArtikelEntfernen() }))
    await erwarteDaten(() => result.current.artikel)
    await act(() =>
      result.current.entfernen.mutateAsync({ id: stammartikelIds.kopierpapier, artikelId: artikelIds.a1 }),
    )
    await waitFor(() =>
      expect(result.current.artikel.data?.eintraege.find((a) => a.id === artikelIds.a1)?.stammartikel).toBeNull(),
    )
  })
})
