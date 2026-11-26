import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { api, apiUrl, daten, erledigt } from './client'
import type {
  Aufteilen,
  BelegAendern,
  BelegeAbfrage,
  BelegStatus,
  Bestaetigen,
  BezugSetzen,
  DubletteSetzen,
  GespeicherteAnsichtAnlegen,
  LogoAusschnitt,
  Massenaktion,
  PositionAendern,
  SkontoSetzen,
} from './typen'

export const belegeKeys = {
  alle: ['belege'] as const,
  listen: () => ['belege', 'liste'] as const,
  liste: (filter: BelegeAbfrage) => ['belege', 'liste', filter] as const,
  zaehler: () => ['belege', 'zaehler'] as const,
  detail: (id: string) => ['belege', 'detail', id] as const,
  nachbarn: (id: string, filter: BelegeAbfrage) => ['belege', 'nachbarn', id, filter] as const,
  aehnliche: (id: string, anzahl?: number) => ['belege', 'aehnliche', id, anzahl ?? null] as const,
  historie: (id: string) => ['belege', 'historie', id] as const,
}

export const ansichtenKeys = {
  alle: ['ansichten'] as const,
  liste: () => ['ansichten', 'liste'] as const,
}

/**
 * Alles, was aus Belegen abgeleitet wird (Zähler, Dashboard, Summen je Korrespondent/Tag/Kategorie,
 * Artikelpreise, Suche, Export-Vorschau). Nach jeder Beleg-Änderung neu laden.
 */
const belegAbhaengig = [
  ['belege'],
  ['dashboard'],
  ['korrespondenten'],
  ['tags'],
  ['kategorien'],
  ['artikel'],
  ['stammartikel'],
  ['suche'],
  ['export'],
  ['import'],
] as const

/**
 * Lädt alles Beleg-Abhängige neu. `ohneBeleg`: Abfragen dieses Belegs nur als veraltet markieren, nicht neu laden –
 * etwa nach dem Löschen, solange seine Detailansicht noch offen ist (sie bekäme sonst 404).
 */
export async function belegeNeuLaden(queryClient: QueryClient, ohneBeleg?: string) {
  if (ohneBeleg === undefined)
    return Promise.all(belegAbhaengig.map((queryKey) => queryClient.invalidateQueries({ queryKey })))
  const vomBeleg = (q: { queryKey: readonly unknown[] }) => q.queryKey.includes(ohneBeleg)
  await queryClient.invalidateQueries({ predicate: vomBeleg, refetchType: 'none' })
  return Promise.all(
    belegAbhaengig.map((queryKey) => queryClient.invalidateQueries({ queryKey, predicate: (q) => !vomBeleg(q) })),
  )
}

function useBelegeNeuLaden() {
  const queryClient = useQueryClient()
  return () => belegeNeuLaden(queryClient)
}

/** Takt, in dem gepollt wird, solange der Server noch an etwas arbeitet. */
export const verarbeitungsPollIntervall = 3000

/**
 * Lädt alles Belegabhängige neu, sobald die Zahl laufender Vorgänge sinkt – also wenn die Pipeline im
 * Hintergrund etwas fertig hat. `laufend` ist `undefined`, solange noch keine Daten da sind.
 */
export function useNachAbschlussNeuLaden(laufend: number | undefined) {
  const queryClient = useQueryClient()
  const vorher = useRef(laufend)
  useEffect(() => {
    const alt = vorher.current
    vorher.current = laufend
    if (alt !== undefined && laufend !== undefined && laufend < alt) void belegeNeuLaden(queryClient)
  }, [laufend, queryClient])
}

/** Status, in denen die Pipeline noch am Beleg arbeitet – solange wird das Detail gepollt. */
const laufendeBelegStatus: readonly BelegStatus[] = ['Importiert', 'InVerarbeitung']

/** GET /api/belege – gefilterte, sortierte und geblätterte Belegliste. */
export function useBelege(filter: BelegeAbfrage = {}) {
  return useQuery({
    queryKey: belegeKeys.liste(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/belege', { params: { query: filter }, signal })),
    placeholderData: keepPreviousData,
  })
}

/** GET /api/belege/zaehler – Zähler der fünf Ansichten (Alle, Inbox, Zu prüfen, Offen, Überfällig). */
export function useBelegZaehler() {
  return useQuery({
    queryKey: belegeKeys.zaehler(),
    queryFn: ({ signal }) => daten(api.GET('/api/belege/zaehler', { signal })),
  })
}

/**
 * GET /api/belege/{id} – Beleg-Detail. Pollt alle 3 s, solange die Pipeline noch daran arbeitet; ist sie
 * fertig, wird alles Belegabhängige neu geladen.
 */
export function useBeleg(id: string) {
  const query = useQuery({
    queryKey: belegeKeys.detail(id),
    queryFn: ({ signal }) => daten(api.GET('/api/belege/{id}', { params: { path: { id } }, signal })),
    refetchInterval: (query) =>
      query.state.data && laufendeBelegStatus.includes(query.state.data.status) ? verarbeitungsPollIntervall : false,
  })
  useNachAbschlussNeuLaden(query.data ? Number(laufendeBelegStatus.includes(query.data.status)) : undefined)
  return query
}

/** GET /api/belege/{id}/nachbarn – vorheriger/nächster Beleg in der aktuellen Liste. */
export function useBelegNachbarn(id: string, filter: BelegeAbfrage = {}) {
  return useQuery({
    queryKey: belegeKeys.nachbarn(id, filter),
    queryFn: ({ signal }) =>
      daten(api.GET('/api/belege/{id}/nachbarn', { params: { path: { id }, query: filter }, signal })),
  })
}

/** GET /api/belege/{id}/aehnliche – ähnliche Belege (Dubletten-Tab). */
export function useAehnlicheBelege(id: string, anzahl?: number) {
  return useQuery({
    queryKey: belegeKeys.aehnliche(id, anzahl),
    queryFn: ({ signal }) =>
      daten(api.GET('/api/belege/{id}/aehnliche', { params: { path: { id }, query: { anzahl } }, signal })),
  })
}

/** GET /api/belege/{id}/historie – Audit-Einträge. */
export function useBelegHistorie(id: string) {
  return useQuery({
    queryKey: belegeKeys.historie(id),
    queryFn: ({ signal }) => daten(api.GET('/api/belege/{id}/historie', { params: { path: { id } }, signal })),
  })
}

/** PUT /api/belege/{id} – Kopf- und Steuerdaten ändern. */
export function useBelegAendern() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: BelegAendern & { id: string }) =>
      daten(api.PUT('/api/belege/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/belege/{id}/positionen – alle Positionen ersetzen. */
export function usePositionenErsetzen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, positionen }: { id: string; positionen: PositionAendern[] }) =>
      daten(api.PUT('/api/belege/{id}/positionen', { params: { path: { id } }, body: positionen })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/tags – Tag zuordnen. */
export function useTagHinzufuegen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, tagId }: { id: string; tagId: string }) =>
      erledigt(api.POST('/api/belege/{id}/tags', { params: { path: { id } }, body: { tagId } })),
    onSuccess: neuLaden,
  })
}

