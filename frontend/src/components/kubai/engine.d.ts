// Typen für die Teile von engine.js, die das Frontend direkt benutzt.

export interface KubaiIconDef {
  name: string
  svg: (u: unknown) => string
  target: (hover: boolean, state: Record<string, unknown>) => Record<string, unknown>
  draw: (p: never, E: never) => void
}

export interface KubaiPalette {
  name: string
  colors: string[]
  edges?: string[]
  deepMarks?: boolean
}

export interface KubaiInstance {
  hover: number
  raf: number
}

export const runtime: { palette: KubaiPalette | null }

/** Baut ein Icon in `host` auf; `colorIndex` wählt die Farbe aus der Palette, `uid` macht die ids eindeutig. */
export function mountIcon(icon: KubaiIconDef, colorIndex: number, host: Element, uid?: string | number): KubaiInstance

/** Animiert vom aktuellen Bild zum Ziel (Ruhe oder Hover, je nach `hover`). */
export function go(inst: KubaiInstance): void
