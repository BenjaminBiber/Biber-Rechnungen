import { describe, expect, it } from 'vitest'
import { grundpreis } from './grundpreis'

describe('grundpreis', () => {
  it('rechnet Einzelpreis ÷ Faktor × Menge und rundet auf Cent', () => {
    expect(grundpreis(24.9, 2500, 100)).toBe(1)
    expect(grundpreis(5.49, 500, 100)).toBe(1.1)
    expect(grundpreis(1.005, 1, 1)).toBe(1.01)
    expect(grundpreis(309, 1, 1)).toBe(309)
  })

  it('lehnt Faktor 0 ab', () => {
    expect(() => grundpreis(24.9, 0, 100)).toThrow(RangeError)
  })
})
