/*
 * Formatierung für Beträge, Zahlen und Daten (de-DE).
 * Zwischen Zahl und Einheit steht ein geschütztes Leerzeichen (U+00A0), wie es Intl liefert.
 */

const LOCALE = 'de-DE'
const ZEITZONE = 'Europe/Berlin'
const NUR_DATUM = /^(\d{4})-(\d{2})-(\d{2})$/

/** Betrag als Währung, z. B. `formatEuro(1704)` → „1.704,00 €“. */
export function formatEuro(wert: number, waehrung = 'EUR'): string {
  return new Intl.NumberFormat(LOCALE, { style: 'currency', currency: waehrung }).format(wert)
}

/** Zahl mit deutschen Trennzeichen, z. B. `formatZahl(1234.5, 2)` → „1.234,50“. */
export function formatZahl(wert: number, nachkommastellen?: number): string {
  return new Intl.NumberFormat(LOCALE, {
    minimumFractionDigits: nachkommastellen,
    maximumFractionDigits: nachkommastellen ?? 3,
  }).format(wert)
}

/**
 * Datum als „TT.MM.JJJJ“. Reine Datumswerte („2026-11-19“) werden ohne Zeitzonen-Umrechnung
 * übernommen, Zeitpunkte („2026-11-19T22:30:00Z“) in deutscher Zeit dargestellt.
 */
export function formatDatum(iso: string): string {
  const nurDatum = NUR_DATUM.exec(iso)
  if (nurDatum) {
    const [, jahr, monat, tag] = nurDatum
    return `${tag}.${monat}.${jahr}`
  }
  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: ZEITZONE,
  }).format(new Date(iso))
}

/** Zeitpunkt als „TT.MM.JJJJ, HH:MM“ in deutscher Zeit. */
export function formatDatumZeit(iso: string): string {
  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: ZEITZONE,
  }).format(new Date(iso))
}

/**
 * Anteil als Prozent mit höchstens einer Nachkommastelle, z. B. `formatProzent(0.123)` → „12,3 %“.
 * Mit Vorzeichen: `formatProzent(0.05, true)` → „+5 %“.
 */
export function formatProzent(anteil: number, mitVorzeichen = false): string {
  return new Intl.NumberFormat(LOCALE, {
    style: 'percent',
    maximumFractionDigits: 1,
    signDisplay: mitVorzeichen ? 'exceptZero' : 'auto',
  }).format(anteil)
}

/** Initialen aus erstem und letztem Wort, z. B. „Benjamin Biber“ → „BB“. */
export function initialen(name: string): string {
  const woerter = name.trim().split(/\s+/).filter(Boolean)
  if (woerter.length === 0) return ''
  const erstes = woerter[0].charAt(0)
  const letztes = woerter.length > 1 ? woerter[woerter.length - 1].charAt(0) : ''
  return (erstes + letztes).toLocaleUpperCase(LOCALE)
}
