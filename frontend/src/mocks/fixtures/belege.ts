import type {
  AuditEintrag,
  BelegDatei,
  BelegDetail,
  ExtraktionsQuelle,
  ImportQuelle,
  Position,
  PruefBefund,
  PruefungArt,
  Skonto,
} from '@/api/typen'
import { artikelIds, belegIds, dateiId, kategorieIds, korrespondentIds, tagIds } from './ids'
import { korrespondenten } from './korrespondenten'
import { kategorien, tagKurz, tags } from './tags'

/** „Heute“ im Mock – das Design zeigt den 24.11.2026. Überfälligkeit wird daran gemessen. */
export const mockHeute = '2026-11-24'

type TagSchluessel = keyof typeof tagIds

/** Der Beleg, dessen Korrespondent im Mock per Auto-Matching zugeordnet wurde (Erkennung „Auto“). */
const autoBeleg = 'b12'

type Zeile = {
  schluessel: keyof typeof belegIds
  korrespondent: keyof typeof korrespondentIds
  nr: string
  datum: string
  faellig: string
  kategorie: keyof typeof kategorieIds
  tags: TagSchluessel[]
  brutto: number
  netto: number
  quelle: ImportQuelle
  format: 'XRechnung' | 'ZUGFeRD' | 'Scan' | 'PDF'
  titel: string
  seiten: number
  auszug: string
}

