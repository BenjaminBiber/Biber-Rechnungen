import type { BelegAendern, BelegDetail, Belegart } from '@/api/typen'
import {
  betragEingabe,
  datumEingabe,
  leistungEingabe,
  parseBetrag,
  parseDatum,
  parseLeistung,
  parseSatz,
  satzEingabe,
} from './gemeinsam'

/** Eingaben des Tabs „Daten“ als Text, wie sie in den Feldern stehen. */
export type Formular = {
  korrespondentId: string | null
  belegart: Belegart
  titel: string
  rechnungsnummer: string
  rechnungsdatum: string
  leistung: string
  faelligkeitsdatum: string
  kategorieId: string | null
  waehrung: string
  netto: string
  steuer: string
  brutto: string
  steuerzeilen: { satz: string; netto: string; steuer: string }[]
}

export type FeldFehler = Partial<Record<string, string>>

export const FEHLER_DATUM = 'Datum im Format TT.MM.JJJJ eingeben'
export const FEHLER_LEISTUNG = 'Datum oder Zeitraum „TT.MM.JJJJ – TT.MM.JJJJ“ eingeben'
export const FEHLER_BETRAG = 'Betrag im Format 1.234,56 eingeben'
export const MAX_TITEL = 500
export const FEHLER_TITEL = `Höchstens ${MAX_TITEL} Zeichen`
export const FEHLER_SATZ = 'Steuersatz wie 19 oder 7 eingeben'
export const FEHLER_NEGATIV = 'Negative Beträge gibt es nur bei Gutschriften'
export const fehlerSatzDoppelt = (zeile: number) => `Den Steuersatz gibt es schon in Zeile ${zeile}`

export function ausBeleg(beleg: BelegDetail): Formular {
  return {
    korrespondentId: beleg.korrespondent?.id ?? null,
    belegart: beleg.belegart,
    titel: beleg.titel,
    rechnungsnummer: beleg.rechnungsnummer ?? '',
    rechnungsdatum: datumEingabe(beleg.rechnungsdatum),
    leistung: leistungEingabe(beleg),
    faelligkeitsdatum: datumEingabe(beleg.faelligkeitsdatum),
    kategorieId: beleg.kategorie?.id ?? null,
    waehrung: beleg.waehrung,
    netto: betragEingabe(beleg.netto),
    steuer: betragEingabe(beleg.steuer),
    brutto: betragEingabe(beleg.brutto),
    steuerzeilen: beleg.steuerzeilen.map((z) => ({
      satz: satzEingabe(z.satz),
      netto: betragEingabe(z.netto),
      steuer: betragEingabe(z.steuer),
    })),
  }
}

export function gleich(a: Formular, b: Formular): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

/**
 * Prüft alle Felder; Schlüssel wie im `BelegAendernDto` (Steuerzeilen als `steuerzeilen.0.netto`). Enthält auch die
 * Regeln des Servers (keine negativen Beträge außer bei Gutschriften, jeder Steuersatz nur einmal), damit deren Fehler
 * am Feld stehen statt erst nach dem Speichern.
 */
export function pruefen(f: Formular): FeldFehler {
  const fehler: FeldFehler = {}
  const negativErlaubt = f.belegart === 'Gutschrift'
  const betrag = (schluessel: string, text: string) => {
    const wert = parseBetrag(text)
    if (wert === null) fehler[schluessel] = FEHLER_BETRAG
    else if (wert < 0 && !negativErlaubt) fehler[schluessel] = FEHLER_NEGATIV
  }
  if (f.titel.trim().length > MAX_TITEL) fehler.titel = FEHLER_TITEL
  for (const feld of ['rechnungsdatum', 'faelligkeitsdatum'] as const)
    if (f[feld].trim() && !parseDatum(f[feld])) fehler[feld] = FEHLER_DATUM
  if (parseLeistung(f.leistung) === 'ungueltig') fehler.leistung = FEHLER_LEISTUNG
  for (const feld of ['netto', 'steuer', 'brutto'] as const) if (f[feld].trim()) betrag(feld, f[feld])
  const saetze: (number | null)[] = []
  f.steuerzeilen.forEach((z, i) => {
    const satz = parseSatz(z.satz)
    const frueher = satz === null ? -1 : saetze.indexOf(satz)
    if (satz === null) fehler[`steuerzeilen.${i}.satz`] = FEHLER_SATZ
    else if (frueher >= 0) fehler[`steuerzeilen.${i}.satz`] = fehlerSatzDoppelt(frueher + 1)
    saetze.push(satz)
    betrag(`steuerzeilen.${i}.netto`, z.netto)
    betrag(`steuerzeilen.${i}.steuer`, z.steuer)
  })
  return fehler
}

