import type { BackupStatus, Dashboard, DiVerbrauch, MonatsKosten, SystemStatusEintrag } from '@/api/typen'
import { artikelIds, belegIds, kategorieIds, korrespondentIds } from './ids'

const softwareHosting = { id: kategorieIds.softwareHosting, name: 'Software & Hosting' }
const telekommunikation = { id: kategorieIds.telekommunikation, name: 'Telekommunikation' }
const buerobedarf = { id: kategorieIds.buerobedarf, name: 'Bürobedarf' }
const fahrzeug = { id: kategorieIds.fahrzeug, name: 'Fahrzeug' }
const hardware = { id: kategorieIds.hardware, name: 'Hardware' }
const sonstiges = { id: kategorieIds.sonstiges, name: 'Sonstiges' }

/** Ein Monat; `werte` in der Reihenfolge Software & Hosting, Telekommunikation, Bürobedarf, Fahrzeug, Hardware, Sonstiges. */
function monatsKosten(
  monat: string,
  summeVorjahr: number,
  [sw, tk, bb, fz, hw, so]: [number, number, number, number, number, number],
): MonatsKosten {
  return {
    monat,
    summe: sw + tk + bb + fz + hw + so,
    summeVorjahr,
    nachKategorie: [
      { kategorie: softwareHosting, farbe: '#2F2D86', summe: sw },
      { kategorie: telekommunikation, farbe: '#5B8DEF', summe: tk },
      { kategorie: buerobedarf, farbe: '#E3893B', summe: bb },
      { kategorie: fahrzeug, farbe: '#1F8A70', summe: fz },
      { kategorie: hardware, farbe: '#9AA4B5', summe: hw },
      { kategorie: sonstiges, farbe: '#CFCEEA', summe: so },
    ],
  }
}

/**
 * Gesamtkosten pro Monat 2026 aus `Main.dc.html` (`renderVals().data`, Zeilen = Kategorien k1–k6);
 * Summen stimmen mit `MobilDashboard.dc.html` überein. Vorjahreswerte ergänzt, passend zu +5,8 % ggü. Nov 2025 und +9,7 % ggü. Jan–Nov 2025.
 */
const kostenProMonat: MonatsKosten[] = [
  monatsKosten('2026-01', 1550, [452, 184, 312, 540, 0, 218]),
  monatsKosten('2026-02', 2470, [452, 179, 96, 610, 1240, 142]),
  monatsKosten('2026-03', 1387, [468, 184, 148, 420, 0, 306]),
  monatsKosten('2026-04', 1476, [468, 192, 402, 388, 0, 174]),
  monatsKosten('2026-05', 1748, [468, 184, 88, 702, 386, 96]),
  monatsKosten('2026-06', 1359, [471, 184, 126, 455, 0, 260]),
  monatsKosten('2026-07', 1446, [471, 188, 233, 512, 0, 188]),
  monatsKosten('2026-08', 2999, [471, 184, 94, 620, 1790, 142]),
  monatsKosten('2026-09', 1444, [489, 184, 318, 388, 0, 210]),
  monatsKosten('2026-10', 1512, [489, 184, 142, 470, 214, 165]),
  monatsKosten('2026-11', 1611, [489, 186, 264, 534, 0, 231]),
  monatsKosten('2026-12', 1650, [0, 0, 0, 0, 0, 0]),
]

/** Document-Intelligence-Verbrauch November 2026 (`Einstellungen.dc.html`, Glocke in `Topbar.dc.html`). */
export const diVerbrauch: DiVerbrauch = {
  monat: '2026-11',
  seiten: 412,
  limit: 500,
  prozent: 82,
  pausiert: false,
  wartend: 0,
}