/** Die zwölf Belege aus `Belege.dc.html` (`raw` in `renderVals()`); Netto aus dem Inhaltsauszug. */
const zeilen: Zeile[] = [
  {
    schluessel: 'b1',
    korrespondent: 'hostwerkSued',
    nr: 'HW-2026-11-0381',
    datum: '2026-11-20',
    faellig: '2026-12-04',
    kategorie: 'softwareHosting',
    tags: ['inbox'],
    brutto: 489,
    netto: 410.92,
    quelle: 'Mail',
    format: 'XRechnung',
    titel: 'Hosting & Domains November',
    seiten: 1,
    auszug:
      'Hostwerk Süd GmbH · Rechnung HW-2026-11-0381 · Leistungszeitraum 01.11.–30.11.2026 · Managed Server M2 349,00 € · Domains (4) 48,00 € · Backup-Speicher 500 GB 13,92 € · Netto 410,92 € · USt 19 % 78,08 € · Gesamtbetrag 489,00 € · zahlbar bis 04.12.2026',
  },
  {
    schluessel: 'b2',
    korrespondent: 'bueroweltKraemer',
    nr: '2026-48817',
    datum: '2026-11-19',
    faellig: '2026-12-03',
    kategorie: 'buerobedarf',
    tags: ['inbox', 'pruef'],
    brutto: 264,
    netto: 221.85,
    quelle: 'Ordner',
    format: 'Scan',
    titel: 'Büromaterial Bestellung',
    seiten: 1,
    auszug:
      'Bürowelt Krämer KG · Rechnung Nr. 2026-48817 · Kundennr. 10442 · Kopierpapier A4 80 g · Toner HP 59A schwarz · Ordner breit · Haftnotizen · Kugelschreiber · Versand · Nettobetrag 221,85 € · MwSt. 19 % 42,15 € · Rechnungsbetrag 264,00 € · zahlbar bis 03.12.2026 ohne Abzug',
  },
  {
    schluessel: 'b3',
    korrespondent: 'autohausBrenner',
    nr: 'R-118342',
    datum: '2026-11-18',
    faellig: '2026-12-02',
    kategorie: 'fahrzeug',
    tags: ['bez'],
    brutto: 534,
    netto: 448.74,
    quelle: 'Mail',
    format: 'ZUGFeRD',
    titel: 'Inspektion & Ölwechsel',
    seiten: 2,
    auszug:
      'Autohaus Brenner GmbH · Werkstattrechnung R-118342 · Kennzeichen LI-MK 482 · Inspektion nach Herstellervorgabe · Motoröl 5W-30 5,2 l · Ölfilter · Arbeitswerte 14 AW · Netto 448,74 € · USt 85,26 € · Gesamt 534,00 € · Betrag dankend erhalten',
  },
  {
    schluessel: 'b4',
    korrespondent: 'netzfunkTelekom',
    nr: '7004 1182 3391',
    datum: '2026-11-15',
    faellig: '2026-11-30',
    kategorie: 'telekommunikation',
    tags: ['ls'],
    brutto: 186,
    netto: 156.3,
    quelle: 'Mail',
    format: 'ZUGFeRD',
    titel: 'Mobilfunk & Internet November',
    seiten: 3,
    auszug:
      'Netzfunk Telekom AG · Ihre Rechnung November 2026 · Kundenkonto 7004 1182 3391 · Business Mobil L (2 Karten) · Festnetz & Internet 250 · Netto 156,30 € · USt 29,70 € · Der Betrag von 186,00 € wird am 30.11.2026 per Lastschrift eingezogen',
  },
  {
    schluessel: 'b5',
    korrespondent: 'stadtwerkeLindenau',
    nr: '2026-1187',
    datum: '2026-11-14',
    faellig: '2026-11-28',
    kategorie: 'sonstiges',
    tags: ['inbox', 'pruef'],
    brutto: 118.4,
    netto: 99.5,
    quelle: 'Mail',
    format: 'XRechnung',
    titel: 'Strom-Abschlag November',
    seiten: 1,
    auszug:
      'Stadtwerke Lindenau · Abschlagsrechnung 2026-1187 · Strom Gewerbe · Abschlag November 2026 · Netto 99,50 € · USt 18,90 € · Gesamt 118,40 €',
  },
  {
    schluessel: 'b6',
    korrespondent: 'druckereiWeller',
    nr: '26-0912',
    datum: '2026-11-12',
    faellig: '2026-11-26',
    kategorie: 'sonstiges',
    tags: ['inbox', 'pruef'],
    brutto: 231,
    netto: 194.12,
    quelle: 'Upload',
    format: 'PDF',
    titel: 'Visitenkarten & Flyer',
    seiten: 1,
    auszug:
      'Druckerei Weller · Rechnung 26-0912 · Visitenkarten 350 g, 500 Stück · Flyer DIN lang, 1.000 Stück · Gestaltungspauschale · Netto 194,12 € · USt 19 % 36,88 € · Summe 231,00 € · Vielen Dank für Ihren Auftrag',
  },
  {
    schluessel: 'b7',
    korrespondent: 'kanzleiHofmann',
    nr: 'HP-2026-342',
    datum: '2026-11-10',
    faellig: '2026-11-24',
    kategorie: 'sonstiges',
    tags: ['pruef'],
    brutto: 452.2,
    netto: 380,
    quelle: 'Mail',
    format: 'PDF',
    titel: 'Steuerberatung Q3',
    seiten: 2,
    auszug:
      'Kanzlei Hofmann & Partner · Honorarrechnung HP-2026-342 · Umsatzsteuer-Voranmeldung Q3 2026 · Finanzbuchhaltung Juli–September · Netto 380,00 € · USt 72,20 € · Gesamt 452,20 €',
  },
  {
    schluessel: 'b8',
    korrespondent: 'elektroHaas',
    nr: 'EH-4471',
    datum: '2026-10-14',
    faellig: '2026-10-28',
    kategorie: 'hardware',
    tags: ['pruef'],
    brutto: 178.5,
    netto: 150,
    quelle: 'Ordner',
    format: 'Scan',
    titel: 'Netzwerkverkabelung Büro',
    seiten: 1,
    auszug:
      'Elektro Haas e.K. · Rechnung EH-4471 · Netzwerkdose Cat. 6A · Patchkabel 2 m · Montage 1,5 Std. · Anfahrt · Netto 150,00 € · USt 28,50 € · Gesamt 178,50 € · zahlbar bis 28.10.2026',
  },
  {
    schluessel: 'b9',
    korrespondent: 'stadtwerkeLindenau',
    nr: '2026-1102',
    datum: '2026-11-01',
    faellig: '2026-11-15',
    kategorie: 'sonstiges',
    tags: [],
    brutto: 233.8,
    netto: 196.47,
    quelle: 'Mail',
    format: 'XRechnung',
    titel: 'Jahresabrechnung Strom',
    seiten: 2,
    auszug:
      'Stadtwerke Lindenau · Rechnung 2026-1102 · Jahresverbrauchsabrechnung Strom Gewerbe · Nachzahlung · Netto 196,47 € · USt 37,33 € · Gesamt 233,80 € · fällig 15.11.2026',
  },
  {
    schluessel: 'b10',
    korrespondent: 'hardwareversandNord',
    nr: 'HVN-552190',
    datum: '2026-10-21',
    faellig: '2026-11-04',
    kategorie: 'hardware',
    tags: ['pp'],
    brutto: 214,
    netto: 179.83,
    quelle: 'Mail',
    format: 'ZUGFeRD',
    titel: 'Dockingstation & Zubehör',
    seiten: 1,
    auszug:
      'Hardwareversand Nord GmbH · Rechnung HVN-552190 · USB-C-Dockingstation · HDMI-Kabel 2 m · Versand kostenlos · Netto 179,83 € · USt 34,17 € · Gesamt 214,00 € · bezahlt per PayPal',
  },
  {
    schluessel: 'b11',
    korrespondent: 'hostwerkSued',
    nr: 'HW-2026-10-0377',
    datum: '2026-10-20',
    faellig: '2026-11-03',
    kategorie: 'softwareHosting',
    tags: ['ls'],
    brutto: 489,
    netto: 410.92,
    quelle: 'Mail',
    format: 'XRechnung',
    titel: 'Hosting & Domains Oktober',
    seiten: 1,
    auszug:
      'Hostwerk Süd GmbH · Rechnung HW-2026-10-0377 · Leistungszeitraum 01.10.–31.10.2026 · Managed Server M2 349,00 € · Domains (4) 48,00 € · Backup-Speicher 500 GB 13,92 € · Netto 410,92 € · USt 78,08 € · Gesamtbetrag 489,00 € · Lastschrift',
  },
  {
    schluessel: 'b12',
    korrespondent: 'bueroweltKraemer',
    nr: '2026-47102',
    datum: '2026-10-16',
    faellig: '2026-10-30',
    kategorie: 'buerobedarf',
    tags: ['bez'],
    brutto: 142,
    netto: 119.33,
    quelle: 'Ordner',
    format: 'Scan',
    titel: 'Toner & Kopierpapier',
    seiten: 1,
    auszug:
      'Bürowelt Krämer KG · Rechnung Nr. 2026-47102 · Kopierpapier A4 80 g · Toner HP 59A schwarz · Ordner breit · Nettobetrag 119,33 € · MwSt. 19 % 22,67 € · Rechnungsbetrag 142,00 €',
  },
]

