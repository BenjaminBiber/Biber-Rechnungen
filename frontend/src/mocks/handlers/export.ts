import { HttpResponse } from 'msw'
import type { ExportAnfrage, ExportLauf, ExportVorschau } from '@/api/typen'
import { leeresZip } from '../dateien'
import { db, neueId } from '../db'
import { exportZeitraeume } from '../fixtures/export'
import { istZuPruefen } from './belege'
import { datei, jsonKoerper, nichtGefunden, route, runden, ungueltig, type Route } from './gemeinsam'

/** Vorschau des Designs für die angebotenen Zeiträume; andere Zeiträume rechnet der Mock aus den Belegen. */
function vorschau(anfrage: ExportAnfrage): ExportVorschau {
  const vorgabe = exportZeitraeume.find((z) => z.von === anfrage.von && z.bis === anfrage.bis)
  if (vorgabe) return vorgabe.vorschau
  const imZeitraum = db.belege.filter(
    (b) => (b.rechnungsdatum ?? '') >= anfrage.von && (b.rechnungsdatum ?? '') <= anfrage.bis,
  )
  const zuPruefen = imZeitraum.filter(istZuPruefen)
  const dubletten = imZeitraum.filter((b) => b.istDublette)
  const enthalten = imZeitraum.filter(
    (b) => !(anfrage.dublettenAusschliessen && b.istDublette) && !(anfrage.zuPruefenAusschliessen && istZuPruefen(b)),
  )
  const jeKategorie = new Map<string, number>()
  for (const b of enthalten) {
    const name = b.kategorie?.name ?? 'Ohne Kategorie'
    jeKategorie.set(name, runden((jeKategorie.get(name) ?? 0) + (b.brutto ?? 0)))
  }
  return {
    anzahlBelege: enthalten.length,
    summeBrutto: runden(enthalten.reduce((s, b) => s + (b.brutto ?? 0), 0)),
    vorsteuer: runden(enthalten.reduce((s, b) => s + (b.steuer ?? 0), 0)),
    dublettenAusgeschlossen: anfrage.dublettenAusschliessen ? dubletten.length : 0,
    anzahlZuPruefen: zuPruefen.length,
    zipGroesse: anfrage.zipOriginale ? enthalten.length * 412_000 : null,
    kategorien: [...jeKategorie].map(([kategorie, brutto]) => ({
      kategorie,
      euerZeile: db.kategorien.find((k) => k.name === kategorie)?.euerZeile ?? null,
      brutto,
    })),
  }
}

function pruefe(anfrage: ExportAnfrage): Response | undefined {
  if (!anfrage.von || !anfrage.bis) return ungueltig('von', 'Zeitraum ist erforderlich.')
  if (anfrage.von > anfrage.bis) return ungueltig('bis', '„Bis“ liegt vor „Von“.')
  return undefined
}

export const exportRouten: Route[] = [
  route('post', '/api/export/vorschau', 'Bearbeiten', async ({ request }) => {
    const anfrage = await jsonKoerper<ExportAnfrage>(request)
    return pruefe(anfrage) ?? HttpResponse.json(vorschau(anfrage))
  }),

  route('post', '/api/export', 'Bearbeiten', async ({ request }) => {
    const anfrage = await jsonKoerper<ExportAnfrage>(request)
    const fehler = pruefe(anfrage)
    if (fehler) return fehler
    const teile = [
      anfrage.belegliste && `Belegliste (${anfrage.beleglisteFormat})`,
      anfrage.summenJeKategorie && 'Summen',
      anfrage.vorsteuer && 'Vorsteuer',
      anfrage.zipOriginale && 'ZIP',
    ].filter(Boolean)
    const id = neueId()
    const lauf: ExportLauf = {
      id,
      erstelltAm: new Date().toISOString(),
      bezeichnung: anfrage.bezeichnung,
      inhalt: teile.join(', '),
      anzahlBelege: vorschau(anfrage).anzahlBelege,
      von: anfrage.von,
      bis: anfrage.bis,
      downloadUrl: `/api/export/laeufe/${id}/datei`,
    }
    db.exportLaeufe = [lauf, ...db.exportLaeufe]
    return HttpResponse.json(lauf, { status: 201 })
  }),

  route('get', '/api/export/laeufe', 'Lesen', () => HttpResponse.json(db.exportLaeufe)),

  route('get', '/api/export/laeufe/{id}/datei', 'Lesen', ({ params }) => {
    const lauf = db.exportLaeufe.find((l) => l.id === params.id)
    if (!lauf) return nichtGefunden('Export')
    return datei(leeresZip(), 'application/zip', `Export_${lauf.bezeichnung.replace(/\s+/g, '_')}.zip`, {
      inline: false,
    })
  }),
]