/** Steuersatz für eine neue Steuerzeile: 19, 7 oder 0 – der erste, den es noch nicht gibt (sonst leer). */
export function neuerSatz(zeilen: Formular['steuerzeilen']): string {
  const vorhanden = zeilen.map((z) => parseSatz(z.satz))
  return ['19', '7', '0'].find((s) => !vorhanden.includes(parseSatz(s))) ?? ''
}

/** Text im Feld zu einem Prüfschlüssel (`titel`, `steuerzeilen.0.netto`); `''` für Schlüssel ohne Feld. */
export function feldText(f: Formular, schluessel: string): string {
  const wert = schluessel
    .split('.')
    .reduce<unknown>((w, teil) => (w && typeof w === 'object' ? (w as Record<string, unknown>)[teil] : undefined), f)
  return typeof wert === 'string' ? wert : ''
}

/** Schlüssel, deren Fehler das Formular an einem Feld oder im Bereich „Beträge“ anzeigt. */
const ANGEZEIGT =
  /^(titel|korrespondentId|belegart|rechnungsnummer|rechnungsdatum|leistung|faelligkeitsdatum|kategorieId|waehrung|netto|steuer|brutto|steuerzeilen(\.\d+(\.(satz|netto|steuer))?)?)$/

/** Fehler ohne eigenes Feld (etwa unbekannte Schlüssel vom Server): zeigt das Formular gesammelt, sonst blockierten sie unsichtbar. */
export function fehlerOhneFeld(fehler: FeldFehler): string[] {
  return Object.entries(fehler).flatMap(([schluessel, meldung]) =>
    meldung && !ANGEZEIGT.test(schluessel) ? [meldung] : [],
  )
}

/**
 * Baut das `BelegAendernDto`; nur nach erfolgreichem `pruefen` aufrufen. Ein leerer Titel lässt den Server wieder
 * Rechnungsnummer bzw. Korrespondent anzeigen.
 */
export function zuDto(f: Formular): BelegAendern {
  const leistung = parseLeistung(f.leistung)
  const betrag = (text: string) => (text.trim() ? parseBetrag(text) : null)
  const datum = (text: string) => (text.trim() ? parseDatum(text) : null)
  return {
    korrespondentId: f.korrespondentId,
    belegart: f.belegart,
    rechnungsnummer: f.rechnungsnummer.trim() || null,
    titel: f.titel.trim(),
    rechnungsdatum: datum(f.rechnungsdatum),
    ...(leistung === 'ungueltig' ? { leistungsdatum: null, leistungVon: null, leistungBis: null } : leistung),
    faelligkeitsdatum: datum(f.faelligkeitsdatum),
    kategorieId: f.kategorieId,
    waehrung: f.waehrung,
    netto: betrag(f.netto),
    steuer: betrag(f.steuer),
    brutto: betrag(f.brutto),
    steuerzeilen: f.steuerzeilen.map((z) => ({
      satz: parseSatz(z.satz)!,
      netto: parseBetrag(z.netto)!,
      steuer: parseBetrag(z.steuer)!,
    })),
  }
}

/**
 * Serverfehler (`errors` mit Feldnamen aus dem DTO, beliebige Groß-/Kleinschreibung) den Feldern zuordnen. Pfade wie
 * `Steuerzeilen[0].Satz` werden zu `steuerzeilen.0.satz`, den Schlüsseln von `pruefen`.
 */
export function serverFehler(errors: Record<string, string[]> | undefined): FeldFehler {
  const fehler: FeldFehler = {}
  for (const [schluessel, meldungen] of Object.entries(errors ?? {})) {
    const feld = schluessel
      .replace(/^\$\./, '')
      .replace(/\[(\d+)\]/g, '.$1')
      .split('.')
      .map((teil) => teil.charAt(0).toLowerCase() + teil.slice(1))
      .join('.')
    const ziel = ['leistungsdatum', 'leistungVon', 'leistungBis'].includes(feld) ? 'leistung' : feld
    fehler[ziel] ??= meldungen[0]
  }
  return fehler
}
