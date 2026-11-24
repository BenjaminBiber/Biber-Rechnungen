import { HttpResponse } from 'msw'
import type { Benutzer, BenutzerAendern, BenutzerAnlegen, BenutzerFreigeben, NeuesPasswort } from '@/api/typen'
import { db, neueId } from '../db'
import { jsonKoerper, keinInhalt, nichtGefunden, problem, route, ungueltig, type Route } from './gemeinsam'

function finde(id: string): Benutzer | undefined {
  return db.benutzerListe.find((b) => b.id === id)
}

function ersetze(benutzer: Benutzer): Benutzer {
  db.benutzerListe = db.benutzerListe.map((b) => (b.id === benutzer.id ? benutzer : b))
  if (benutzer.id === db.benutzer.id) db.benutzer = benutzer
  return benutzer
}

export const benutzerRouten: Route[] = [
  route('get', '/api/benutzer', 'Admin', () => HttpResponse.json(db.benutzerListe)),

  route('post', '/api/benutzer', 'Admin', async ({ request }) => {
    const eingabe = await jsonKoerper<BenutzerAnlegen>(request)
    if (!eingabe.name.trim()) return ungueltig('name', 'Name ist erforderlich.')
    if (db.benutzerListe.some((b) => b.email === eingabe.email))
      return ungueltig('email', 'E-Mail ist bereits vergeben.')
    if (eingabe.passwort.length < 10) return ungueltig('passwort', 'Mindestens 10 Zeichen.')
    const benutzer: Benutzer = {
      id: neueId(),
      name: eingabe.name,
      email: eingabe.email,
      rolle: eingabe.rolle,
      freigegeben: true,
      totpAktiv: false,
      anmeldung: 'Passwort',
      zuletztAktiv: null,
    }
    db.benutzerListe.push(benutzer)
    return HttpResponse.json(benutzer, { status: 201 })
  }),

  route('put', '/api/benutzer/{id}', 'Admin', async ({ request, params }) => {
    const benutzer = finde(params.id)
    if (!benutzer) return nichtGefunden('Benutzer')
    const eingabe = await jsonKoerper<BenutzerAendern>(request)
    if (benutzer.id === db.benutzer.id && (eingabe.rolle !== 'Admin' || !eingabe.aktiv))
      return problem(409, 'Der eigene Admin-Zugang kann nicht herabgestuft werden')
    return HttpResponse.json(ersetze({ ...benutzer, rolle: eingabe.rolle, freigegeben: eingabe.aktiv }))
  }),

  route('post', '/api/benutzer/{id}/passwort', 'Admin', async ({ request, params }) => {
    if (!finde(params.id)) return nichtGefunden('Benutzer')
    const eingabe = await jsonKoerper<NeuesPasswort>(request)
    if (eingabe.neuesPasswort.length < 10) return ungueltig('neuesPasswort', 'Mindestens 10 Zeichen.')
    return keinInhalt()
  }),

  route('post', '/api/benutzer/{id}/freigeben', 'Admin', async ({ request, params }) => {
    const benutzer = finde(params.id)
    if (!benutzer) return nichtGefunden('Benutzer')
    if (benutzer.freigegeben) return problem(400, 'Benutzer ist bereits freigegeben')
    const { rolle } = await jsonKoerper<BenutzerFreigeben>(request)
    return HttpResponse.json(ersetze({ ...benutzer, rolle, freigegeben: true }))
  }),

  route('post', '/api/benutzer/{id}/ablehnen', 'Admin', ({ params }) => {
    const benutzer = finde(params.id)
    if (!benutzer) return nichtGefunden('Benutzer')
    if (benutzer.freigegeben) return problem(400, 'Nur wartende Registrierungen können abgelehnt werden')
    db.benutzerListe = db.benutzerListe.filter((b) => b.id !== benutzer.id)
    return keinInhalt()
  }),
]
