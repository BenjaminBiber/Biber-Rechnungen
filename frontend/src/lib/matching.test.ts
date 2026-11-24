import { describe, expect, it } from 'vitest'
import { matchingKurztext } from './matching'

describe('matchingKurztext', () => {
  it.each([
    [{ algorithmus: 'EinWort', muster: 'paypal', grossKlein: false }, 'Ein Wort: paypal'],
    [
      { algorithmus: 'AlleWoerter', muster: 'hotel übernachtung', grossKlein: false },
      'Alle Wörter: hotel übernachtung',
    ],
    [{ algorithmus: 'Exakt', muster: 'Leasingrate', grossKlein: true }, 'Exakt: Leasingrate'],
    [{ algorithmus: 'Regex', muster: 'lastschrift|sepa', grossKlein: false }, 'Regex'],
    [{ algorithmus: 'Auto', muster: null, grossKlein: false }, 'Auto'],
    [{ algorithmus: 'Keiner', muster: 'egal', grossKlein: false }, 'Keiner'],
  ] as const)('%o → %s', (m, text) => {
    expect(matchingKurztext(m)).toBe(text)
  })
})
