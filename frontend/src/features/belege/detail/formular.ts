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
export const FEHLER_SATZ = 'Steuersatz wie 19 oder 7 eingeben'

export function ausBeleg(beleg: BelegDetail): Formular {
  return {
    korrespondentId: beleg.korrespondent?.id ?? null,
    belegart: beleg.belegart,
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

/** Prüft alle Felder; Schlüssel wie im `BelegAendernDto` (Steuerzeilen als `steuerzeilen.0.netto`). */
export function pruefen(f: Formular): FeldFehler {
  const fehler: FeldFehler = {}
  for (const feld of ['rechnungsdatum', 'faelligkeitsdatum'] as const)
    if (f[feld].trim() && !parseDatum(f[feld])) fehler[feld] = FEHLER_DATUM
  if (parseLeistung(f.leistung) === 'ungueltig') fehler.leistung = FEHLER_LEISTUNG
  for (const feld of ['netto', 'steuer', 'brutto'] as const)
    if (f[feld].trim() && parseBetrag(f[feld]) === null) fehler[feld] = FEHLER_BETRAG
  f.steuerzeilen.forEach((z, i) => {
    if (parseSatz(z.satz) === null) fehler[`steuerzeilen.${i}.satz`] = FEHLER_SATZ
    if (parseBetrag(z.netto) === null) fehler[`steuerzeilen.${i}.netto`] = FEHLER_BETRAG
    if (parseBetrag(z.steuer) === null) fehler[`steuerzeilen.${i}.steuer`] = FEHLER_BETRAG
  })
  return fehler
}

/** Baut das `BelegAendernDto`; nur nach erfolgreichem `pruefen` aufrufen. */
export function zuDto(f: Formular, titel: string): BelegAendern {
  const leistung = parseLeistung(f.leistung)
  const betrag = (text: string) => (text.trim() ? parseBetrag(text) : null)
  const datum = (text: string) => (text.trim() ? parseDatum(text) : null)
  return {
    korrespondentId: f.korrespondentId,
    belegart: f.belegart,
    rechnungsnummer: f.rechnungsnummer.trim() || null,
    titel,
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

/** Serverfehler (`errors` mit Feldnamen aus dem DTO, beliebige Groß-/Kleinschreibung) den Feldern zuordnen. */
export function serverFehler(errors: Record<string, string[]> | undefined): FeldFehler {
  const fehler: FeldFehler = {}
  for (const [schluessel, meldungen] of Object.entries(errors ?? {})) {
    const feld = schluessel.charAt(0).toLowerCase() + schluessel.slice(1)
    const ziel = ['leistungsdatum', 'leistungVon', 'leistungBis'].includes(feld) ? 'leistung' : feld
    fehler[ziel] = meldungen[0]
  }
  return fehler
}
