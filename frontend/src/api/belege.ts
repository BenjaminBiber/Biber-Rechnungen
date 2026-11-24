import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api, apiUrl, daten, erledigt } from './client'
import type {
  Aufteilen,
  BelegAendern,
  BelegeAbfrage,
  Bestaetigen,
  DubletteSetzen,
  GespeicherteAnsichtAnlegen,
  LogoAusschnitt,
  Massenaktion,
  PositionAendern,
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

export function belegeNeuLaden(queryClient: QueryClient) {
  return Promise.all(belegAbhaengig.map((queryKey) => queryClient.invalidateQueries({ queryKey })))
}

function useBelegeNeuLaden() {
  const queryClient = useQueryClient()
  return () => belegeNeuLaden(queryClient)
}

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

/** GET /api/belege/{id} – Beleg-Detail. */
export function useBeleg(id: string) {
  return useQuery({
    queryKey: belegeKeys.detail(id),
    queryFn: ({ signal }) => daten(api.GET('/api/belege/{id}', { params: { path: { id } }, signal })),
  })
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

/** GET /api/belege/{id}/vorschau – PNG-Vorschau einer Seite (Standard: erste). */
export function belegVorschauUrl(belegId: string, seite?: number): string {
  return apiUrl(`/api/belege/${belegId}/vorschau`, { seite })
}

/** GET /api/belege/{id}/visualisierung – HTML-Darstellung einer E-Rechnung. */
export function belegVisualisierungUrl(belegId: string): string {
  return apiUrl(`/api/belege/${belegId}/visualisierung`)
}