function runden(betrag: number): number {
  return Math.round(betrag * 100) / 100
}

const position = (
  nr: number,
  text: string,
  menge: number | null,
  einheit: string | null,
  einzelpreis: number | null,
  gesamt: number,
  artikel?: { id: string; name: string },
  grundpreis?: [number, string],
): Position => ({
  nr,
  text,
  menge,
  einheit,
  einzelpreis,
  gesamt,
  steuersatz: 19,
  merkmale: [],
  artikel: artikel ?? null,
  stammartikelId: null,
  grundpreis: grundpreis?.[0] ?? null,
  grundpreisEinheit: grundpreis?.[1] ?? null,
})

/** Positionen aus `BelegDetail.dc.html` (Beleg b2) bzw. aus den Inhaltsauszügen. */
const positionen: Partial<Record<keyof typeof belegIds, Position[]>> = {
  b2: [
    position(
      1,
      'Kopierpapier A4 80 g, 5 × 500 Bl.',
      2,
      'Karton',
      24.9,
      49.8,
      { id: artikelIds.a1, name: 'Kopierpapier A4 80 g' },
      [1, '100 Bl.'],
    ),
    position(2, 'Lasertoner schwarz, 3.000 S.', 1, 'Stück', 64.9, 64.9, {
      id: artikelIds.a5,
      name: 'Lasertoner schwarz',
    }),
    {
      ...position(
        3,
        'Ordner breit 8 cm, blau',
        10,
        'Stück',
        2.49,
        24.9,
        { id: artikelIds.a2, name: 'Ordner breit 8 cm, blau' },
        [2.49, 'St.'],
      ),
      artikelnummer: '7710-08',
      merkmale: [
        { name: 'Farbe', wert: 'blau' },
        { name: 'Rückenbreite', wert: '8 cm' },
      ],
    },
    position(4, 'Haftnotizen 76×76, 12 Blöcke', 2, 'Pack', 8.95, 17.9, undefined, [0.75, 'Block']),
    position(5, 'Kugelschreiber blau', 20, 'Stück', 0.89, 17.8, { id: artikelIds.a9, name: 'Kugelschreiber blau' }, [
      0.89,
      'St.',
    ]),
    position(6, 'Versandkarton 30×20×15', 15, 'Stück', 2.97, 44.55, undefined, [2.97, 'St.']),
  ],
  b1: [
    position(1, 'Managed Server M2', 1, 'Monat', 349, 349, { id: artikelIds.a3, name: 'Managed Server M' }),
    position(2, 'Domains (4)', 4, 'Stück', 12, 48),
    position(3, 'Backup-Speicher 500 GB', 1, 'Monat', 13.92, 13.92),
  ],
  b11: [
    position(1, 'Managed Server M2', 1, 'Monat', 349, 349, { id: artikelIds.a3, name: 'Managed Server M' }),
    position(2, 'Domains (4)', 4, 'Stück', 12, 48),
    position(3, 'Backup-Speicher 500 GB', 1, 'Monat', 13.92, 13.92),
  ],
}

