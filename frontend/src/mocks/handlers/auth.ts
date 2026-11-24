import { HttpResponse } from 'msw'
import type {
  AnmeldeErgebnis,
  Anmelden,
  AuthStatus,
  Benutzer,
  Code,
  Ersteinrichtung,
  Registrieren,
  Totp,
} from '@/api/typen'
import { db, neueId } from '../db'
import { mockZugang, totpVorschlag } from '../fixtures/auth'
import { angenommen, jsonKoerper, keinInhalt, problem, route, ungueltig, weiterleitung, type Route } from './gemeinsam'

function ergebnis(status: AnmeldeErgebnis['status']): Response {
  return HttpResponse.json<AnmeldeErgebnis>({ status })
}

function anmelden(): Response {
  db.angemeldet = true
  db.wartetAufTotp = false
  return ergebnis('Ok')
}

export const authRouten: Route[] = [
  route('get', '/api/auth/status', 'Anonym', () =>
    HttpResponse.json<AuthStatus>({
      eingerichtet: db.eingerichtet,
      angemeldet: db.angemeldet,
      benutzer: db.angemeldet ? db.benutzer : null,
      registrierungOffen: db.einstellungen.zugriff.offeneRegistrierung,
      googleAktiv: db.einstellungen.zugriff.googleAnmeldung,
    }),
  ),

  route('get', '/api/auth/totp-vorschlag', 'Anonym', () =>
    db.eingerichtet ? problem(409, 'Bereits eingerichtet') : HttpResponse.json(totpVorschlag),
  ),

  route('post', '/api/auth/ersteinrichtung', 'Anonym', async ({ request }) => {
    if (db.eingerichtet) return problem(409, 'Bereits eingerichtet')
    const eingabe = await jsonKoerper<Ersteinrichtung>(request)
    if (eingabe.passwort.length < 10) return ungueltig('passwort', 'Mindestens 10 Zeichen.')
    const benutzer: Benutzer = {
      id: db.benutzer.id,
      name: eingabe.name,
      email: eingabe.email,
      rolle: 'Admin',
      freigegeben: true,
      totpAktiv: true,
      anmeldung: 'Passwort',
      zuletztAktiv: null,
    }
    db.benutzer = benutzer
    db.benutzerListe = [benutzer]
    db.passwort = eingabe.passwort
    db.konto = { ...db.konto, name: eingabe.name, email: eingabe.email, totpAktiv: true }
    db.eingerichtet = true
    db.angemeldet = true
    return HttpResponse.json(benutzer, { status: 201 })
  }),

  route('post', '/api/auth/anmelden', 'Anonym', async ({ request }) => {
    const eingabe = await jsonKoerper<Anmelden>(request)
    const wartend = db.benutzerListe.find((b) => b.email === eingabe.email && !b.freigegeben)
    if (wartend) return ergebnis('WartetAufFreigabe')
    if (eingabe.email !== db.benutzer.email || eingabe.passwort !== db.passwort) return ergebnis('Ungueltig')
    if (db.benutzer.totpAktiv) {
      db.wartetAufTotp = true
      return ergebnis('TotpErforderlich')
    }
    return anmelden()
  }),

  route('post', '/api/auth/totp', 'Anonym', async ({ request }) => {
    const eingabe = await jsonKoerper<Totp>(request)
    if (!db.wartetAufTotp || eingabe.code.replace(/\s/g, '') !== mockZugang.totpCode) return ergebnis('Ungueltig')
    return anmelden()
  }),

  route('post', '/api/auth/wiederherstellungscode', 'Anonym', async ({ request }) => {
    const { code } = await jsonKoerper<Code>(request)
    const normal = code.trim().toUpperCase()
    if (!db.wartetAufTotp || !db.wiederherstellungscodes.includes(normal)) return ergebnis('Ungueltig')
    db.wiederherstellungscodes = db.wiederherstellungscodes.filter((c) => c !== normal)
    db.konto.wiederherstellungscodes = db.wiederherstellungscodes.length
    return anmelden()
  }),

  route('post', '/api/auth/abmelden', 'Lesen', () => {
    db.angemeldet = false
    db.wartetAufTotp = false
    return keinInhalt()
  }),

  route('post', '/api/auth/registrieren', 'Anonym', async ({ request }) => {
    if (!db.einstellungen.zugriff.offeneRegistrierung) return problem(403, 'Registrierung ist geschlossen')
    const eingabe = await jsonKoerper<Registrieren>(request)
    if (db.benutzerListe.some((b) => b.email === eingabe.email))
      return ungueltig('email', 'E-Mail ist bereits vergeben.')
    db.benutzerListe.push({
      id: neueId(),
      name: eingabe.name,
      email: eingabe.email,
      rolle: null,
      freigegeben: false,
      totpAktiv: false,
      anmeldung: 'Passwort',
      zuletztAktiv: null,
    })
    return angenommen()
  }),

  // Google-Anmeldung: im Mock gleich zurück in die App.
  route('get', '/api/auth/google/start', 'Anonym', () => weiterleitung('/')),
  route('get', '/api/auth/google/rueckruf', 'Anonym', () => {
    db.angemeldet = true
    return weiterleitung('/')
  }),
]
