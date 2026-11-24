import { HttpResponse } from 'msw'
import type { Code, KontoAendern, PasswortAendern, TotpEinrichtung, Wiederherstellungscodes } from '@/api/typen'
import { db } from '../db'
import { mockZugang, totpVorschlag } from '../fixtures/auth'
import { jsonKoerper, keinInhalt, route, ungueltig, type Route } from './gemeinsam'

function neueCodes(): string[] {
  const zeichen = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
  return Array.from({ length: 10 }, (_, i) =>
    Array.from({ length: 9 }, (_, j) =>
      j === 4 ? '-' : zeichen[(i * 7 + j * 13 + db.naechsteNummer) % zeichen.length],
    ).join(''),
  )
}

function benutzerAktualisieren(): void {
  db.benutzer = { ...db.benutzer, name: db.konto.name, email: db.konto.email, totpAktiv: db.konto.totpAktiv }
  db.benutzerListe = db.benutzerListe.map((b) => (b.id === db.benutzer.id ? db.benutzer : b))
}

export const kontoRouten: Route[] = [
  route('get', '/api/konto', 'Lesen', () => HttpResponse.json(db.konto)),

  route('put', '/api/konto', 'Lesen', async ({ request }) => {
    const eingabe = await jsonKoerper<KontoAendern>(request)
    if (!eingabe.name.trim()) return ungueltig('name', 'Name ist erforderlich.')
    if (!eingabe.email.includes('@')) return ungueltig('email', 'Keine gültige E-Mail-Adresse.')
    db.konto = { ...db.konto, name: eingabe.name, email: eingabe.email }
    benutzerAktualisieren()
    return HttpResponse.json(db.konto)
  }),

  route('post', '/api/konto/passwort', 'Lesen', async ({ request }) => {
    const eingabe = await jsonKoerper<PasswortAendern>(request)
    if (eingabe.altesPasswort !== db.passwort) return ungueltig('altesPasswort', 'Das aktuelle Passwort stimmt nicht.')
    if (eingabe.neuesPasswort.length < 10) return ungueltig('neuesPasswort', 'Mindestens 10 Zeichen.')
    db.passwort = eingabe.neuesPasswort
    db.konto.passwortGeaendertAm = new Date().toISOString()
    return keinInhalt()
  }),

  route('post', '/api/konto/totp/einrichten', 'Lesen', () => HttpResponse.json<TotpEinrichtung>(totpVorschlag)),

  route('post', '/api/konto/totp/aktivieren', 'Lesen', async ({ request }) => {
    const { code } = await jsonKoerper<Code>(request)
    if (code.replace(/\s/g, '') !== mockZugang.totpCode) return ungueltig('code', 'Der Code stimmt nicht.')
    db.wiederherstellungscodes = neueCodes()
    db.konto = { ...db.konto, totpAktiv: true, wiederherstellungscodes: db.wiederherstellungscodes.length }
    benutzerAktualisieren()
    return HttpResponse.json<Wiederherstellungscodes>({ codes: db.wiederherstellungscodes })
  }),

  route('post', '/api/konto/totp/deaktivieren', 'Lesen', () => {
    db.wiederherstellungscodes = []
    db.konto = { ...db.konto, totpAktiv: false, wiederherstellungscodes: 0 }
    benutzerAktualisieren()
    return keinInhalt()
  }),

  route('post', '/api/konto/wiederherstellungscodes', 'Lesen', () => {
    db.wiederherstellungscodes = neueCodes()
    db.konto.wiederherstellungscodes = db.wiederherstellungscodes.length
    return HttpResponse.json<Wiederherstellungscodes>({ codes: db.wiederherstellungscodes })
  }),

  route('post', '/api/konto/sitzungen/andere-abmelden', 'Lesen', () => {
    db.konto.aktiveSitzungen = 1
    return keinInhalt()
  }),
]
