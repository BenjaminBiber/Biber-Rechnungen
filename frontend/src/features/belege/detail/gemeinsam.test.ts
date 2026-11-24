import { describe, expect, it } from 'vitest'
import { parseBetrag, parseDatum, parseLeistung, parseSatz, steuerStimmt } from './gemeinsam'

describe('deutsche Eingabeformate', () => {
  it('liest Datumsangaben TT.MM.JJJJ und lehnt unmögliche Daten ab', () => {
    expect(parseDatum('19.11.2026')).toBe('2026-11-19')
    expect(parseDatum('1.2.2026')).toBe('2026-02-01')
    expect(parseDatum('29.02.2028')).toBe('2028-02-29')
    expect(parseDatum('31.02.2026')).toBeNull()
    expect(parseDatum('2026-11-19')).toBeNull()
    expect(parseDatum('19.11.26')).toBeNull()
  })

  it('liest Beträge mit Komma und Tausenderpunkt', () => {
    expect(parseBetrag('221,85')).toBe(221.85)
    expect(parseBetrag('1.221,85')).toBe(1221.85)
    expect(parseBetrag('264')).toBe(264)
    expect(parseBetrag('-2,5')).toBe(-2.5)
    expect(parseBetrag('264,00 €')).toBe(264)
    expect(parseBetrag('221.85')).toBeNull()
    expect(parseBetrag('22,855')).toBeNull()
    expect(parseBetrag('abc')).toBeNull()
  })

  it('liest Steuersätze', () => {
    expect(parseSatz('19')).toBe(19)
    expect(parseSatz('7 %')).toBe(7)
    expect(parseSatz('5,5')).toBe(5.5)
    expect(parseSatz('x')).toBeNull()
  })

  it('liest Leistungsdatum oder -zeitraum', () => {
    expect(parseLeistung('')).toEqual({ leistungsdatum: null, leistungVon: null, leistungBis: null })
    expect(parseLeistung('19.11.2026')).toEqual({ leistungsdatum: '2026-11-19', leistungVon: null, leistungBis: null })
    expect(parseLeistung('01.11.2026 – 30.11.2026')).toEqual({
      leistungsdatum: null,
      leistungVon: '2026-11-01',
      leistungBis: '2026-11-30',
    })
    expect(parseLeistung('01.11.2026-30.11.2026')).not.toBe('ungueltig')
    expect(parseLeistung('30.11.2026 – 01.11.2026')).toBe('ungueltig')
    expect(parseLeistung('November')).toBe('ungueltig')
  })

  it('prüft Steuerzeilen mit 0,02 € Toleranz', () => {
    expect(steuerStimmt(19, 221.85, 42.15)).toBe(true)
    expect(steuerStimmt(19, 221.85, 42.17)).toBe(true)
    expect(steuerStimmt(19, 221.85, 42.18)).toBe(false)
    expect(steuerStimmt(7, 100, 7.5)).toBe(false)
  })
})
