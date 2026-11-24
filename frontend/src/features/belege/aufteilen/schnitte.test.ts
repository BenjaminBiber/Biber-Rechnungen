import { describe, expect, it } from 'vitest'
import { seitenText, teilBuchstabe, teileAusSchnitten } from './schnitte'

describe('teileAusSchnitten', () => {
  it('liefert ohne Schnitt einen Teil', () => {
    expect(teileAusSchnitten(4, [])).toEqual([{ seiten: [1, 2, 3, 4] }])
  })

  it('teilt bei 4 Seiten nach Seite 2', () => {
    expect(teileAusSchnitten(4, [2])).toEqual([{ seiten: [1, 2] }, { seiten: [3, 4] }])
  })

  it('verarbeitet mehrere, unsortierte Schnitte', () => {
    expect(teileAusSchnitten(5, [4, 1, 1])).toEqual([{ seiten: [1] }, { seiten: [2, 3, 4] }, { seiten: [5] }])
  })

  it('ignoriert einen Schnitt nach der letzten Seite', () => {
    expect(teileAusSchnitten(3, [3, 0])).toEqual([{ seiten: [1, 2, 3] }])
  })
})

describe('Beschriftung', () => {
  it('benennt Teile und Seitenbereiche', () => {
    expect(teilBuchstabe(0)).toBe('A')
    expect(teilBuchstabe(2)).toBe('C')
    expect(seitenText([1, 2])).toBe('Seiten 1–2')
    expect(seitenText([3])).toBe('Seite 3')
  })
})
