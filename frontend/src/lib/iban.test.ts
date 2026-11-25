import { describe, expect, it } from 'vitest'
import { bicGueltig, bicNormalisieren, ibanFormatieren, ibanGueltig } from './iban'

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

  it.each(['COBADEFFXXX', 'byladem1', 'BYLA DEM1 001'])('erkennt den BIC %s als gültig', (bic) =>
    expect(bicGueltig(bic)).toBe(true),
  )

  it.each(['COBA', 'COBADEFF1', 'C0BADEFF', ''])('lehnt den BIC %s ab', (bic) => expect(bicGueltig(bic)).toBe(false))

  it('normalisiert den BIC', () => {
    expect(bicNormalisieren(' cobadeff xxx ')).toBe('COBADEFFXXX')
  })
})
