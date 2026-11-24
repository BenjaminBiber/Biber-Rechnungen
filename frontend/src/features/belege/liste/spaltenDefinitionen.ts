import { useCallback, useState } from 'react'
import type { BelegSortierung } from '@/api/typen'

export type SpaltenSchluessel =
  | 'rechnungsnummer'
  | 'korrespondent'
  | 'titel'
  | 'kategorie'
  | 'rechnungsdatum'
  | 'faelligkeit'
  | 'brutto'
  | 'seiten'
  | 'herkunft'
  | 'netto'
  | 'leistungsdatum'
  | 'konfidenz'

export type SpaltenDefinition = {
  schluessel: SpaltenSchluessel
  label: string
  /** Sortierung beim Klick auf den Spaltenkopf. */
  sortierung?: BelegSortierung
  /** Beträge rechtsbündig mit Tabellenziffern. */
  betrag?: boolean
  /** Nicht abwählbar. */
  fest?: boolean
}

/** Alle Spalten der Tabelle in Standard-Reihenfolge; Netto, Leistungsdatum und Konfidenz sind Zusatzspalten. */
export const spaltenDefinitionen: SpaltenDefinition[] = [
  { schluessel: 'rechnungsnummer', label: 'Rechnungsnr.' },
  { schluessel: 'korrespondent', label: 'Korrespondent', sortierung: 'Korrespondent', fest: true },
  { schluessel: 'titel', label: 'Titel' },
  { schluessel: 'kategorie', label: 'Kategorie' },
  { schluessel: 'rechnungsdatum', label: 'Rechnungsdatum', sortierung: 'Rechnungsdatum' },
  { schluessel: 'faelligkeit', label: 'Fällig', sortierung: 'Faelligkeit' },
  { schluessel: 'brutto', label: 'Brutto', sortierung: 'Brutto', betrag: true },
  { schluessel: 'seiten', label: 'Seiten' },
  { schluessel: 'herkunft', label: 'Herkunft' },
  { schluessel: 'netto', label: 'Netto', betrag: true },
  { schluessel: 'leistungsdatum', label: 'Leistungsdatum' },
  { schluessel: 'konfidenz', label: 'Konfidenz' },
]

const ZUSATZ: SpaltenSchluessel[] = ['netto', 'leistungsdatum', 'konfidenz']

export const standardSpalten: SpaltenSchluessel[] = spaltenDefinitionen
  .map((d) => d.schluessel)
  .filter((s) => !ZUSATZ.includes(s))

export const SPALTEN_SPEICHER = 'belege.spalten'

function istSchluessel(wert: unknown): wert is SpaltenSchluessel {
  return spaltenDefinitionen.some((d) => d.schluessel === wert)
}

/** Sichtbare Spalten in Reihenfolge; unbekannte fallen weg, Korrespondent ist immer dabei. */
export function spaltenBereinigen(werte: readonly unknown[]): SpaltenSchluessel[] {
  const gueltig = [...new Set(werte.filter(istSchluessel))]
  return gueltig.includes('korrespondent') ? gueltig : ['korrespondent', ...gueltig]
}

function spaltenLesen(): SpaltenSchluessel[] {
  try {
    const roh: unknown = JSON.parse(window.localStorage.getItem(SPALTEN_SPEICHER) ?? 'null')
    return Array.isArray(roh) ? spaltenBereinigen(roh) : standardSpalten
  } catch {
    return standardSpalten
  }
}

/** Sichtbare Tabellenspalten in Reihenfolge, gespeichert in `localStorage` (`belege.spalten`). */
export function useSpalten(): [SpaltenSchluessel[], (spalten: readonly unknown[]) => void] {
  const [spalten, setSpalten] = useState(spaltenLesen)
  const setzen = useCallback((neu: readonly unknown[]) => {
    const bereinigt = spaltenBereinigen(neu)
    window.localStorage.setItem(SPALTEN_SPEICHER, JSON.stringify(bereinigt))
    setSpalten(bereinigt)
  }, [])
  return [spalten, setzen]
}
