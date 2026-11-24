import { describe, expect, it } from 'vitest'
import { begrenzen, MINDESTGROESSE, skalieren, verschieben, type Rechteck } from './ausschnitt'

const r: Rechteck = { x: 0.1, y: 0.1, breite: 0.3, hoehe: 0.2 }

describe('ausschnitt', () => {
  it('verschiebt innerhalb der Seite', () => {
    expect(verschieben(r, 0.05, -0.02)).toEqual({ x: 0.15, y: 0.08, breite: 0.3, hoehe: 0.2 })
  })

  it('hält beim Verschieben am Rand an, ohne die Größe zu ändern', () => {
    expect(verschieben(r, -1, -1)).toEqual({ x: 0, y: 0, breite: 0.3, hoehe: 0.2 })
    expect(verschieben(r, 2, 2)).toEqual({ x: 0.7, y: 0.8, breite: 0.3, hoehe: 0.2 })
  })

  it('skaliert und begrenzt auf den verbleibenden Platz', () => {
    expect(skalieren(r, 0.1, 0.1)).toEqual({ x: 0.1, y: 0.1, breite: 0.4, hoehe: 0.3 })
    expect(skalieren(r, 5, 5)).toEqual({ x: 0.1, y: 0.1, breite: 0.9, hoehe: 0.9 })
  })

  it('unterschreitet die Mindestgröße nicht', () => {
    expect(skalieren(r, -1, -1)).toEqual({ x: 0.1, y: 0.1, breite: MINDESTGROESSE, hoehe: MINDESTGROESSE })
  })

  it('korrigiert ungültige Rechtecke', () => {
    expect(begrenzen({ x: -0.2, y: 0.95, breite: 1.5, hoehe: 0.01 })).toEqual({
      x: 0,
      y: 0.95,
      breite: 1,
      hoehe: MINDESTGROESSE,
    })
  })
})
