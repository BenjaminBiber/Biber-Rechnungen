import { useCallback, useState } from 'react'
import type { BelegSortierung } from '@/api/typen'

export type SpaltenSchluessel =
  | 'rechnungsnummer'
  | 'korrespondent'
  | 'titel'
  | 'tags'
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
  { schluessel: 'rechnungsnummer', label: 'Rechnungsnr.', sortierung: 'Rechnungsnummer' },
  { schluessel: 'korrespondent', label: 'Korrespondent', sortierung: 'Korrespondent', fest: true },
  { schluessel: 'titel', label: 'Titel', sortierung: 'Titel' },
  { schluessel: 'tags', label: 'Tags' },
  { schluessel: 'kategorie', label: 'Kategorie', sortierung: 'Kategorie' },
  { schluessel: 'rechnungsdatum', label: 'Rechnungsdatum', sortierung: 'Rechnungsdatum' },
  { schluessel: 'faelligkeit', label: 'Fällig', sortierung: 'Faelligkeit' },
  { schluessel: 'brutto', label: 'Brutto', sortierung: 'Brutto', betrag: true },
  { schluessel: 'seiten', label: 'Seiten', sortierung: 'Seiten' },
  { schluessel: 'herkunft', label: 'Herkunft', sortierung: 'Herkunft' },
  { schluessel: 'netto', label: 'Netto', sortierung: 'Netto', betrag: true },
  { schluessel: 'leistungsdatum', label: 'Leistungsdatum', sortierung: 'Leistungsdatum' },
  { schluessel: 'konfidenz', label: 'Konfidenz', sortierung: 'Konfidenz' },
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

/** Merker, dass gespeicherte Spaltenlisten die (neue) Tags-Spalte schon einmal angeboten bekommen haben. */
const TAGS_MIGRIERT = 'belege.spalten.tags'

/** Gespeicherte Listen von vor der Tags-Spalte bekommen sie einmalig hinter „Titel“ eingefügt. */
function tagsNachruesten(werte: unknown[]): unknown[] {
  if (window.localStorage.getItem(TAGS_MIGRIERT)) return werte
  window.localStorage.setItem(TAGS_MIGRIERT, '1')
  if (werte.includes('tags')) return werte
  const i = werte.indexOf('titel')
  const neu = i < 0 ? [...werte, 'tags'] : [...werte.slice(0, i + 1), 'tags', ...werte.slice(i + 1)]
  window.localStorage.setItem(SPALTEN_SPEICHER, JSON.stringify(spaltenBereinigen(neu)))
  return neu
}

function spaltenLesen(): SpaltenSchluessel[] {
  try {
    const roh: unknown = JSON.parse(window.localStorage.getItem(SPALTEN_SPEICHER) ?? 'null')
    return Array.isArray(roh) ? spaltenBereinigen(tagsNachruesten(roh)) : standardSpalten
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
