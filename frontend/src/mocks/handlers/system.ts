import { HttpResponse } from 'msw'
import type {
  ApiToken,
  ApiTokenAnlegen,
  ApiTokenErstellt,
  BelegLoeschen,
  BenachrichtigungZaehler,
  IntegritaetsLauf,
  LoeschErgebnis,
  Loeschen,
  NeuAuslesenStatus,
  Suchergebnis,
} from '@/api/typen'
import { beispielPdf } from '../dateien'
import { db, neueId } from '../db'
import { tokenScopes } from '../fixtures/tokens'
import { listeneintrag } from './belege'
import {
  abfrage,
  angenommen,
  datei,
  jsonKoerper,
  keinInhalt,
  nichtGefunden,
  problem,
  route,
  seite,
  ungueltig,
  wahr,
  zahl,
  type Route,
} from './gemeinsam'
import { korrespondentEintrag } from './korrespondenten'

/** Sekunden, die der Mock je Beleg veranschlagt. */
const sekundenJeBeleg = 4

/**
 * Simulierter Fortschritt von „Alle Belege erneut auslesen“: Jeder Abruf liest ein Viertel der Belege weiter aus;
 * ein angeforderter Abbruch greift beim nächsten Abruf (wie im Backend nach dem aktuellen Beleg).
 */
function neuAuslesenWeiter(): void {
  const lauf = db.neuAuslesenLauf
  if (!lauf?.laeuft) return
  const verarbeitet = lauf.abgebrochen
    ? lauf.verarbeitet
    : Math.min(lauf.gesamt, lauf.verarbeitet + Math.max(1, Math.ceil(lauf.gesamt / 4)))
  const fertig = lauf.abgebrochen || verarbeitet >= lauf.gesamt
  db.neuAuslesenLauf = {
    ...lauf,
    verarbeitet,
    laeuft: !fertig,
    beendetAm: fertig ? new Date().toISOString() : null,
    restSekunden: fertig ? null : (lauf.gesamt - verarbeitet) * sekundenJeBeleg,
    dauerSekunden: verarbeitet * sekundenJeBeleg,
  }
}

const neuAuslesenStand = (): NeuAuslesenStatus => ({ anzahlBelege: db.belege.length, lauf: db.neuAuslesenLauf })

