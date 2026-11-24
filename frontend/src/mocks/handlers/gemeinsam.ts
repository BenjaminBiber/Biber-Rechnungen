import { http, HttpResponse, type HttpHandler } from 'msw'
import type { Rolle } from '@/api/typen'
import { db } from '../db'

/** Autorisierungsrichtlinie wie `x-richtlinie` in `docs/api/openapi.json`; `Anonym` = ohne Anmeldung. */
export type Richtlinie = 'Anonym' | 'Lesen' | 'Bearbeiten' | 'Admin'

export type Methode = 'get' | 'post' | 'put' | 'delete'

export type Anfrage = { request: Request; params: Record<string, string> }

export type Antwort = (anfrage: Anfrage) => Response | Promise<Response>

/** Ein Endpunkt des Vertrags; `pfad` in OpenAPI-Schreibweise (`/api/belege/{id}`). */
export type Route = { methode: Methode; pfad: string; richtlinie: Richtlinie; antwort: Antwort }

export function route(methode: Methode, pfad: string, richtlinie: Richtlinie, antwort: Antwort): Route {
  return { methode, pfad, richtlinie, antwort }
}

const rechte: Record<Rolle, Richtlinie[]> = {
  Admin: ['Lesen', 'Bearbeiten', 'Admin'],
  Benutzer: ['Lesen', 'Bearbeiten'],
  NurLesen: ['Lesen'],
}

/** ProblemDetails-Antwort (RFC 9457); `erweiterungen` sind zusätzliche Felder wie `korrespondentId`. */
export function problem(
  status: number,
  title: string,
  errors?: Record<string, string[]>,
  erweiterungen: Record<string, unknown> = {},
): Response {
  return HttpResponse.json(
    { type: `https://httpstatuses.io/${status}`, title, status, ...(errors ? { errors } : {}), ...erweiterungen },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

export const nichtGefunden = (was = 'Eintrag') => problem(404, `${was} nicht gefunden`)

export function ungueltig(feld: string, meldung: string): Response {
  return problem(400, 'Die Eingabe ist ungültig.', { [feld]: [meldung] })
}

export const keinInhalt = () => new HttpResponse(null, { status: 204 })
export const angenommen = () => new HttpResponse(null, { status: 202 })

/** Wandelt `/api/belege/{id}` in die MSW-Schreibweise `/api/belege/:id`. */
export function mswPfad(pfad: string): string {
  return pfad.replace(/\{(\w+)\}/g, ':$1')
}

function erzwungenerFehler(methode: Methode, pfad: string): Response | undefined {
  const treffer = db.fehler.find(
    (f) => mswPfad(f.pfad) === mswPfad(pfad) && (!f.methode || f.methode.toLowerCase() === methode),
  )
  return treffer ? problem(treffer.status, `Erzwungener Fehler ${treffer.status} (Szenario)`) : undefined
}

/**
 * Baut aus den Routen MSW-Handler. Reihenfolge der Prüfungen wie im Backend:
 * erzwungener Fehler (Szenario) → angemeldet? (401) → Recht der Rolle? (403) → Antwort.
 */
export function zuHandlern(routen: Route[]): HttpHandler[] {
  return routen.map(({ methode, pfad, richtlinie, antwort }) =>
    http[methode](mswPfad(pfad), async ({ request, params }) => {
      const fehler = erzwungenerFehler(methode, pfad)
      if (fehler) return fehler
      if (richtlinie !== 'Anonym') {
        if (!db.angemeldet) return problem(401, 'Nicht angemeldet')
        const rolle = db.benutzer.rolle ?? 'NurLesen'
        if (!rechte[rolle].includes(richtlinie)) return problem(403, 'Keine Berechtigung')
      }
      return antwort({ request, params: params as Record<string, string> })
    }),
  )
}

/** Query-Parameter einer Anfrage. */
export function abfrage(request: Request): URLSearchParams {
  return new URL(request.url).searchParams
}

export function zahl(parameter: URLSearchParams, name: string): number | undefined {
  const wert = parameter.get(name)
  if (wert === null || wert === '') return undefined
  const ergebnis = Number(wert)
  return Number.isNaN(ergebnis) ? undefined : ergebnis
}

export function wahr(parameter: URLSearchParams, name: string): boolean | undefined {
  const wert = parameter.get(name)
  return wert === null ? undefined : wert === 'true'
}

export async function jsonKoerper<T>(request: Request): Promise<T> {
  return (await request.json()) as T
}

/** Blättern (Seite 1-basiert) mit Seitenhülle wie `SeiteDto<T>`. */
export function seite<T>(eintraege: T[], nummer = 1, groesse = 25) {
  const start = (Math.max(1, nummer) - 1) * groesse
  return {
    eintraege: eintraege.slice(start, start + groesse),
    gesamt: eintraege.length,
    seite: nummer,
    seitengroesse: groesse,
  }
}

/** Binärantwort (Datei-Endpunkte). */
export function datei(
  inhalt: BodyInit,
  mimeType: string,
  dateiname?: string,
  { inline = true }: { inline?: boolean } = {},
): Response {
  const headers: Record<string, string> = { 'Content-Type': mimeType }
  if (dateiname) headers['Content-Disposition'] = `${inline ? 'inline' : 'attachment'}; filename="${dateiname}"`
  return new HttpResponse(inhalt, { status: 200, headers })
}

export function weiterleitung(ziel: string): Response {
  return new HttpResponse(null, { status: 302, headers: { Location: ziel } })
}

export function runden(betrag: number): number {
  return Math.round(betrag * 100) / 100
}