/** Dashboard „Jahr 2026“, Stand 23.11.2026, aus `Main.dc.html` (Kacheln, Diagramm, Top-Korrespondenten, Listen). */
export const dashboard: Dashboard = {
  kosten: { anzahl: 5, betrag: 1704, aenderungProzent: 5.8, vergleichszeitraum: 'Nov 2025', aeltesteTage: null },
  kostenJahr: {
    anzahl: 59,
    betrag: 20845,
    aenderungProzent: 9.7,
    vergleichszeitraum: 'Jan–Nov 2025',
    aeltesteTage: null,
  },
  offen: { anzahl: 9, betrag: 2418.4, aenderungProzent: null, vergleichszeitraum: null, aeltesteTage: null },
  ueberfaellig: { anzahl: 2, betrag: 412.3, aenderungProzent: null, vergleichszeitraum: null, aeltesteTage: 26 },
  inbox: { anzahl: 12, betrag: null, aenderungProzent: null, vergleichszeitraum: null, aeltesteTage: null },
  zuPruefen: { anzahl: 5, betrag: null, aenderungProzent: null, vergleichszeitraum: null, aeltesteTage: null },
  kostenProMonat,
  topKorrespondenten: [
    {
      korrespondent: { id: korrespondentIds.autohausBrenner, name: 'Autohaus Brenner GmbH' },
      logo: { url: `/api/korrespondenten/${korrespondentIds.autohausBrenner}/logo`, initialen: 'AB', quelle: 'Pdf' },
      summe: 5639,
      anzahlBelege: 22,
    },
    {
      korrespondent: { id: korrespondentIds.hostwerkSued, name: 'Hostwerk Süd GmbH' },
      logo: { url: `/api/korrespondenten/${korrespondentIds.hostwerkSued}/logo`, initialen: 'HS', quelle: 'Favicon' },
      summe: 5188,
      anzahlBelege: 11,
    },
    {
      korrespondent: { id: korrespondentIds.hardwareversandNord, name: 'Hardwareversand Nord GmbH' },
      logo: {
        url: `/api/korrespondenten/${korrespondentIds.hardwareversandNord}/logo`,
        initialen: 'HN',
        quelle: 'Favicon',
      },
      summe: 3630,
      anzahlBelege: 4,
    },
    {
      korrespondent: { id: korrespondentIds.bueroweltKraemer, name: 'Bürowelt Krämer KG' },
      logo: { url: `/api/korrespondenten/${korrespondentIds.bueroweltKraemer}/logo`, initialen: 'BK', quelle: 'Pdf' },
      summe: 2223,
      anzahlBelege: 31,
    },
    {
      korrespondent: { id: korrespondentIds.netzfunkTelekom, name: 'Netzfunk Telekom AG' },
      logo: { url: `/api/korrespondenten/${korrespondentIds.netzfunkTelekom}/logo`, initialen: 'NT', quelle: 'Upload' },
      summe: 2033,
      anzahlBelege: 11,
    },
  ],
  zuPruefenListe: [
    {
      belegId: belegIds.b2,
      korrespondent: 'Bürowelt Krämer KG',
      rechnungsnummer: '2026-48817',
      brutto: 264,
      grund: 'Konfidenz 0,74 unter 0,80 · Positionssumme weicht um 2,00 € ab',
    },
    {
      belegId: belegIds.b5,
      korrespondent: 'Stadtwerke Lindenau',
      rechnungsnummer: '2026-1187',
      brutto: 118.4,
      grund: 'KoSIT: BR-DE-15 Käuferreferenz fehlt',
    },
    {
      belegId: belegIds.b6,
      korrespondent: 'Druckerei Weller',
      rechnungsnummer: '26-0912',
      brutto: 231,
      grund: '§ 14 UStG: Leistungsdatum fehlt',
    },
    {
      belegId: belegIds.b7,
      korrespondent: 'Kanzlei Hofmann & Partner',
      rechnungsnummer: 'HP-2026-342',
      brutto: 452.2,
      grund: 'Dublettenverdacht zu HP-2026-311',
    },
    {
      belegId: belegIds.b8,
      korrespondent: 'Elektro Haas e.K.',
      rechnungsnummer: 'EH-4471',
      brutto: 178.5,
      grund: 'Neuer Korrespondent',
    },
  ],
  offeneBelege: [
    {
      belegId: belegIds.b8,
      korrespondent: 'Elektro Haas e.K.',
      rechnungsnummer: 'EH-4471',
      brutto: 178.5,
      faelligkeitsdatum: '2026-10-28',
      ueberfaellig: true,
    },
    {
      belegId: belegIds.b9,
      korrespondent: 'Stadtwerke Lindenau',
      rechnungsnummer: '2026-1102',
      brutto: 233.8,
      faelligkeitsdatum: '2026-11-15',
      ueberfaellig: true,
    },
    {
      belegId: belegIds.b7,
      korrespondent: 'Kanzlei Hofmann & Partner',
      rechnungsnummer: 'HP-2026-342',
      brutto: 452.2,
      faelligkeitsdatum: '2026-11-24',
      ueberfaellig: false,
    },
    {
      belegId: belegIds.b6,
      korrespondent: 'Druckerei Weller',
      rechnungsnummer: '26-0912',
      brutto: 231,
      faelligkeitsdatum: '2026-11-26',
      ueberfaellig: false,
    },
  ],
  // Rechnet der Handler aus den Belegen (`skonto` im Detail), damit Bezahlen oder Ändern die Kachel mitzieht.
  skonto: { anzahl: 0, ersparnis: 0, eintraege: [] },
  preisaenderungen: [
    {
      id: artikelIds.a4,
      name: 'Business Mobil L',
      istStammartikel: false,
      korrespondent: 'Netzfunk Telekom AG',
      preisAlt: 45.65,
      preisNeu: 39.95,
      einheit: 'Monat',
      aenderung: -12.5,
    },
    {
      id: artikelIds.a2,
      name: 'Ordner breit 8 cm, blau',
      istStammartikel: false,
      korrespondent: 'Bürowelt Krämer KG',
      preisAlt: 2.24,
      preisNeu: 2.49,
      einheit: 'Stück',
      aenderung: 11.2,
    },
    {
      id: artikelIds.a1,
      name: 'Kopierpapier A4 80 g',
      istStammartikel: false,
      korrespondent: 'Bürowelt Krämer KG',
      preisAlt: 22.99,
      preisNeu: 24.9,
      einheit: 'Karton',
      aenderung: 8.3,
    },
    {
      id: artikelIds.a3,
      name: 'Managed Server M',
      istStammartikel: false,
      korrespondent: 'Hostwerk Süd GmbH',
      preisAlt: 289,
      preisNeu: 309,
      einheit: 'Monat',
      aenderung: 6.9,
    },
    {
      id: artikelIds.a8,
      name: 'Toner TN-schwarz kompatibel',
      istStammartikel: false,
      korrespondent: 'Hardwareversand Nord GmbH',
      preisAlt: 42,
      preisNeu: 39.9,
      einheit: 'Stück',
      aenderung: -5.0,
    },
  ],
  vorsteuer: null,
  diVerbrauch,
}