const pruefNamen: Record<PruefungArt, string> = {
  Positionssumme: 'Summe der Positionen = Netto',
  NettoSteuerBrutto: 'Netto + USt = Brutto je Steuersatz',
  Pflichtfelder: 'Pflichtfelder',
  Datum: 'Datum plausibel',
  Paragraph14: 'Angaben nach § 14 UStG',
  Dublette: 'Dublettenverdacht',
  NeuerKorrespondent: 'Neuer Korrespondent',
  Konfidenz: 'Erkennungssicherheit',
  Kosit: 'KoSIT-Validierung',
  Bankverbindung: 'Bankverbindung bekannt',
  UstIdNr: 'USt-IdNr. gültig (VIES)',
}

/** Fehlgeschlagene Prüfungen der fünf Belege „zu prüfen“ (`Main.dc.html`, Liste „Zu prüfen“). */
const fehlgeschlagen: Partial<Record<keyof typeof belegIds, [PruefungArt, string][]>> = {
  b2: [
    ['Positionssumme', 'Positionen 219,85 € statt 221,85 € · Differenz 2,00 €, Toleranz 0,02 €'],
    ['Konfidenz', 'Konfidenz 0,74 liegt unter der Schwelle 0,80'],
  ],
  b5: [['Kosit', 'KoSIT: BR-DE-15 Käuferreferenz fehlt']],
  b6: [['Paragraph14', '§ 14 UStG: Leistungsdatum fehlt']],
  b7: [['Dublette', 'Dublettenverdacht zu HP-2026-311']],
  b8: [['NeuerKorrespondent', 'Neuer Korrespondent']],
}

/** Ok-Texte für Beleg b2 wörtlich aus `BelegDetail.dc.html`, sonst knapp. */
function befunde(zeile: Zeile, istERechnung: boolean): PruefBefund[] {
  const fehler = fehlgeschlagen[zeile.schluessel] ?? []
  const steuer = runden(zeile.brutto - zeile.netto)
  const okTexte: Record<PruefungArt, string> = {
    Positionssumme: 'Summe der Positionen entspricht dem Netto',
    NettoSteuerBrutto: `19 %: ${euro(zeile.netto)} + ${euro(steuer)} = ${euro(zeile.brutto)}`,
    Pflichtfelder: 'Korrespondent, Datum, Rechnungsnummer und Gesamtbetrag vorhanden',
    Datum: `${datumDe(zeile.datum)} – nicht in der Zukunft, jünger als 2 Jahre`,
    Paragraph14: 'USt-IdNr., Leistungsdatum, Empfänger, Steuersatz vorhanden',
    Dublette: 'Kein Treffer',
    NeuerKorrespondent: zeile.schluessel === 'b2' ? 'Bekannt seit 03/2024 · 31 Belege' : 'Korrespondent bekannt',
    Konfidenz: 'Konfidenz über der Schwelle 0,80',
    Kosit: 'XRechnung gültig',
    Bankverbindung: 'IBAN ist beim Korrespondenten hinterlegt',
    UstIdNr: 'USt-IdNr. ist laut VIES gültig',
  }
  const reihenfolge = Object.keys(pruefNamen) as PruefungArt[]
  const fehlgeschlageneArten = new Set(fehler.map(([art]) => art))
  return [
    ...fehler.map(([pruefung, detail]) => ({
      pruefung,
      name: pruefNamen[pruefung],
      ergebnis: 'Fehlgeschlagen' as const,
      detail,
    })),
    ...reihenfolge
      .filter((art) => !fehlgeschlageneArten.has(art))
      .map((pruefung) => {
        const nichtAnwendbar = (pruefung === 'Kosit' && !istERechnung) || (pruefung === 'Konfidenz' && istERechnung)
        return {
          pruefung,
          name: pruefNamen[pruefung],
          ergebnis: nichtAnwendbar ? ('NichtAnwendbar' as const) : ('Ok' as const),
          detail: nichtAnwendbar
            ? pruefung === 'Kosit'
              ? zeile.format === 'Scan'
                ? 'Keine E-Rechnung (gescanntes PDF)'
                : 'Keine E-Rechnung'
              : 'E-Rechnung – kein Auslesen nötig'
            : okTexte[pruefung],
        }
      }),
  ]
}

