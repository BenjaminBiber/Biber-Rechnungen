/**
 * Welche GET-Anfragen der Service Worker zwischenspeichert. Alles andere – jede Nicht-GET-Anfrage,
 * `/api/auth/*` außer `status`, Originaldateien und `/api/tokens` – geht immer direkt ans Netz.
 */

export const cacheNamen = {
  listen: 'api-listen',
  belege: 'api-belege',
  vorschau: 'api-vorschau',
} as const

/** Alle Caches mit API-Antworten – werden beim Abmelden geleert. */
export const apiCaches: readonly string[] = Object.values(cacheNamen)

const listen = new Set([
  '/api/belege',
  '/api/belege/zaehler',
  '/api/dashboard',
  '/api/korrespondenten',
  '/api/tags',
  '/api/kategorien',
  '/api/auth/status',
])

/** Listen → StaleWhileRevalidate. */
export function istListe(pfad: string): boolean {
  return listen.has(pfad)
}

const einzelbeleg = /^\/api\/belege\/(?!zaehler$|massenaktion$)[^/]+$/

/** `GET /api/belege/{id}` → NetworkFirst. */
export function istEinzelbeleg(pfad: string): boolean {
  return einzelbeleg.test(pfad)
}

const vorschau = /^\/api\/belege\/[^/]+\/vorschau$/

/** Vorschaubilder `GET /api/belege/{id}/vorschau` → CacheFirst. */
export function istVorschau(pfad: string): boolean {
  return vorschau.test(pfad)
}

/** Wird diese Anfrage überhaupt zwischengespeichert? */
export function wirdZwischengespeichert(methode: string, pfad: string): boolean {
  if (methode.toUpperCase() !== 'GET') return false
  return istListe(pfad) || istEinzelbeleg(pfad) || istVorschau(pfad)
}
