import { describe, expect, it } from 'vitest'
import { exportBezeichnung, schnellwahl } from './bezeichnung'

const heute = new Date(2026, 10, 23)

describe('exportBezeichnung', () => {
  it.each([
    ['2026-07-01', '2026-09-30', 'Q3 2026'],
    ['2026-10-01', '2026-10-31', 'Oktober 2026'],
    ['2026-02-01', '2026-02-28', 'Februar 2026'],
    ['2025-01-01', '2025-12-31', 'Jahr 2025'],
    ['2026-10-01', '2026-11-23', 'Q4 2026 (bis heute)'],
    ['2026-01-01', '2026-11-23', 'Jahr 2026 (bis heute)'],
    ['2026-07-01', '2026-08-15', '01.07.2026 – 15.08.2026'],
    ['2026-07-03', '2026-09-30', '03.07.2026 – 30.09.2026'],
  ])('%s bis %s → %s', (von, bis, erwartet) => {
    expect(exportBezeichnung(von, bis, heute)).toBe(erwartet)
  })
})

describe('schnellwahl', () => {
  it('liefert die Pillen des Designs mit ihren Grenzen', () => {
    expect(schnellwahl(heute).map(({ text, von, bis }) => [text, von, bis])).toEqual([
      ['Oktober', '2026-10-01', '2026-10-31'],
      ['November', '2026-11-01', '2026-11-30'],
      ['Q3 2026', '2026-07-01', '2026-09-30'],
      ['Q4 2026', '2026-10-01', '2026-11-23'],
      ['Jahr 2026', '2026-01-01', '2026-11-23'],
      ['Vorjahr', '2025-01-01', '2025-12-31'],
    ])
  })
})
