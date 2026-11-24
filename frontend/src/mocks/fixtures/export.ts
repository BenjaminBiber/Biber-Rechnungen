import type { ExportKategorieZeile, ExportLauf, ExportVorschau } from '@/api/typen'
import { exportLaufId } from './ids'

/** Kategoriezeilen der Vorschau; Beträge in der Reihenfolge der Kategorien k1–k6. */
function kategorien([sw, tk, bb, fz, hw, so]: [
  number,
  number,
  number,
  number,
  number,
  number,
]): ExportKategorieZeile[] {
  return [
    { kategorie: 'Software & Hosting', euerZeile: 'Übrige Betriebsausgaben', brutto: sw },
    { kategorie: 'Telekommunikation', euerZeile: 'Telekommunikation', brutto: tk },
    { kategorie: 'Bürobedarf', euerZeile: 'Bürobedarf', brutto: bb },
    { kategorie: 'Fahrzeug', euerZeile: 'Kfz-Kosten (Leasing)', brutto: fz },
    { kategorie: 'Hardware', euerZeile: 'GWG / AfA prüfen', brutto: hw },
    { kategorie: 'Sonstiges', euerZeile: null, brutto: so },
  ]
}

/**
 * Zeitraum-Vorgaben mit Vorschau aus `Export.dc.html` (`renderVals().P`). Die ZIP-Größe („ca. 24 MB“) nennt das
 * Design nur für Q3 2026; für die übrigen Zeiträume ist sie anteilig nach Belegzahl ergänzt.
 */
export const exportZeitraeume: {
  schluessel: string
  bezeichnung: string
  von: string
  bis: string
  vorschau: ExportVorschau
}[] = [
  {
    schluessel: 'okt',
    bezeichnung: 'Oktober 2026',
    von: '2026-10-01',
    bis: '2026-10-31',
    vorschau: {
      anzahlBelege: 19,
      summeBrutto: 1664,
      vorsteuer: 258.9,
      dublettenAusgeschlossen: 0,
      anzahlZuPruefen: 0,
      zipGroesse: 7838535,
      kategorien: kategorien([489, 184, 142, 470, 214, 165]),
    },
  },
  {
    schluessel: 'nov',
    bezeichnung: 'November 2026',
    von: '2026-11-01',
    bis: '2026-11-30',
    vorschau: {
      anzahlBelege: 20,
      summeBrutto: 1704,
      vorsteuer: 265.44,
      dublettenAusgeschlossen: 1,
      anzahlZuPruefen: 5,
      zipGroesse: 8251090,
      kategorien: kategorien([489, 186, 264, 534, 0, 231]),
    },
  },
  {
    schluessel: 'q3',
    bezeichnung: 'Q3 2026',
    von: '2026-07-01',
    bis: '2026-09-30',
    vorschau: {
      anzahlBelege: 61,
      summeBrutto: 6482,
      vorsteuer: 1012.37,
      dublettenAusgeschlossen: 2,
      anzahlZuPruefen: 0,
      zipGroesse: 24 * 1024 * 1024,
      kategorien: kategorien([1431, 556, 645, 1520, 1790, 540]),
    },
  },
  {
    schluessel: 'q4',
    bezeichnung: 'Q4 2026 (bis heute)',
    von: '2026-10-01',
    bis: '2026-11-23',
    vorschau: {
      anzahlBelege: 39,
      summeBrutto: 3368,
      vorsteuer: 537.04,
      dublettenAusgeschlossen: 1,
      anzahlZuPruefen: 5,
      zipGroesse: 16089625,
      kategorien: kategorien([978, 370, 406, 1004, 214, 396]),
    },
  },
  {
    schluessel: 'jahr',
    bezeichnung: 'Jahr 2026 (bis heute)',
    von: '2026-01-01',
    bis: '2026-11-23',
    vorschau: {
      anzahlBelege: 212,
      summeBrutto: 20845,
      vorsteuer: 3301.54,
      dublettenAusgeschlossen: 3,
      anzahlZuPruefen: 5,
      zipGroesse: 87461552,
      kategorien: kategorien([5188, 2033, 2223, 5639, 3630, 2132]),
    },
  },
  {
    schluessel: 'vj',
    bezeichnung: 'Jahr 2025',
    von: '2025-01-01',
    bis: '2025-12-31',
    vorschau: {
      anzahlBelege: 231,
      summeBrutto: 20980,
      vorsteuer: 3318.2,
      dublettenAusgeschlossen: 4,
      anzahlZuPruefen: 0,
      zipGroesse: 95300088,
      kategorien: kategorien([5010, 2390, 1950, 5880, 3420, 2330]),
    },
  },
]

/** „Letzte Exporte“ aus `Export.dc.html`; Erstellzeit MESZ → UTC. */
export const exportLaeufe: ExportLauf[] = [
  {
    id: exportLaufId(1),
    erstelltAm: '2026-10-05T08:12:00Z',
    erstelltVon: 'M. Keller',
    bezeichnung: 'Q3 2026',
    inhalt: 'Belegliste (xlsx), Summen, Vorsteuer, ZIP',
    anzahlBelege: 61,
    von: '2026-07-01',
    bis: '2026-09-30',
    downloadUrl: `/api/export/laeufe/${exportLaufId(1)}/datei`,
  },
  {
    id: exportLaufId(2),
    erstelltAm: '2026-07-06T07:40:00Z',
    erstelltVon: 'M. Keller',
    bezeichnung: 'Q2 2026',
    inhalt: 'Belegliste (xlsx), Summen, Vorsteuer',
    anzahlBelege: 54,
    von: '2026-04-01',
    bis: '2026-06-30',
    downloadUrl: `/api/export/laeufe/${exportLaufId(2)}/datei`,
  },
  {
    id: exportLaufId(3),
    erstelltAm: '2026-04-08T14:05:00Z',
    erstelltVon: 'M. Keller',
    bezeichnung: 'Q1 2026',
    inhalt: 'Belegliste (csv), Summen, Vorsteuer, ZIP',
    anzahlBelege: 58,
    von: '2026-01-01',
    bis: '2026-03-31',
    downloadUrl: `/api/export/laeufe/${exportLaufId(3)}/datei`,
  },
]
