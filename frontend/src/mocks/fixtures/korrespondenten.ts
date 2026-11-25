import type { KorrespondentDetail, KorrespondentListeneintrag, Logo, LogoQuelle, Matching } from '@/api/typen'
import { kategorieIds, korrespondentIds } from './ids'
import { kategorien } from './tags'

/**
 * Gespeicherter Korrespondent im Mock: Detailfelder ohne die abgeleiteten Listen
 * (`letzteBelege`, `artikel` bildet der Handler aus Belegen und Artikeln) plus die Listenspalten.
 */
export type KorrespondentDatensatz = Omit<KorrespondentDetail, 'letzteBelege' | 'artikel' | 'anzahlArtikel'> &
  Pick<KorrespondentListeneintrag, 'hinweis' | 'anzahlBelege' | 'kostenJahr' | 'letzterBeleg' | 'istNeu'> & {
    /** Hintergrund- und Schriftfarbe des Initialen-Logos (Design). */
    logoFarben: { hintergrund: string; schrift: string }
  }

export function logoFuer(id: string, initialen: string, quelle: LogoQuelle): Logo {
  return { url: quelle === 'Keine' ? null : `/api/korrespondenten/${id}/logo`, initialen, quelle }
}

function kategorie(name: string) {
  const treffer = kategorien.find((k) => k.name === name)
  return treffer ? { id: treffer.id, name: treffer.name } : null
}

const auto: Matching = { algorithmus: 'Auto', muster: null, grossKlein: false }
const keins: Matching = { algorithmus: 'Keiner', muster: null, grossKlein: false }

type Zeile = [
  schluessel: keyof typeof korrespondentIds,
  initialen: string,
  hintergrund: string,
  schrift: string,
  name: string,
  hinweis: string,
  ustOderSteuernummer: string,
  kategorie: string,
  matching: Matching,
  anzahl: number,
  kostenJahr: number,
  letzterBeleg: string,
  istNeu: boolean,
  logo: LogoQuelle,
]

/** Die zehn Korrespondenten aus `Korrespondenten.dc.html` (Spalten wie `raw` dort). */
const zeilen: Zeile[] = [
  [
    'autohausBrenner',
    'AB',
    '#DDF0EA',
    '#145C4B',
    'Autohaus Brenner GmbH',
    '',
    'DE 811 402 377',
    'Fahrzeug',
    auto,
    22,
    5639,
    '2026-11-18',
    false,
    'Pdf',
  ],
  [
    'hostwerkSued',
    'HS',
    '#E7E7F4',
    '#2F2D86',
    'Hostwerk Süd GmbH',
    'Alias „Hostwerk“',
    'DE 296 118 004',
    'Software & Hosting',
    { algorithmus: 'EinWort', muster: 'hostwerk', grossKlein: false },
    11,
    5188,
    '2026-11-20',
    false,
    'Favicon',
  ],
  [
    'hardwareversandNord',
    'HN',
    '#EEF0F3',
    '#4A5262',
    'Hardwareversand Nord GmbH',
    '',
    'DE 301 550 912',
    'Hardware',
    auto,
    4,
    3630,
    '2026-10-21',
    false,
    'Favicon',
  ],
  [
    'bueroweltKraemer',
    'BK',
    '#FDF0E3',
    '#A44A06',
    'Bürowelt Krämer KG',
    '2 IBANs',
    'DE 284 117 560',
    'Bürobedarf',
    auto,
    31,
    2223,
    '2026-11-19',
    false,
    'Pdf',
  ],
  [
    'netzfunkTelekom',
    'NT',
    '#E1F3F4',
    '#0E6168',
    'Netzfunk Telekom AG',
    '',
    'DE 122 900 418',
    'Telekommunikation',
    { algorithmus: 'Exakt', muster: 'Netzfunk Telekom', grossKlein: false },
    11,
    2033,
    '2026-11-15',
    false,
    'Upload',
  ],
  [
    'kanzleiHofmann',
    'KH',
    '#EEF0F3',
    '#4A5262',
    'Kanzlei Hofmann & Partner',
    '',
    'DE 270 334 189',
    'Sonstiges',
    keins,
    6,
    1894.4,
    '2026-11-10',
    false,
    'Keine',
  ],
  [
    'druckereiWeller',
    'DW',
    '#F0EAFB',
    '#5B2EA6',
    'Druckerei Weller',
    '',
    'Steuernr. 06/123/45678',
    'Sonstiges',
    auto,
    9,
    1402.1,
    '2026-11-23',
    false,
    'Pdf',
  ],
  [
    'stadtwerkeLindenau',
    'SL',
    '#E7E7F4',
    '#2F2D86',
    'Stadtwerke Lindenau',
    'XRechnung',
    'DE 139 027 655',
    'Sonstiges',
    auto,
    11,
    1288.6,
    '2026-11-14',
    false,
    'Favicon',
  ],
  [
    'elektroHaas',
    'EH',
    '#FDECEA',
    '#B42318',
    'Elektro Haas e.K.',
    'noch nicht geprüft',
    'DE 318 442 071',
    '—',
    keins,
    1,
    178.5,
    '2026-10-14',
    true,
    'Keine',
  ],
  [
    'bueroweltKraemerDublette',
    'BW',
    '#FDF0E3',
    '#A44A06',
    'Buerowelt Kraemer',
    'vermutlich Dublette von „Bürowelt Krämer KG“',
    '—',
    'Bürobedarf',
    keins,
    2,
    96.2,
    '2026-03-02',
    false,
    'Keine',
  ],
]

