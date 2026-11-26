import { HttpResponse } from 'msw'
import type { AktionErgebnis, Einstellungen, MailZugangSpeichern } from '@/api/typen'
import { db } from '../db'
import { jsonKoerper, keinInhalt, nichtGefunden, problem, route, ungueltig, type Route } from './gemeinsam'

type Gruppe = Exclude<keyof Einstellungen, 'umgebung' | 'mailZugang'>

const gruppen: Gruppe[] = ['allgemein', 'ust', 'pruefungen', 'di', 'mail', 'ordner', 'aufbewahrung', 'zugriff']

const ok = (meldung: string) => HttpResponse.json<AktionErgebnis>({ erfolg: true, meldung })

export const einstellungenRouten: Route[] = [
  route('get', '/api/einstellungen', 'Lesen', () => HttpResponse.json(db.einstellungen)),

  route('put', '/api/einstellungen/{gruppe}', 'Admin', async ({ request, params }) => {
    const gruppe = params.gruppe as Gruppe
    if (!gruppen.includes(gruppe)) return nichtGefunden('Einstellungsgruppe')
    const werte = await jsonKoerper<Record<string, unknown>>(request)
    if (gruppe === 'aufbewahrung' && Number(werte.jahre) < 8)
      return ungueltig('jahre', 'Die gesetzliche Aufbewahrungsfrist beträgt mindestens 8 Jahre.')
    if (gruppe === 'di' && !(Number(werte.monatslimit) > 0))
      return ungueltig('monatslimit', 'Limit muss größer als 0 sein.')
    db.einstellungen = { ...db.einstellungen, [gruppe]: { ...db.einstellungen[gruppe], ...werte } }
    if (gruppe === 'di') db.diVerbrauch = { ...db.diVerbrauch, limit: db.einstellungen.di.monatslimit }
    return new HttpResponse(null, { status: 204 })
  }),

  route('get', '/api/einstellungen/di/verbrauch', 'Lesen', () => HttpResponse.json(db.diVerbrauch)),

  route('post', '/api/einstellungen/di/verbindung-testen', 'Admin', () =>
    db.einstellungen.umgebung.diKonfiguriert
      ? ok('Verbindung zu Document Intelligence erfolgreich.')
      : HttpResponse.json<AktionErgebnis>({ erfolg: false, meldung: 'Document Intelligence ist nicht konfiguriert.' }),
  ),

  route('post', '/api/einstellungen/di/fortsetzen', 'Admin', () => {
    db.diVerbrauch = { ...db.diVerbrauch, pausiert: false }
    return ok('Verarbeitung wird fortgesetzt.')
  }),

  route('put', '/api/einstellungen/mail/zugang', 'Admin', async ({ request }) => {
    const zugang = db.einstellungen.mailZugang
    if (zugang.ausUmgebung)
      return problem(409, 'Der IMAP-Zugang ist per .env gesetzt und lässt sich hier nicht ändern.')
    const werte = await jsonKoerper<MailZugangSpeichern>(request)
    if (!werte.host?.trim()) return ungueltig('host', 'Darf nicht leer sein.')
    const passwortGesetzt = zugang.passwortGesetzt || !!werte.passwort
    db.einstellungen = {
      ...db.einstellungen,
      mailZugang: {
        ...zugang,
        host: werte.host.trim(),
        port: werte.port,
        ssl: werte.ssl,
        benutzer: werte.benutzer.trim(),
        passwortGesetzt,
        konfiguriert: passwortGesetzt && !!werte.benutzer.trim(),
      },
    }
    if (db.mailStatus.fehler) db.mailStatus = { verbunden: false, letzterAbruf: null, fehler: null }
    return keinInhalt()
  }),

  route('post', '/api/einstellungen/mail/verbindung-testen', 'Admin', () =>
    db.einstellungen.mailZugang.konfiguriert
      ? ok('Verbindung zum Postfach erfolgreich.')
      : HttpResponse.json<AktionErgebnis>({ erfolg: false, meldung: 'IMAP ist nicht konfiguriert.' }),
  ),

  route('post', '/api/einstellungen/mail/jetzt-abrufen', 'Admin', () => {
    db.mailStatus = { ...db.mailStatus, verbunden: true, letzterAbruf: new Date().toISOString(), fehler: null }
    return ok('Postfach abgerufen – keine neuen Mails.')
  }),

  route('get', '/api/einstellungen/mail/status', 'Lesen', () => HttpResponse.json(db.mailStatus)),
]
