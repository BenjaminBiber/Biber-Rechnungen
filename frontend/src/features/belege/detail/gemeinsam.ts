import type { BelegAnsicht, BelegDatei, BelegDetail, BelegeAbfrage, PruefBefund } from '@/api/typen'
import { formatZahl } from '@/lib/format'

/** Abweichung, bis zu der Netto × Satz und Steuer einer Steuerzeile als „stimmt“ gelten. */
export const STEUER_TOLERANZ = 0.02

/** Anzeigenamen der Ansichten der Belegliste („Zurück zu Inbox“, „3 von 12 in Inbox“). */
export const ansichtNamen: Record<BelegAnsicht, string> = {
  Alle: 'Alle',
  Inbox: 'Inbox',
  ZuPruefen: 'Zu prüfen',
  Offen: 'Offen',
  Ueberfaellig: 'Überfällig',
}

/** Suchparameter `aus`: Filter der Liste, aus der der Beleg geöffnet wurde. */
export type BelegDetailSuche = { aus?: BelegeAbfrage }

export function original(beleg: BelegDetail): BelegDatei | undefined {
  return beleg.dateien.find((d) => d.rolle === 'Original')
}

export function istXml(datei: BelegDatei | undefined): boolean {
  return !!datei && /xml/i.test(datei.mimeType)
}

export function istPdf(datei: BelegDatei | undefined): boolean {
  return datei?.mimeType === 'application/pdf'
}

/** XML der E-Rechnung: eingebettet (ZUGFeRD) oder das Original selbst (reine XRechnung). */
export function xmlDatei(beleg: BelegDetail): BelegDatei | undefined {
  if (!beleg.istERechnung) return undefined
  return (
    beleg.dateien.find((d) => d.rolle === 'EingebettetesXml') ?? (istXml(original(beleg)) ? original(beleg) : undefined)
  )
}

/** „zu prüfen“ ist der System-Tag; nur „Bestätigen“ entfernt ihn. */
export function istZuPruefen(beleg: BelegDetail): boolean {
  return beleg.tags.some((t) => t.istSystem)
}

export function fehlgeschlageneBefunde(beleg: BelegDetail): PruefBefund[] {
  return beleg.befunde.filter((b) => b.ergebnis === 'Fehlgeschlagen')
}

const DE_DATUM = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/

/** „19.11.2026“ → „2026-11-19“; `null` bei ungültiger Eingabe (auch 31.02.). */
export function parseDatum(eingabe: string): string | null {
  const treffer = DE_DATUM.exec(eingabe.trim())
  if (!treffer) return null
  const [, tag, monat, jahr] = treffer.map(Number)
  const datum = new Date(Date.UTC(jahr, monat - 1, tag))
  if (datum.getUTCFullYear() !== jahr || datum.getUTCMonth() !== monat - 1 || datum.getUTCDate() !== tag) return null
  return `${jahr}-${String(monat).padStart(2, '0')}-${String(tag).padStart(2, '0')}`
}

/** „2026-11-19“ → „19.11.2026“, leer bleibt leer. */
export function datumEingabe(iso: string | null | undefined): string {
  if (!iso) return ''
  const [jahr, monat, tag] = iso.split('-')
  return `${tag}.${monat}.${jahr}`
}

const DE_BETRAG = /^-?(\d{1,3}(\.\d{3})+|\d+)(,\d{1,2})?$/

/** „1.221,85“ → 1221.85; `null` bei ungültiger Eingabe. Ein Punkt gilt nur als Tausendertrenner. */
export function parseBetrag(eingabe: string): number | null {
  const text = eingabe.trim().replace(/\s*€$/, '')
  if (!DE_BETRAG.test(text)) return null
  return Number(text.replace(/\./g, '').replace(',', '.'))
}

/** 221.85 → „221,85“, leer bleibt leer. */
export function betragEingabe(wert: number | null | undefined): string {
  return wert === null || wert === undefined ? '' : formatZahl(wert, 2)
}

/** Steuersatz „19“, „7,5“ oder „19 %“ → Zahl. */
export function parseSatz(eingabe: string): number | null {
  const text = eingabe.trim().replace(/\s*%$/, '')
  if (!/^\d{1,2}(,\d{1,2})?$/.test(text)) return null
  return Number(text.replace(',', '.'))
}

export function satzEingabe(satz: number): string {
  return formatZahl(satz)
}

/** Netto × Satz stimmt mit der Steuer überein (Toleranz 0,02 €). */
export function steuerStimmt(satz: number, netto: number, steuer: number): boolean {
  const erwartet = Math.round(netto * satz) / 100
  return Math.abs(erwartet - steuer) <= STEUER_TOLERANZ + 1e-9
}

/** Leistungsfeld: ein Datum oder „von – bis“. */
export type Leistung =
  | { leistungsdatum: string | null; leistungVon: null; leistungBis: null }
  | { leistungsdatum: null; leistungVon: string; leistungBis: string }

export function parseLeistung(eingabe: string): Leistung | 'ungueltig' {
  const text = eingabe.trim()
  if (!text) return { leistungsdatum: null, leistungVon: null, leistungBis: null }
  const teile = text.split(/\s*[–-]\s*(?=\d)/)
  if (teile.length === 1) {
    const datum = parseDatum(text)
    return datum ? { leistungsdatum: datum, leistungVon: null, leistungBis: null } : 'ungueltig'
  }
  if (teile.length !== 2) return 'ungueltig'
  const von = parseDatum(teile[0])
  const bis = parseDatum(teile[1])
  if (!von || !bis || von > bis) return 'ungueltig'
  return { leistungsdatum: null, leistungVon: von, leistungBis: bis }
}

export function leistungEingabe(beleg: Pick<BelegDetail, 'leistungsdatum' | 'leistungVon' | 'leistungBis'>): string {
  if (beleg.leistungVon && beleg.leistungBis)
    return `${datumEingabe(beleg.leistungVon)} – ${datumEingabe(beleg.leistungBis)}`
  return datumEingabe(beleg.leistungsdatum)
}

export function seitenText(anzahl: number): string {
  return anzahl === 1 ? '1 Seite' : `${anzahl} Seiten`
}
