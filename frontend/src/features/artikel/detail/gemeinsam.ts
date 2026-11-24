import type { PreisDetail } from '@/api/typen'
import { formatZahl } from '@/lib/format'

export type Preisart = 'grund' | 'einzel'

export const preisarten: Preisart[] = ['grund', 'einzel']

const kuerzel: Record<string, string> = { Blatt: 'Bl.' }

/** Kurzform einer Basiseinheit für Beträge und Mengen, z. B. „Blatt“ → „Bl.“. */
export function einheitKurz(einheit?: string | null): string {
  if (!einheit) return ''
  return kuerzel[einheit] ?? einheit
}

/** „100 Bl.“ – Bezugsmenge des Grundpreises. */
export function grundpreisBezug(detail: Pick<PreisDetail, 'basiseinheit' | 'grundpreisMenge'>): string {
  return `${formatZahl(detail.grundpreisMenge ?? 1)} ${einheitKurz(detail.basiseinheit)}`.trim()
}

/** Ob das Detail Grundpreise kennt; beim einzelnen Artikel ohne Basiseinheit zählen nur Einzelpreise. */
export function hatGrundpreis(detail: PreisDetail): boolean {
  return (
    detail.kennzahlen.guenstigsterGrundpreis != null ||
    detail.serien.some((s) => s.punkte.some((p) => p.grundpreis != null))
  )
}

export type DetailSuche = { jahr?: number; preisart?: Preisart }

/** Suchparameter `jahr` und `preisart` (`grund|einzel`) beider Detailrouten. */
export function detailSuche(suche: Record<string, unknown>): DetailSuche {
  const jahr = Number(suche.jahr)
  const preisart = preisarten.find((p) => p === suche.preisart)
  return {
    ...(Number.isInteger(jahr) && jahr > 1900 ? { jahr } : {}),
    ...(preisart ? { preisart } : {}),
  }
}
