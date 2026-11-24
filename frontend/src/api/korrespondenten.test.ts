import { act, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { belegIds, korrespondentIds } from '@/mocks/fixtures/ids'
import { useBeleg } from './belege'
import {
  korrespondentLogoUrl,
  useKorrespondent,
  useKorrespondentAendern,
  useKorrespondenten,
  useKorrespondentenZusammenfuehren,
  useLogoHochladen,
} from './korrespondenten'
import { erwarteDaten, nodeFormDataVerwenden, renderMitQuery } from './testHilfe'

describe('Korrespondenten-Hooks', () => {
  it('useKorrespondenten liefert die zehn Korrespondenten und sucht in Namen und Aliasen', async () => {
    const { result } = renderMitQuery(() => useKorrespondenten())
    const liste = await erwarteDaten(() => result.current)
    expect(liste).toHaveLength(10)
    expect(liste[0]).toMatchObject({ name: 'Autohaus Brenner GmbH', anzahlBelege: 22, kostenJahr: 5639 })

    const suche = renderMitQuery(() => useKorrespondenten({ suche: 'hostwerk' }))
    expect((await erwarteDaten(() => suche.result.current)).map((k) => k.name)).toEqual(['Hostwerk Süd GmbH'])
  })

  it('useKorrespondent liefert das Detail mit offenen Belegen und Artikeln', async () => {
    const { result } = renderMitQuery(() => useKorrespondent(korrespondentIds.bueroweltKraemer))
    const detail = await erwarteDaten(() => result.current)
    expect(detail.kennzahlen).toMatchObject({ kostenJahr: 2223, offenAnzahl: 1, offenSumme: 264 })
    expect(detail.letzteBelege.map((b) => b.rechnungsnummer)).toEqual(['2026-48817', '2026-47102'])
    expect(detail.anzahlArtikel).toBeGreaterThan(0)
  })

  it('useKorrespondentAendern ändert den Namen auch in den Belegen', async () => {
    const { result } = renderMitQuery(() => ({
      detail: useKorrespondent(korrespondentIds.hostwerkSued),
      beleg: useBeleg(belegIds.b1),
      aendern: useKorrespondentAendern(),
    }))
    const k = await erwarteDaten(() => result.current.detail)
    await erwarteDaten(() => result.current.beleg)
    await act(() =>
      result.current.aendern.mutateAsync({
        id: k.id,
        name: 'Hostwerk Süd AG',
        aliase: k.aliase,
        ibans: k.ibans,
        istLieferant: true,
        istKunde: false,
        defaultKategorieId: k.defaultKategorie?.id,
        matching: k.matching,
      }),
    )
    await waitFor(() => expect(result.current.beleg.data?.korrespondent?.name).toBe('Hostwerk Süd AG'))
  })

  it('useKorrespondentenZusammenfuehren übernimmt die Dublette als Alias', async () => {
    const { result } = renderMitQuery(() => ({
      liste: useKorrespondenten(),
      zusammen: useKorrespondentenZusammenfuehren(),
    }))
    await erwarteDaten(() => result.current.liste)
    const detail = await act(() =>
      result.current.zusammen.mutateAsync({
        behaltenId: korrespondentIds.bueroweltKraemer,
        quellIds: [korrespondentIds.bueroweltKraemerDublette],
      }),
    )
    expect(detail.aliase).toContain('Buerowelt Kraemer')
    await waitFor(() => expect(result.current.liste.data).toHaveLength(9))
  })

  it('useLogoHochladen sendet die Datei als Formular', async () => {
    await nodeFormDataVerwenden()
    const { result } = renderMitQuery(() => useLogoHochladen())
    const logo = await act(() =>
      result.current.mutateAsync({
        id: korrespondentIds.kanzleiHofmann,
        datei: new File(['png'], 'logo.png', { type: 'image/png' }),
      }),
    )
    expect(logo).toMatchObject({ quelle: 'Upload', url: korrespondentLogoUrl(korrespondentIds.kanzleiHofmann) })
  })
})