/** DELETE /api/belege/{id}/tags/{tagId} – Tag entfernen. */
export function useTagEntfernen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, tagId }: { id: string; tagId: string }) =>
      erledigt(api.DELETE('/api/belege/{id}/tags/{tagId}', { params: { path: { id, tagId } } })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/bestaetigen – „zu prüfen“ entfernen (optional mit Begründung). */
export function useBelegBestaetigen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: Bestaetigen & { id: string }) =>
      erledigt(api.POST('/api/belege/{id}/bestaetigen', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/erneut-auslesen – Beleg erneut durch die Pipeline schicken. */
export function useErneutAuslesen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.POST('/api/belege/{id}/erneut-auslesen', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/dublette – als Dublette markieren (`duplikatVonId`) oder Markierung aufheben (null). */
export function useDubletteSetzen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: DubletteSetzen & { id: string }) =>
      erledigt(api.POST('/api/belege/{id}/dublette', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/bezug – Gutschrift mit einer Rechnung verknüpfen oder die Verknüpfung lösen. */
export function useBezugSetzen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: BezugSetzen & { id: string }) =>
      erledigt(api.POST('/api/belege/{id}/bezug', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/belege/{id}/skonto – Skonto setzen oder entfernen. */
export function useSkontoSetzen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: SkontoSetzen & { id: string }) =>
      daten(api.PUT('/api/belege/{id}/skonto', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/massenaktion – Tags/Kategorie setzen oder bestätigen für mehrere Belege. */
export function useMassenaktion() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: (eingabe: Massenaktion) => daten(api.POST('/api/belege/massenaktion', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/aufteilen – Beleg nach Seiten in neue Import-Vorgänge aufteilen. */
export function useAufteilen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: Aufteilen & { id: string }) =>
      daten(api.POST('/api/belege/{id}/aufteilen', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/belege/{id}/logo-uebernehmen – Ausschnitt als Logo des Korrespondenten übernehmen. */
export function useLogoUebernehmen() {
  const neuLaden = useBelegeNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: LogoAusschnitt & { id: string }) =>
      daten(api.POST('/api/belege/{id}/logo-uebernehmen', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** GET /api/ansichten – gespeicherte Ansichten der Belegliste. */
export function useAnsichten() {
  return useQuery({
    queryKey: ansichtenKeys.liste(),
    queryFn: ({ signal }) => daten(api.GET('/api/ansichten', { signal })),
  })
}

/** POST /api/ansichten – aktuelle Filter als Ansicht speichern. */
export function useAnsichtSpeichern() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (eingabe: GespeicherteAnsichtAnlegen) => daten(api.POST('/api/ansichten', { body: eingabe })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ansichtenKeys.alle }),
  })
}

/** DELETE /api/ansichten/{id}. */
export function useAnsichtLoeschen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.DELETE('/api/ansichten/{id}', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ansichtenKeys.alle }),
  })
}

/** GET /api/belege/{id}/dateien/{dateiId} – Original, eingebettetes XML usw.; `inline` zeigt statt herunterzuladen. */
export function belegDateiUrl(belegId: string, dateiId: string, inline?: boolean): string {
  return apiUrl(`/api/belege/${belegId}/dateien/${dateiId}`, { inline })
}

/**
 * GET /api/belege/{id}/vorschau – WebP-Vorschau einer Seite (Standard: erste) in `breite` Pixeln
 * (Server: 200–1600, Standard 600; jede Breite wird serverseitig zwischengespeichert).
 */
export function belegVorschauUrl(belegId: string, seite?: number, breite?: number): string {
  return apiUrl(`/api/belege/${belegId}/vorschau`, { seite, breite })
}

/** GET /api/belege/{id}/visualisierung – HTML-Darstellung einer E-Rechnung. */
export function belegVisualisierungUrl(belegId: string): string {
  return apiUrl(`/api/belege/${belegId}/visualisierung`)
}
