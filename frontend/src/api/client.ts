import createClient, { type Middleware } from 'openapi-fetch'
import type { paths } from './schema'

/** Fehler einer API-Antwort, abgebildet aus `ProblemDetails` (RFC 9457). */
export class ApiFehler extends Error {
  readonly status: number
  readonly titel: string
  readonly fehler?: Record<string, string[]>
  /** Vollständiger ProblemDetails-Körper, z. B. für Erweiterungen wie `korrespondentId` oder `pfade`. */
  readonly problem: Record<string, unknown>

  constructor(status: number, titel: string, fehler?: Record<string, string[]>, problem: Record<string, unknown> = {}) {
    super(titel)
    this.name = 'ApiFehler'
    this.status = status
    this.titel = titel
    this.fehler = fehler
    this.problem = problem
  }
}

export function istApiFehler(wert: unknown): wert is ApiFehler {
  return wert instanceof ApiFehler
}

type ProblemDetails = { title?: string | null; detail?: string | null; errors?: Record<string, string[]> | null }

/** Baut aus Status und Antwortkörper (ProblemDetails oder Text) einen `ApiFehler`. */
export function zuApiFehler(status: number, koerper: unknown, statusText = ''): ApiFehler {
  const problem: ProblemDetails = typeof koerper === 'object' && koerper !== null ? (koerper as ProblemDetails) : {}
  const titel =
    problem.title ||
    problem.detail ||
    (typeof koerper === 'string' && koerper.trim() ? koerper.trim() : '') ||
    statusText ||
    `Fehler ${status}`
  return new ApiFehler(status, titel, problem.errors ?? undefined, problem as Record<string, unknown>)
}

/**
 * Weiterleitung bei 401. Als Objekt exportiert, damit Tests die Navigation ersetzen können
 * (`vi.spyOn(anmeldeWeiterleitung, 'ausfuehren')`).
 */
export const anmeldeWeiterleitung = {
  ziel: '/anmelden',
  ausfuehren(): void {
    window.location.assign(this.ziel)
  },
}

/** Leitet nach `/anmelden` weiter – außer man ist schon dort. */
export function beiNichtAngemeldet(): void {
  if (typeof window === 'undefined') return
  if (window.location.pathname === anmeldeWeiterleitung.ziel) return
  anmeldeWeiterleitung.ausfuehren()
}

const weiterleitungBei401: Middleware = {
  onResponse({ response }) {
    if (response.status === 401) beiNichtAngemeldet()
    return undefined
  },
}

/**
 * Typisierter Client für alle Endpunkte aus `docs/api/openapi.json`. Die Pfade im Vertrag
 * beginnen bereits mit `/api`, die Basis ist daher nur der Ursprung (absolut, weil Node/jsdom
 * keine relativen `Request`-URLs kennen). `fetch` wird erst beim Aufruf gelesen, damit MSW greift.
 */
export const api = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? 'http://localhost',
  credentials: 'include',
  fetch: (anfrage) => globalThis.fetch(anfrage),
})
api.use(weiterleitungBei401)

/** Relative URL für Datei-Endpunkte (`<a href>`, `<img src>`); leere Parameter entfallen. */
export function apiUrl(pfad: string, abfrage: Record<string, string | number | boolean | undefined> = {}): string {
  const parameter = new URLSearchParams()
  for (const [name, wert] of Object.entries(abfrage)) if (wert !== undefined) parameter.set(name, String(wert))
  const text = parameter.toString()
  return text ? `${pfad}?${text}` : pfad
}

type Antwort<D> = { data?: D; error?: unknown; response: Response }

/** Wartet auf eine Client-Antwort und liefert die Daten oder wirft einen `ApiFehler`. */
export async function daten<D>(anfrage: Promise<Antwort<D>>): Promise<D> {
  const { data, error, response } = await anfrage
  if (!response.ok) throw zuApiFehler(response.status, error, response.statusText)
  return data as D
}

/** Wie `daten`, für Endpunkte ohne Antwortkörper (204/202). */
export async function erledigt(anfrage: Promise<Antwort<unknown>>): Promise<void> {
  await daten(anfrage)
}
