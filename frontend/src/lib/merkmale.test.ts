import { describe, expect, it } from 'vitest'
import { merkmaleAufteilen } from './merkmale'

describe('merkmaleAufteilen', () => {
  it('macht unterschiedliche Merkmale zu Spalten und gleiche zu gemeinsamen', () => {
    const farbe = { name: 'Farbe', wert: 'grau/marine/schwarz' }
    const { spalten, gemeinsam, werte } = merkmaleAufteilen([
      { merkmale: [{ name: 'Größe', wert: '47' }, farbe] },
      { merkmale: [{ name: 'Größe', wert: '42' }, farbe] },
    ])
    expect(spalten).toEqual([{ name: 'Größe', titel: 'Größe' }])
    expect(gemeinsam).toEqual([farbe])
    expect(werte.map((w) => w.get('Größe'))).toEqual(['47', '42'])
  })

  it('macht ein Merkmal, das nicht überall vorkommt, zur Spalte', () => {
    const { spalten, gemeinsam } = merkmaleAufteilen([
      { merkmale: [{ name: 'Farbe', wert: 'blau' }] },
      { merkmale: [] },
    ])
    expect(spalten.map((s) => s.name)).toEqual(['Farbe'])
    expect(gemeinsam).toEqual([])
  })

  it('fasst Merkmale ohne Namen in der Spalte „Merkmal“ zusammen', () => {
    const { spalten, werte } = merkmaleAufteilen([
      {
        merkmale: [
          { name: null, wert: 'schwarz' },
          { name: null, wert: 'XL' },
        ],
      },
      { merkmale: [{ name: null, wert: 'M' }] },
    ])
    expect(spalten).toEqual([{ name: '', titel: 'Merkmal' }])
    expect(werte.map((w) => w.get(''))).toEqual(['schwarz / XL', 'M'])
  })

  it('liefert ohne Zeilen nichts', () => {
    expect(merkmaleAufteilen([])).toEqual({ spalten: [], gemeinsam: [], werte: [] })
  })
})
