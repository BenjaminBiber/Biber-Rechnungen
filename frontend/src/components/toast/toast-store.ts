/** Dauer, nach der Erfolgs- und Info-Meldungen verschwinden. Fehler bleiben stehen. */
export const TOAST_DAUER_MS = 6000

export type ToastArt = 'erfolg' | 'info' | 'fehler'

export type ToastAktion = { text: string; ausfuehren: () => void }

export type Toast = { id: number; art: ToastArt; text: string; aktion?: ToastAktion }

let toasts: Toast[] = []
let naechsteId = 1
const zuhoerer = new Set<() => void>()

function melden(): void {
  zuhoerer.forEach((zuhoeren) => zuhoeren())
}

export function abonnieren(zuhoeren: () => void): () => void {
  zuhoerer.add(zuhoeren)
  return () => zuhoerer.delete(zuhoeren)
}

export function aktuelleToasts(): Toast[] {
  return toasts
}

export function entfernen(id: number): void {
  toasts = toasts.filter((t) => t.id !== id)
  melden()
}

export function hinzufuegen(art: ToastArt, text: string, aktion?: ToastAktion): number {
  const id = naechsteId++
  toasts = [...toasts, { id, art, text, aktion }]
  melden()
  if (art !== 'fehler') setTimeout(() => entfernen(id), TOAST_DAUER_MS)
  return id
}

/** Nur für Tests: alle Meldungen verwerfen. */
export function alleEntfernen(): void {
  toasts = []
  melden()
}
