import { HttpResponse } from 'msw'
import type { ImportQuelle, ImportVorgang } from '@/api/typen'
import { db, neueId } from '../db'
import { mockHeute } from '../fixtures/belege'
import { belegIds } from '../fixtures/ids'
import { abfrage, nichtGefunden, route, seite, ungueltig, zahl, type Route } from './gemeinsam'

const schritte = [
  'Dubletten-Check',
  'Format',
  'Document Intelligence',
  'Korrespondent',
  'Positionen',
  'Zuordnung',
  'Prüfungen',
  'Ablage',
]

/**
 * Im Mock hochgeladene Vorgänge laufen bei jedem Abruf einen Schritt weiter
 * (Angenommen → InVerarbeitung → Fertig), damit das Polling sichtbar endet.
 */
function weiterlaufen(): void {
  db.importVorgaenge = db.importVorgaenge.map((v) => {
    if (!db.simulierteVorgaenge.includes(v.id)) return v
    if (v.status === 'Angenommen')
      return {
        ...v,
        status: 'InVerarbeitung',
        versuche: 1,
        schritte: schritte.map((name, i) => ({ name, status: i < 2 ? 'ok' : i === 2 ? 'laeuft' : 'offen' })),
      }
    if (v.status === 'InVerarbeitung')
      return {
        ...v,
        status: 'Fertig',
        ergebnis: 'Beleg angelegt',
        belegId: belegIds.b1,
        schritte: schritte.map((name) => ({ name, status: 'ok' })),
      }
    return v
  })
}

export const importRouten: Route[] = [
  route('post', '/api/import/upload', 'Bearbeiten', async ({ request }) => {
    const formular = await request.formData()
    const dateien = formular.getAll('dateien').filter((d): d is File => typeof d !== 'string')
    if (!dateien.length) return ungueltig('dateien', 'Keine Datei übermittelt.')
    const vorgaenge: ImportVorgang[] = dateien.map((d) => ({
      id: neueId(),
      zeitpunkt: new Date().toISOString(),
      quelle: 'Upload',
      dateiname: d.name,
      groesse: d.size,
      herkunft: db.benutzer.name,
      herkunftDetail: null,
      status: 'Angenommen',
      versuche: 0,
      maxVersuche: 5,
      ergebnis: null,
      fehler: null,
      belegId: null,
      duplikatVonBelegId: null,
      naechsterVersuchAm: null,
      schritte: schritte.map((name) => ({ name, status: 'offen' })),
    }))
    db.importVorgaenge = [...vorgaenge, ...db.importVorgaenge]
    db.simulierteVorgaenge.push(...vorgaenge.map((v) => v.id))
    return HttpResponse.json(vorgaenge, { status: 202 })
  }),

  route('get', '/api/import/vorgaenge', 'Lesen', ({ request }) => {
    weiterlaufen()
    const parameter = abfrage(request)
    const quelle = parameter.get('quelle') as ImportQuelle | null
    const tage = zahl(parameter, 'tage')
    const ab =
      tage === undefined ? '' : new Date(Date.parse(`${mockHeute}T23:59:59Z`) - tage * 86_400_000).toISOString()
    const treffer = db.importVorgaenge
      .filter(
        (v) => (!quelle || v.quelle === quelle) && (!ab || v.zeitpunkt >= ab || db.simulierteVorgaenge.includes(v.id)),
      )
      .sort((a, b) => b.zeitpunkt.localeCompare(a.zeitpunkt))
    return HttpResponse.json(seite(treffer, zahl(parameter, 'seite') ?? 1, 20))
  }),

  route('get', '/api/import/vorgaenge/{id}', 'Lesen', ({ params }) => {
    weiterlaufen()
    const vorgang = db.importVorgaenge.find((v) => v.id === params.id)
    return vorgang ? HttpResponse.json(vorgang) : nichtGefunden('Import-Vorgang')
  }),
]
