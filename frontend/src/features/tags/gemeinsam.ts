import type { ChipTon } from '@/components/Chip'

export type Reiter = 'tags' | 'kategorien'

export const reiterListe: Reiter[] = ['tags', 'kategorien']

/** Die sieben Farbfelder des Designs: Tags speichern den Chip-Ton, Kategorien den Hex-Wert. */
export const farben: { name: string; ton: ChipTon; hex: string }[] = [
  { name: 'Blau', ton: 'blau', hex: '#5B8DEF' },
  { name: 'Petrol', ton: 'petrol', hex: '#1F8A70' },
  { name: 'Grün', ton: 'gruen', hex: '#3BA55C' },
  { name: 'Orange', ton: 'orange', hex: '#E3893B' },
  { name: 'Rot', ton: 'rot', hex: '#D64541' },
  { name: 'Lila', ton: 'lila', hex: '#8B5CF6' },
  { name: 'Grau', ton: 'grau', hex: '#9AA4B5' },
]

const toene = new Set<string>(farben.map((f) => f.ton))

/** Chip-Ton eines Tags; unbekannte Werte werden grau. */
export function tagTon(farbe: string): ChipTon {
  return toene.has(farbe) ? (farbe as ChipTon) : 'grau'
}
