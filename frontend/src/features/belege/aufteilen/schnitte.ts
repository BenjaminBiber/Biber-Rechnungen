/**
 * Zerlegt die Seiten 1…`seitenanzahl` an den Schnitten in Teile. Ein Schnitt `n` trennt nach Seite `n`;
 * Schnitte außerhalb von 1…seitenanzahl-1 und doppelte Schnitte werden ignoriert.
 */
export function teileAusSchnitten(seitenanzahl: number, schnitte: number[]): { seiten: number[] }[] {
  const gueltig = [...new Set(schnitte)].filter((s) => s >= 1 && s < seitenanzahl).sort((a, b) => a - b)
  const grenzen = [0, ...gueltig, seitenanzahl]
  return grenzen.slice(1).map((ende, i) => ({
    seiten: Array.from({ length: ende - grenzen[i] }, (_, j) => grenzen[i] + j + 1),
  }))
}

/** Buchstabe eines Teils: 0 → A, 1 → B … */
export function teilBuchstabe(index: number): string {
  return String.fromCharCode(65 + (index % 26))
}

/** „Seiten 1–2“ bzw. „Seite 3“. */
export function seitenText(seiten: number[]): string {
  if (seiten.length === 1) return `Seite ${seiten[0]}`
  return `Seiten ${seiten[0]}–${seiten[seiten.length - 1]}`
}
