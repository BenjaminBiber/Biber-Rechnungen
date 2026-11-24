import { describe, expect, it } from 'vitest'
import { artikelListe, preisDetails, stammartikelListe } from './artikel'

describe('Artikel-Fixtures', () => {
  it.each(stammartikelListe)('Stammartikel „$name“ stimmt mit Artikelliste und Detail überein', (stamm) => {
    const detail = preisDetails[stamm.id]
    const inListe = artikelListe.filter((a) => a.stammartikel?.id === stamm.id)

    expect(inListe.map((a) => a.id).sort()).toEqual((detail.artikel ?? []).map((a) => a.id).sort())
    expect(stamm.anzahlArtikel).toBe(inListe.length)
    expect([...new Set(inListe.map((a) => a.korrespondent.name))].sort()).toEqual([...stamm.korrespondenten].sort())
  })
})
