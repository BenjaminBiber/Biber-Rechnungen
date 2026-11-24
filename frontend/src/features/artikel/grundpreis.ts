/**
 * Grundpreis je `menge` Basiseinheiten, auf Cent gerundet:
 * `grundpreis(24.9, 2500, 100)` → 1 (24,90 € ÷ 2500 Blatt × 100).
 */
export function grundpreis(einzelpreis: number, faktor: number, menge: number): number {
  if (!(faktor > 0)) throw new RangeError('Der Umrechnungsfaktor muss größer als 0 sein.')
  return Math.round(((einzelpreis / faktor) * menge + Number.EPSILON) * 100) / 100
}
