import { describe, expect, it } from 'vitest'
import { kategorieBaum, vollerKategorieName } from './kategorien'

const kategorien = [
  { id: 't', name: 'Tanken', elternId: 'k' },
  { id: 'b', name: 'Büro', elternId: null },
  { id: 'v', name: 'Versicherung', elternId: 'k' },
  { id: 'k', name: 'Kfz' },
  { id: 'w', name: 'Waise', elternId: 'gibt-es-nicht' },
]

describe('kategorieBaum', () => {
  it('ordnet Unterkategorien alphabetisch unter ihre Hauptkategorie', () => {
    expect(kategorieBaum(kategorien).map(({ kategorie, ebene }) => [kategorie.name, ebene])).toEqual([
      ['Büro', 0],
      ['Kfz', 0],
      ['Tanken', 1],
      ['Versicherung', 1],
      ['Waise', 0],
    ])
  })
})

describe('vollerKategorieName', () => {
  it('stellt der Unterkategorie die Hauptkategorie voran', () => {
    expect(vollerKategorieName(kategorien[0], kategorien)).toBe('Kfz › Tanken')
    expect(vollerKategorieName(kategorien[1], kategorien)).toBe('Büro')
  })
})
