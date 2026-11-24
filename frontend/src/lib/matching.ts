import type { Matching, MatchingAlgorithmus } from '@/api/typen'

/** Auswahltexte des Matching-Editors in der Reihenfolge des Designs. */
export const matchingAlgorithmen: { wert: MatchingAlgorithmus; text: string }[] = [
  { wert: 'EinWort', text: 'Ein Wort trifft zu' },
  { wert: 'AlleWoerter', text: 'Alle Wörter treffen zu' },
  { wert: 'Exakt', text: 'Exakte Zeichenfolge' },
  { wert: 'Regex', text: 'Regulärer Ausdruck' },
  { wert: 'Unscharf', text: 'Unscharf (ähnlich)' },
  { wert: 'Auto', text: 'Auto (ML.NET, aus bisherigen Zuweisungen)' },
  { wert: 'Keiner', text: 'Keiner' },
]

const kurzname: Record<MatchingAlgorithmus, string> = {
  EinWort: 'Ein Wort',
  AlleWoerter: 'Alle Wörter',
  Exakt: 'Exakt',
  Regex: 'Regex',
  Unscharf: 'Unscharf',
  Auto: 'Auto',
  Keiner: 'Keiner',
}

/** Algorithmus ohne Muster (Musterfeld ausgeblendet). */
export function ohneMuster(algorithmus: MatchingAlgorithmus): boolean {
  return algorithmus === 'Keiner' || algorithmus === 'Auto'
}

/** Kurzform für Tabellen, z. B. „Ein Wort: paypal“, „Auto“, „Keiner“, „Regex“. */
export function matchingKurztext(m: Matching): string {
  const name = kurzname[m.algorithmus]
  if (ohneMuster(m.algorithmus) || m.algorithmus === 'Regex' || !m.muster) return name
  return `${name}: ${m.muster}`
}
