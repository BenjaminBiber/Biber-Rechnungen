/** Auswahlrechteck auf einer Belegseite, alle Werte als Anteile 0–1 der Seitenbreite bzw. -höhe. */
export type Rechteck = { x: number; y: number; breite: number; hoehe: number }

/** Kleinste erlaubte Breite und Höhe des Ausschnitts. */
export const MINDESTGROESSE = 0.05

/** Startauswahl oben links – dort steht das Logo meistens. */
export const startAusschnitt: Rechteck = { x: 0.05, y: 0.03, breite: 0.3, hoehe: 0.12 }

const runden = (v: number) => Math.round(v * 10000) / 10000
const klemmen = (v: number, min: number, max: number) => Math.min(Math.max(v, min), max)

/** Rechteck auf Mindestgröße und die Seite (0–1) begrenzen. */
export function begrenzen(r: Rechteck): Rechteck {
  const breite = klemmen(r.breite, MINDESTGROESSE, 1)
  const hoehe = klemmen(r.hoehe, MINDESTGROESSE, 1)
  return {
    x: runden(klemmen(r.x, 0, 1 - breite)),
    y: runden(klemmen(r.y, 0, 1 - hoehe)),
    breite: runden(breite),
    hoehe: runden(hoehe),
  }
}

/** Um `dx`/`dy` verschieben; die Größe bleibt, der Rand der Seite hält das Rechteck auf. */
export function verschieben(r: Rechteck, dx: number, dy: number): Rechteck {
  return begrenzen({ ...r, x: r.x + dx, y: r.y + dy })
}

/** Breite und Höhe um `dBreite`/`dHoehe` ändern; die linke obere Ecke bleibt, wo Platz ist. */
export function skalieren(r: Rechteck, dBreite: number, dHoehe: number): Rechteck {
  const breite = klemmen(r.breite + dBreite, MINDESTGROESSE, 1 - r.x)
  const hoehe = klemmen(r.hoehe + dHoehe, MINDESTGROESSE, 1 - r.y)
  return begrenzen({ ...r, breite, hoehe })
}
