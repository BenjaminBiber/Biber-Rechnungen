import type { Merkmal } from '@/api/typen'

/** Übliche Merkmalnamen als Vorschlag beim Bearbeiten; das Backend vereinheitlicht Schreibweisen wie „Gr.“. */
export const MERKMAL_NAMEN = ['Größe', 'Farbe', 'Länge', 'Ausführung'] as const

/** Spalte für Merkmale ohne Namen. */
const OHNE_NAMEN = ''

export type MerkmalSpalte = { name: string; titel: string }

export type MerkmalAufteilung = {
  /** Merkmale, die sich zwischen den Zeilen unterscheiden (oder nicht überall vorkommen) – je eine Spalte. */
  spalten: MerkmalSpalte[]
  /** Merkmale, die in allen Zeilen denselben Wert haben. */
  gemeinsam: Merkmal[]
  /** Werte je Zeile, nach Spaltenname. */
  werte: Map<string, string>[]
}

/** Werte einer Zeile nach Name; mehrere Merkmale gleichen Namens (oder ohne Namen) mit „ / “ verbunden. */
function nachName(merkmale: readonly Merkmal[]): Map<string, string> {
  const werte = new Map<string, string>()
  for (const m of merkmale) {
    const name = m.name ?? OHNE_NAMEN
    const bisher = werte.get(name)
    werte.set(name, bisher === undefined ? m.wert : `${bisher} / ${m.wert}`)
  }
  return werte
}

/**
 * Teilt die Merkmale mehrerer Zeilen (z. B. der Käufe eines Artikels) in Spalten und gemeinsame Merkmale:
 * Bei Schuhen in Größe 42 und 47, beide „grau/marine/schwarz“, wird „Größe“ eine Spalte und „Farbe“ gemeinsam.
 */
export function merkmaleAufteilen(zeilen: readonly { merkmale: readonly Merkmal[] }[]): MerkmalAufteilung {
  const werte = zeilen.map((z) => nachName(z.merkmale))
  const namen = [...new Set(werte.flatMap((w) => [...w.keys()]))]
  const spalten: MerkmalSpalte[] = []
  const gemeinsam: Merkmal[] = []
  for (const name of namen) {
    const erster = werte[0]?.get(name)
    if (erster !== undefined && werte.every((w) => w.get(name) === erster)) {
      gemeinsam.push({ name: name || null, wert: erster })
    } else {
      spalten.push({ name, titel: name || 'Merkmal' })
    }
  }
  return { spalten, gemeinsam, werte }
}
