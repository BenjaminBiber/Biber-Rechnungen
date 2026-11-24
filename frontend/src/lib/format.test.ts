import { describe, expect, it } from 'vitest'
import { formatDatum, formatDatumZeit, formatEuro, formatProzent, formatZahl, initialen } from './format'

// Intl setzt zwischen Zahl und Einheit ein geschütztes Leerzeichen.
const nbsp = ' '

describe('formatEuro', () => {
  it('formatiert Beträge mit Tausenderpunkt und zwei Nachkommastellen', () => {
    expect(formatEuro(1704)).toBe(`1.704,00${nbsp}€`)
    expect(formatEuro(12.5)).toBe(`12,50${nbsp}€`)
    expect(formatEuro(0)).toBe(`0,00${nbsp}€`)
  })

  it('rundet auf Cent und zeigt negative Beträge mit Minus', () => {
    expect(formatEuro(-1234.567)).toBe(`-1.234,57${nbsp}€`)
  })

  it('nutzt die übergebene Währung', () => {
    expect(formatEuro(10, 'USD')).toBe(`10,00${nbsp}$`)
  })
})

describe('formatZahl', () => {
  it('nutzt deutsche Trennzeichen', () => {
    expect(formatZahl(1234.5)).toBe('1.234,5')
    expect(formatZahl(1234.5, 2)).toBe('1.234,50')
    expect(formatZahl(3)).toBe('3')
  })
})

describe('formatDatum', () => {
  it('formatiert ein reines Datum ohne Zeitzonenverschiebung', () => {
    expect(formatDatum('2026-11-19')).toBe('19.11.2026')
    expect(formatDatum('2026-01-01')).toBe('01.01.2026')
  })

  it('stellt Zeitpunkte in deutscher Zeit dar', () => {
    // 23:30 UTC ist in Berlin bereits der nächste Tag
    expect(formatDatum('2026-11-19T23:30:00Z')).toBe('20.11.2026')
  })
})

describe('formatDatumZeit', () => {
  it('zeigt Datum und Uhrzeit in deutscher Zeit', () => {
    expect(formatDatumZeit('2026-11-19T13:05:00Z')).toBe('19.11.2026, 14:05')
    // Sommerzeit: UTC+2
    expect(formatDatumZeit('2026-07-01T08:00:00Z')).toBe('01.07.2026, 10:00')
  })
})

describe('formatProzent', () => {
  it('zeigt Anteile als Prozent mit höchstens einer Nachkommastelle', () => {
    expect(formatProzent(0.123)).toBe(`12,3${nbsp}%`)
    expect(formatProzent(0.82)).toBe(`82${nbsp}%`)
  })

  it('setzt auf Wunsch ein Vorzeichen', () => {
    expect(formatProzent(0.05, true)).toBe(`+5${nbsp}%`)
    expect(formatProzent(-0.125, true)).toBe(`-12,5${nbsp}%`)
    expect(formatProzent(0, true)).toBe(`0${nbsp}%`)
  })
})

describe('initialen', () => {
  it('nimmt den ersten Buchstaben des ersten und letzten Worts', () => {
    expect(initialen('Benjamin Biber')).toBe('BB')
    expect(initialen('Anna Maria Schmidt')).toBe('AS')
  })

  it('kommt mit einem Wort, Leerraum und Kleinschreibung zurecht', () => {
    expect(initialen('telekom')).toBe('T')
    expect(initialen('  ella  ober ')).toBe('EO')
    expect(initialen('')).toBe('')
  })
})