function euro(betrag: number): string {
  return betrag.toLocaleString('de-DE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'
}

function datumDe(iso: string): string {
  const [jahr, monat, tag] = iso.split('-')
  return `${tag}.${monat}.${jahr}`
}

function ablageName(name: string): string {
  return name
    .replace(/ä/g, 'ae')
    .replace(/ö/g, 'oe')
    .replace(/ü/g, 'ue')
    .replace(/ß/g, 'ss')
    .replace(/[^A-Za-z0-9]+/g, '_')
    .replace(/^_|_$/g, '')
}

function dateien(zeile: Zeile, nr: number, korrespondent: string): BelegDatei[] {
  const id = belegIds[zeile.schluessel]
  const pfad = `ablage/${zeile.datum.slice(0, 4)}/${ablageName(korrespondent)}/${zeile.datum}_${zeile.nr.replace(/\s+/g, '')}.pdf`
  const abgelegt = `${zeile.datum}T07:15:00Z`
  const datei = (
    art: number,
    rolle: BelegDatei['rolle'],
    mimeType: string,
    dateiPfad: string,
    groesse: number,
    sha: string,
  ) => ({
    id: dateiId(nr, art),
    rolle,
    mimeType,
    pfad: dateiPfad,
    sha256: sha,
    groesse,
    abgelegtAm: abgelegt,
    letztePruefungAm: rolle === 'Original' ? '2026-11-23T02:00:00Z' : null,
    pruefsummeOk: rolle === 'Original' ? true : null,
    url: `/api/belege/${id}/dateien/${dateiId(nr, art)}`,
  })
  const hash = (startwert: number) => (startwert * 2654435761).toString(16).padStart(8, '0').slice(0, 8).repeat(8)
  if (zeile.schluessel === 'b2') {
    return [
      datei(
        1,
        'Original',
        'application/pdf',
        'ablage/2026/Buerowelt_Kraemer_KG/2026-11-19_2026-48817.pdf',
        421_888,
        '9f2c41d7' + hash(nr).slice(16) + 'a83be41a',
      ),
      datei(
        4,
        'Vorschau',
        'image/webp',
        'vorschau/2026/11/b2.webp',
        38_912,
        '1c07e9aa' + hash(nr + 50).slice(16) + '5d20f3c9',
      ),
    ]
  }
  const liste = [datei(1, 'Original', 'application/pdf', pfad, 180_000 + nr * 13_100, hash(nr))]
  if (zeile.format === 'ZUGFeRD' || zeile.format === 'XRechnung') {
    liste.push(
      datei(2, 'EingebettetesXml', 'application/xml', pfad.replace(/\.pdf$/, '.xml'), 24_000 + nr * 310, hash(nr + 20)),
    )
    liste.push(
      datei(3, 'Visualisierung', 'text/html', pfad.replace(/\.pdf$/, '.html'), 31_000 + nr * 270, hash(nr + 30)),
    )
  }
  return liste
}

function historie(
  zeile: Zeile,
  korrespondent: string,
  extraktion: ExtraktionsQuelle,
  konfidenz: number | null,
): AuditEintrag[] {
  const nr = Number(zeile.schluessel.slice(1))
  const id = (lfd: number) => `${belegIds[zeile.schluessel].slice(0, 24)}${String(nr * 100 + lfd).padStart(12, '0')}`
  const zeit = (minute: number) => `${zeile.datum}T07:${String(minute).padStart(2, '0')}:00Z`
  const system = (lfd: number, minute: number, art: string, aktion: string, text: string): AuditEintrag => ({
    id: id(lfd),
    zeitpunkt: zeit(minute),
    wer: 'System',
    istSystem: true,
    aktion,
    text,
    feld: null,
    alterWert: null,
    neuerWert: null,
    art,
  })
  const herkunft = {
    Upload: 'Hochgeladen',
    Ordner: 'Importiert aus Eingangsordner',
    Mail: 'Importiert aus Mail',
    Mcp: 'Importiert über MCP',
  }[zeile.quelle]
  const ausgelesen =
    extraktion === 'DocumentIntelligence'
      ? `Ausgelesen mit Document Intelligence, ${zeile.seiten === 1 ? '1 Seite' : `${zeile.seiten} Seiten`}, Konfidenz ${String(konfidenz).replace('.', ',')}`
      : `Ausgelesen aus ${extraktion}-Daten`
  const pfad = dateien(zeile, nr, korrespondent)[0].pfad
  const eintraege: AuditEintrag[] = []
  if (zeile.schluessel === 'b2') {
    eintraege.push(
      {
        id: id(9),
        zeitpunkt: '2026-11-20T08:02:00Z',
        wer: 'A. Lehmann',
        istSystem: false,
        aktion: 'Geändert',
        text: 'Kategorie geändert',
        feld: 'Kategorie',
        alterWert: 'Sonstiges',
        neuerWert: 'Bürobedarf',
        art: 'bearbeitet',
      },
      {
        id: id(8),
        zeitpunkt: '2026-11-20T08:01:00Z',
        wer: 'A. Lehmann',
        istSystem: false,
        aktion: 'Geändert',
        text: 'Leistungsdatum ergänzt',
        feld: 'Leistungsdatum',
        alterWert: 'leer',
        neuerWert: '19.11.2026',
        art: 'bearbeitet',
      },
    )
  }
  const pruefTags = zeile.tags
    .filter((t) => t === 'inbox' || t === 'pruef')
    .map((t) => tags.find((tag) => tag.id === tagIds[t])!.name)
  const befundAnzahl = fehlgeschlagen[zeile.schluessel]?.length ?? 0
  eintraege.push(
    system(7, 15, 'abgelegt', 'Abgelegt', `Abgelegt unter ${pfad}`),
    system(
      6,
      14,
      befundAnzahl > 0 ? 'warnung' : 'tags',
      'Tags gesetzt',
      pruefTags.length > 0
        ? `Tags gesetzt: ${pruefTags.join(', ')}${befundAnzahl > 0 ? ` (${befundAnzahl === 1 ? '1 Befund' : `${befundAnzahl} Befunde`})` : ''}`
        : 'Keine Tags gesetzt',
    ),
    system(
      5,
      14,
      'matching',
      'Kategorie gesetzt',
      `Kategorie „${zeile.schluessel === 'b2' ? 'Sonstiges' : kategorien.find((k) => k.id === kategorieIds[zeile.kategorie])!.name}“ per Matching-Regel gesetzt`,
    ),
    system(
      4,
      14,
      'korrespondent',
      'Korrespondent zugeordnet',
      `Korrespondent zugeordnet: ${korrespondent} (über ${zeile.schluessel === autoBeleg ? 'Auto-Matching' : 'USt-IdNr.'})`,
    ),
    system(3, 14, 'auslesen', 'Ausgelesen', ausgelesen),
    system(
      2,
      13,
      'import',
      'Importiert',
      zeile.schluessel === 'b2'
        ? 'Importiert aus Eingangsordner: Scan_20261119_0812.pdf'
        : `${herkunft}: ${pfad.split('/').pop()}`,
    ),
  )
  return eintraege
}

/**
 * Skonto je Beleg (Prozentsatz, letzter Tag mit Abzug): b1 und b6 laufen noch (b6 endet morgen), b7 ist abgelaufen,
 * b4 ist per Lastschrift bezahlt.
 */
const skontoAngaben: Partial<Record<Zeile['schluessel'], { prozent: number; bis: string }>> = {
  b1: { prozent: 2, bis: '2026-11-27' },
  b4: { prozent: 2, bis: '2026-11-30' },
  b6: { prozent: 3, bis: '2026-11-25' },
  b7: { prozent: 2, bis: '2026-11-17' },
}

function skontoVon(zeile: Zeile): Skonto | null {
  const angabe = skontoAngaben[zeile.schluessel]
  if (!angabe) return null
  return { ...angabe, betrag: runden((zeile.brutto * angabe.prozent) / 100), abgelaufen: angabe.bis < mockHeute }
}

function baueBeleg(zeile: Zeile): BelegDetail {
  const nr = Number(zeile.schluessel.slice(1))
  const id = belegIds[zeile.schluessel]
  const korrespondent = korrespondenten.find((k) => k.id === korrespondentIds[zeile.korrespondent])!
  const kategorie = kategorien.find((k) => k.id === kategorieIds[zeile.kategorie])!
  const istERechnung = zeile.format === 'XRechnung' || zeile.format === 'ZUGFeRD'
  const extraktionsQuelle: ExtraktionsQuelle =
    zeile.format === 'XRechnung' || zeile.format === 'ZUGFeRD' ? zeile.format : 'DocumentIntelligence'
  const konfidenz = istERechnung ? null : zeile.schluessel === 'b2' ? 0.74 : 0.92
  const steuer = runden(zeile.brutto - zeile.netto)
  const belegTags = zeile.tags.map((t) => tagKurz(tags.find((tag) => tag.id === tagIds[t])!))
  const bezahlt = belegTags.some((t) => t.istZahlungsTag)
  const jahr = Number(zeile.datum.slice(0, 4))
  return {
    id,
    rechnungsnummer: zeile.nr,
    korrespondent: { id: korrespondent.id, name: korrespondent.name },
    logo: korrespondent.logo,
    titel: zeile.titel,
    kategorie: { id: kategorie.id, name: kategorie.name },
    rechnungsdatum: zeile.datum,
    faelligkeitsdatum: zeile.faellig,
    ueberfaellig: !bezahlt && zeile.faellig < mockHeute,
    netto: zeile.netto,
    brutto: zeile.brutto,
    waehrung: 'EUR',
    seitenanzahl: zeile.seiten,
    quelle: zeile.quelle,
    format: zeile.format,
    tags: belegTags,
    auszug: zeile.auszug,
    vorschauUrl: `/api/belege/${id}/vorschau`,
    istDublette: false,
    konfidenz,
    leistungsdatum: zeile.schluessel === 'b6' ? null : zeile.datum,
    belegart: 'Rechnung',
    status: 'Fertig',
    korrespondentErkennung: zeile.schluessel === autoBeleg ? 'Auto' : korrespondent.ustIdNr ? 'UstIdNr' : 'Name',
    bankverbindungen: [
      { iban: 'DE12680501010012345678', bic: 'FRSPDE66XXX', bank: 'Sparkasse Freiburg-Nördlicher Breisgau' },
      { iban: 'DE89370400440532013000', bic: null, bank: 'Commerzbank' },
    ],
    kategorieManuell: zeile.schluessel === 'b2',
    defaultKategorie: korrespondent.defaultKategorie ?? null,
    leistungVon: null,
    leistungBis: null,
    steuer,
    steuerzeilen: [{ satz: 19, netto: zeile.netto, steuer, stimmt: true }],
    positionen: positionen[zeile.schluessel] ?? [position(1, zeile.titel, 1, null, zeile.netto, zeile.netto)],
    befunde: befunde(zeile, istERechnung),
    dateien: dateien(zeile, nr, korrespondent.name),
    extraktion: {
      quelle: extraktionsQuelle,
      konfidenz,
      zeitpunkt: `${zeile.datum}T07:14:00Z`,
      seiten: zeile.seiten,
    },
    dubletten: { duplikatVon: null, duplikate: [], hashTreffer: false, fachlicherTreffer: false },
    loeschsperreBis: `${jahr + 8}-12-31`,
    istERechnung,
    aufgeteiltAusBelegId: null,
    teilbelege: [],
    bezug: { rechnung: null, gutschriften: [], restbetrag: null, kandidaten: [] },
    skonto: skontoVon(zeile),
  }
}

/** Die zwölf Belege als vollständige Details (Basis für `src/mocks/db.ts`). */
export const belege: BelegDetail[] = zeilen.map(baueBeleg)

/** Historie je Beleg-ID; b2 wörtlich aus `BelegDetail.dc.html` (acht Einträge). */
export const belegHistorien: Record<string, AuditEintrag[]> = Object.fromEntries(
  zeilen.map((zeile) => {
    const beleg = belege.find((b) => b.id === belegIds[zeile.schluessel])!
    return [beleg.id, historie(zeile, beleg.korrespondent!.name, beleg.extraktion.quelle, beleg.konfidenz ?? null)]
  }),
)
