import { describe, expect, it } from 'vitest'
import { datumEinlesen, zeitraumVorgabe } from './zeitraum'

const mitte = new Date(2026, 10, 24) // 24.11.2026, Q4
const januar = new Date(2026, 0, 15) // Q1 – Vormonat und Vorquartal liegen im Vorjahr
const februarSchaltjahr = new Date(2028, 1, 10)

describe('zeitraumVorgabe', () => {
  it.each([
    ['dieserMonat', mitte, '2026-11-01', '2026-11-30'],
    ['letzterMonat', mitte, '2026-10-01', '2026-10-31'],
    ['diesesQuartal', mitte, '2026-10-01', '2026-12-31'],
    ['letztesQuartal', mitte, '2026-07-01', '2026-09-30'],
    ['diesesJahr', mitte, '2026-01-01', '2026-12-31'],
    ['vorjahr', mitte, '2025-01-01', '2025-12-31'],
    ['dieserMonat', januar, '2026-01-01', '2026-01-31'],
    ['letzterMonat', januar, '2025-12-01', '2025-12-31'],
    ['diesesQuartal', januar, '2026-01-01', '2026-03-31'],
    ['letztesQuartal', januar, '2025-10-01', '2025-12-31'],
    ['diesesJahr', januar, '2026-01-01', '2026-12-31'],
    ['vorjahr', januar, '2025-01-01', '2025-12-31'],
    ['dieserMonat', februarSchaltjahr, '2028-02-01', '2028-02-29'],
    ['letzterMonat', new Date(2026, 2, 31), '2026-02-01', '2026-02-28'],
  ] as const)('%s am %s → %s bis %s', (id, heute, von, bis) => {
    expect(zeitraumVorgabe(id, heute)).toEqual({ von, bis })
  })
})

describe('datumEinlesen', () => {
  it('liest TT.MM.JJJJ', () => {
    expect(datumEinlesen('01.10.2026')).toBe('2026-10-01')
    expect(datumEinlesen(' 1.2.2026 ')).toBe('2026-02-01')
  })

  it.each(['31.02.2026', '32.01.2026', '2026-01-01', '1.1.26', 'abc', ''])('lehnt „%s“ ab', (text) => {
    expect(datumEinlesen(text)).toBeNull()
  })
})
