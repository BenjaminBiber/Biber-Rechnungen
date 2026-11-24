import { describe, expect, it } from 'vitest'
import { istEinzelbeleg, istListe, istVorschau, wirdZwischengespeichert } from './cacheRegeln'

describe('Cache-Regeln des Service Workers', () => {
  it('ordnet Listen, Einzelbelege und Vorschaubilder ihrer Strategie zu', () => {
    for (const pfad of ['/api/belege', '/api/belege/zaehler', '/api/dashboard', '/api/korrespondenten', '/api/tags'])
      expect(istListe(pfad)).toBe(true)
    expect(istListe('/api/kategorien')).toBe(true)
    expect(istListe('/api/auth/status')).toBe(true)
    expect(istEinzelbeleg('/api/belege/b-1')).toBe(true)
    expect(istEinzelbeleg('/api/belege/zaehler')).toBe(false)
    expect(istVorschau('/api/belege/b-1/vorschau')).toBe(true)
  })

  it('speichert nie Nicht-GET-Anfragen, Anmeldung, Originaldateien oder Tokens', () => {
    expect(wirdZwischengespeichert('POST', '/api/belege')).toBe(false)
    expect(wirdZwischengespeichert('POST', '/api/import/upload')).toBe(false)
    expect(wirdZwischengespeichert('PUT', '/api/belege/b-1')).toBe(false)
    expect(wirdZwischengespeichert('GET', '/api/auth/totp-vorschlag')).toBe(false)
    expect(wirdZwischengespeichert('GET', '/api/belege/b-1/dateien/d-1')).toBe(false)
    expect(wirdZwischengespeichert('GET', '/api/tokens')).toBe(false)
    expect(wirdZwischengespeichert('GET', '/api/belege')).toBe(true)
  })
})