/** Benachrichtigungen, Aufbewahrung, Integrität, Neu-Auslesen, Suche, API-Tokens und Verfahrensdoku. */
export const systemRouten: Route[] = [
  route('get', '/api/benachrichtigungen', 'Lesen', ({ request }) => {
    const parameter = abfrage(request)
    const nurUngelesen = wahr(parameter, 'nurUngelesen') ?? false
    const liste = db.benachrichtigungen
      .filter((b) => !nurUngelesen || !b.gelesen)
      .sort((a, b) => b.erstelltAm.localeCompare(a.erstelltAm))
    return HttpResponse.json(seite(liste, zahl(parameter, 'seite') ?? 1, 20))
  }),

  route('get', '/api/benachrichtigungen/zaehler', 'Lesen', () =>
    HttpResponse.json<BenachrichtigungZaehler>({ ungelesen: db.benachrichtigungen.filter((b) => !b.gelesen).length }),
  ),

  route('post', '/api/benachrichtigungen/{id}/gelesen', 'Lesen', ({ params }) => {
    const eintrag = db.benachrichtigungen.find((b) => b.id === params.id)
    if (!eintrag) return nichtGefunden('Benachrichtigung')
    eintrag.gelesen = true
    return keinInhalt()
  }),

  route('post', '/api/benachrichtigungen/alle-gelesen', 'Lesen', () => {
    db.benachrichtigungen = db.benachrichtigungen.map((b) => ({ ...b, gelesen: true }))
    return keinInhalt()
  }),

  route('get', '/api/aufbewahrung/loeschbar', 'Admin', () => HttpResponse.json(db.loeschbar)),

  route('post', '/api/aufbewahrung/loeschen', 'Admin', async ({ request }) => {
    const { jahr, bestaetigung } = await jsonKoerper<Loeschen>(request)
    const eintrag = db.loeschbar.find((l) => l.jahr === jahr)
    if (!eintrag) return problem(409, `Der Jahrgang ${jahr} ist noch nicht löschbar`)
    if (bestaetigung !== 'LÖSCHEN') return ungueltig('bestaetigung', 'Zur Bestätigung „LÖSCHEN“ eingeben.')
    db.loeschbar = db.loeschbar.filter((l) => l.jahr !== jahr)
    const geloeschte = db.belege.filter((b) => b.rechnungsdatum?.startsWith(String(jahr)))
    db.belege = db.belege.filter((b) => !geloeschte.includes(b))
    return HttpResponse.json<LoeschErgebnis>({
      geloescht: eintrag.anzahl,
      pfade: geloeschte.flatMap((b) => b.dateien.map((d) => d.pfad)),
    })
  }),

  route('post', '/api/belege/{id}/loeschen', 'Admin', async ({ params, request }) => {
    const beleg = db.belege.find((b) => b.id === params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const { grund, bemerkung } = await jsonKoerper<BelegLoeschen>(request)
    if (grund !== 'FalschZugeordnet')
      return ungueltig('grund', 'Ein Beleg darf nur gelöscht werden, wenn er falsch zugeordnet ist.')
    if (beleg.status === 'InVerarbeitung')
      return problem(409, 'Der Beleg wird gerade verarbeitet und kann erst danach gelöscht werden.')
    db.belege = db.belege.filter((b) => b !== beleg)
    db.geloeschteBelege = [
      {
        beleg,
        eintrag: {
          id: beleg.id,
          titel: beleg.titel,
          rechnungsnummer: beleg.rechnungsnummer,
          rechnungsdatum: beleg.rechnungsdatum,
          korrespondent: beleg.korrespondent?.name ?? null,
          brutto: beleg.brutto,
          waehrung: beleg.waehrung,
          grund,
          bemerkung: bemerkung?.trim() || null,
          geloeschtAm: new Date().toISOString(),
          geloeschtVon: db.benutzer.name,
          originalId: beleg.dateien.find((d) => d.rolle === 'Original')?.id ?? null,
        },
      },
      ...db.geloeschteBelege,
    ]
    return keinInhalt()
  }),

  route('post', '/api/belege/{id}/wiederherstellen', 'Admin', ({ params }) => {
    const geloescht = db.geloeschteBelege.find((g) => g.eintrag.id === params.id)
    if (!geloescht) return nichtGefunden('Beleg')
    db.geloeschteBelege = db.geloeschteBelege.filter((g) => g !== geloescht)
    db.belege = [...db.belege, geloescht.beleg]
    return keinInhalt()
  }),

  route('get', '/api/integritaet/laeufe', 'Lesen', () => HttpResponse.json(db.integritaetsLaeufe)),

  route('post', '/api/integritaet/starten', 'Admin', () => {
    const lauf: IntegritaetsLauf = {
      id: neueId(),
      gestartetAm: new Date().toISOString(),
      dauerSekunden: 1,
      anzahlDateien: db.belege.reduce((s, b) => s + b.dateien.length, 0),
      anzahlAbweichungen: 0,
      abweichungen: [],
    }
    db.integritaetsLaeufe = [lauf, ...db.integritaetsLaeufe]
    return angenommen()
  }),

  route('get', '/api/neu-auslesen', 'Admin', () => {
    neuAuslesenWeiter()
    return HttpResponse.json(neuAuslesenStand())
  }),

  route('post', '/api/neu-auslesen/starten', 'Admin', () => {
    if (db.neuAuslesenLauf?.laeuft) return problem(409, 'Lauf läuft bereits')
    db.neuAuslesenLauf = {
      id: neueId(),
      gestartetAm: new Date().toISOString(),
      beendetAm: null,
      laeuft: true,
      abgebrochen: false,
      abbruchgrund: null,
      gesamt: db.belege.length,
      verarbeitet: 0,
      anzahlFehler: 0,
      anzahlUebersprungen: 0,
      restSekunden: null,
      dauerSekunden: 0,
      letzterFehler: null,
      fehlerBelege: [],
    }
    return HttpResponse.json(neuAuslesenStand(), { status: 202 })
  }),

  route('post', '/api/neu-auslesen/abbrechen', 'Admin', () => {
    const lauf = db.neuAuslesenLauf
    if (!lauf?.laeuft) return problem(409, 'Kein Lauf aktiv')
    db.neuAuslesenLauf = { ...lauf, abgebrochen: true, abbruchgrund: 'Von Hand abgebrochen' }
    return HttpResponse.json(neuAuslesenStand())
  }),

  route('get', '/api/bankleitzahlen', 'Admin', () => HttpResponse.json(db.bankleitzahlen)),

  route('post', '/api/bankleitzahlen/abrufen', 'Admin', () => {
    const jetzt = new Date().toISOString()
    db.bankleitzahlen = { ...db.bankleitzahlen, letzterAbrufAm: jetzt, letzterFehler: null }
    return HttpResponse.json(db.bankleitzahlen)
  }),

  route('put', '/api/bankleitzahlen', 'Admin', async ({ request }) => {
    const datei = (await request.formData()).get('datei')
    if (!(datei instanceof File)) return ungueltig('datei', 'Es wurde keine Datei hochgeladen.')
    if (!datei.name.toLowerCase().endsWith('.txt'))
      return ungueltig(
        'datei',
        'Das ist keine Bankleitzahlendatei der Bundesbank im TXT-Format (z. B. „blz-aktuell-txt-data.txt“).',
      )
    db.bankleitzahlen = {
      ...db.bankleitzahlen,
      anzahl: 3507,
      geladenAm: new Date().toISOString(),
      quelle: 'Upload',
      datei: datei.name,
    }
    return HttpResponse.json(db.bankleitzahlen)
  }),

  route('get', '/api/suche', 'Lesen', ({ request }) => {
    const q = abfrage(request).get('q')?.trim().toLocaleLowerCase('de') ?? ''
    const passt = (...texte: (string | null | undefined)[]) =>
      !!q && texte.some((t) => t?.toLocaleLowerCase('de').includes(q))
    return HttpResponse.json<Suchergebnis>({
      belege: db.belege
        .filter((b) => passt(b.titel, b.rechnungsnummer, b.korrespondent?.name, b.auszug))
        .slice(0, 5)
        .map(listeneintrag),
      korrespondenten: db.korrespondenten
        .filter((k) => passt(k.name, ...k.aliase))
        .slice(0, 5)
        .map(korrespondentEintrag),
      artikel: db.artikel.filter((a) => passt(a.name)).slice(0, 5),
      stammartikel: db.stammartikel.filter((s) => passt(s.name)).slice(0, 5),
    })
  }),

  route('get', '/api/tokens', 'Lesen', () => HttpResponse.json(db.tokens)),

  route('post', '/api/tokens', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<ApiTokenAnlegen>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    if (!eingabe.scopes.length || eingabe.scopes.some((s) => !tokenScopes.includes(s)))
      return ungueltig('scopes', 'Mindestens einen gültigen Scope wählen.')
    const id = neueId()
    const klartext = `brk_${id.replace(/-/g, '').slice(-24)}`
    const token: ApiToken = {
      id,
      name: eingabe.name,
      endung: klartext.slice(-4),
      scopes: eingabe.scopes,
      erstelltAm: new Date().toISOString(),
      zuletztGenutztAm: null,
      widerrufenAm: null,
    }
    db.tokens.push(token)
    return HttpResponse.json<ApiTokenErstellt>({ token, klartext }, { status: 201 })
  }),

  route('post', '/api/tokens/{id}/widerrufen', 'Bearbeiten', ({ params }) => {
    const token = db.tokens.find((t) => t.id === params.id)
    if (!token) return nichtGefunden('Token')
    token.widerrufenAm ??= new Date().toISOString()
    return keinInhalt()
  }),

  route('get', '/api/verfahrensdoku', 'Lesen', () => HttpResponse.json(db.verfahrensdoku)),

  route('get', '/api/verfahrensdoku/pdf', 'Lesen', () =>
    datei(beispielPdf, 'application/pdf', `Verfahrensdokumentation_${db.verfahrensdoku.version}.pdf`),
  ),
]