/** Kostenverlauf 2026 je Monat (brutto): Bürowelt aus `KorrespondentDetail.dc.html`, sonst aus `Main.dc.html`. */
const kostenverlauf: Partial<Record<keyof typeof korrespondentIds, number[]>> = {
  bueroweltKraemer: [312, 96, 148, 402, 88, 126, 233, 94, 318, 142, 264, 0],
  hostwerkSued: [452, 452, 468, 468, 468, 471, 471, 471, 489, 489, 489, 0],
  netzfunkTelekom: [184, 179, 184, 192, 184, 184, 188, 184, 184, 184, 186, 0],
  autohausBrenner: [540, 610, 420, 388, 702, 455, 512, 620, 388, 470, 534, 0],
  hardwareversandNord: [0, 1240, 0, 0, 386, 0, 0, 1790, 0, 214, 0, 0],
}

/** Stammdaten, die nur `KorrespondentDetail.dc.html` (Bürowelt Krämer KG) zeigt. */
const details: Partial<Record<keyof typeof korrespondentIds, Partial<KorrespondentDatensatz>>> = {
  bueroweltKraemer: {
    aliase: ['Bürowelt Krämer', 'Buerowelt Kraemer KG'],
    anschrift: 'Gewerbestraße 14\n79108 Freiburg',
    ustIdNr: 'DE 284117560',
    steuernummer: '06/471/10442',
    ibans: ['DE12 6805 0101 0012 3456 78', 'DE89 3704 0044 0532 0130 00'],
    website: 'buerowelt-kraemer.de',
    email: 'rechnung@buerowelt-kraemer.de',
    autoTrainiertAm: '2026-11-23T01:30:00Z',
  },
  hostwerkSued: { aliase: ['Hostwerk'] },
}

/** Kennzahlen „Kosten 2026“, „Belege gesamt“ und „Ø je Beleg“ aus `KorrespondentDetail.dc.html`. */
const kennzahlenBuerowelt = { aenderungVorjahr: 14, seit: '2024-03-01', durchschnittJeBeleg: 202.09 }

/** Die zehn Korrespondenten als veränderbare Datensätze (Basis für `src/mocks/db.ts`). */
export const korrespondenten: KorrespondentDatensatz[] = zeilen.map(
  ([
    schluessel,
    initialen,
    hintergrund,
    schrift,
    name,
    hinweis,
    steuer,
    kat,
    matching,
    anzahl,
    kosten,
    letzter,
    istNeu,
    logo,
  ]) => {
    const id = korrespondentIds[schluessel]
    const istSteuernummer = steuer.startsWith('Steuernr. ')
    const standard: KorrespondentDatensatz = {
      id,
      name,
      aliase: [],
      anschrift: null,
      ustIdNr: steuer === '—' || istSteuernummer ? null : steuer,
      steuernummer: istSteuernummer ? steuer.replace('Steuernr. ', '') : null,
      ibans: [],
      website: null,
      email: null,
      istLieferant: true,
      istKunde: false,
      logo: logoFuer(id, initialen, logo),
      logoFarben: { hintergrund, schrift },
      defaultKategorie: kategorie(kat),
      matching,
      autoTrainiertAm: matching.algorithmus === 'Auto' ? '2026-11-23T01:30:00Z' : null,
      kennzahlen: {
        kostenJahr: kosten,
        aenderungVorjahr: null,
        anzahlBelege: anzahl,
        seit: null,
        durchschnittJeBeleg: Math.round((kosten / anzahl) * 100) / 100,
        offenAnzahl: 0,
        offenSumme: 0,
        naechsteFaelligkeit: null,
      },
      kostenverlaufMonate: kostenverlauf[schluessel] ?? [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      hinweis: hinweis || null,
      anzahlBelege: anzahl,
      kostenJahr: kosten,
      letzterBeleg: letzter,
      istNeu,
    }
    const zusatz = details[schluessel] ?? {}
    const datensatz = { ...standard, ...zusatz }
    if (schluessel === 'bueroweltKraemer') datensatz.kennzahlen = { ...datensatz.kennzahlen, ...kennzahlenBuerowelt }
    return datensatz
  },
)

/** Kategorie-ID je Korrespondent für Belege ohne eigene Angabe. */
export const standardKategorieId = (korrespondentId: string): string =>
  korrespondenten.find((k) => k.id === korrespondentId)?.defaultKategorie?.id ?? kategorieIds.sonstiges