/** Karte „Ablage & Sicherung“ aus `Main.dc.html`; Zeitpunkte am 23.11.2026 (MEZ → UTC). */
export const systemStatus: SystemStatusEintrag[] = [
  {
    schluessel: 'integritaet',
    schweregrad: 'Ok',
    titel: 'Integritätsprüfung ohne Befund',
    text: '2.184 Dateien, SHA-256 geprüft',
    zeitpunkt: '2026-11-23T02:00:00Z',
  },
  {
    schluessel: 'backup',
    schweregrad: 'Ok',
    titel: 'Datenbank-Backup erstellt',
    text: 'pg_dump · 30 Dumps vorhanden',
    zeitpunkt: '2026-11-23T01:00:00Z',
  },
  {
    schluessel: 'postfach',
    schweregrad: 'Info',
    titel: 'Postfach abgerufen',
    text: '2 neue Anhänge',
    zeitpunkt: '2026-11-23T08:46:00Z',
  },
  {
    schluessel: 'eingangsordner',
    schweregrad: 'Info',
    titel: 'Eingangsordner überwacht',
    text: 'Rechnungen-Eingang · 0 Dateien wartend',
    zeitpunkt: null,
  },
]

/** Letztes DB-Backup: `Main.dc.html` („pg_dump heute 02:00 · 30 Dumps“) und `Einstellungen.dc.html` („48 MB“). */
export const backupStatus: BackupStatus = {
  letztesBackup: '2026-11-23T01:00:00Z',
  groesse: 48 * 1024 * 1024,
  anzahlDumps: 30,
}
