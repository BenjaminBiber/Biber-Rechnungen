import { describe, expect, it } from 'vitest'
import { ibanFormatieren, ibanGueltig } from './iban'

describe('iban', () => {
  it.each(['DE89 3704 0044 0532 0130 00', 'de89370400440532013000', 'GB82WEST12345698765432', 'AT611904300234573201'])(
    'erkennt %s als gültig',
    (iban) => expect(ibanGueltig(iban)).toBe(true),
  )

  it.each(['DE89 3704 0044 0532 0130 01', 'DE00', '', 'XX12 abc', '1234567890123456'])('lehnt %s ab', (iban) =>
    expect(ibanGueltig(iban)).toBe(false),
  )

  it('formatiert in Vierergruppen', () => {
    expect(ibanFormatieren('de89370400440532013000')).toBe('DE89 3704 0044 0532 0130 00')
  })
})
