import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { belegeNeuLaden } from './belege'
import { api, daten, erledigt } from './client'
import type { KategorieAendern, MatchingAnwenden, MatchingTest, TagAendern } from './typen'

export const tagsKeys = {
  alle: ['tags'] as const,
  liste: () => ['tags', 'liste'] as const,
}

export const kategorienKeys = {
  alle: ['kategorien'] as const,
  liste: (jahr?: number) => ['kategorien', 'liste', jahr ?? null] as const,
}

export const matchingKeys = {
  alle: ['matching'] as const,
  autoStatus: () => ['matching', 'auto-status'] as const,
}

/** Tags und Kategorien hängen an Belegen – nach Änderungen alles Belegabhängige neu laden. */
function useNeuLaden() {
  const queryClient = useQueryClient()
  return () => belegeNeuLaden(queryClient)
}

/** GET /api/tags. */
export function useTags() {
  return useQuery({
    queryKey: tagsKeys.liste(),
    queryFn: ({ signal }) => daten(api.GET('/api/tags', { signal })),
  })
}

/** POST /api/tags. */
export function useTagAnlegen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (eingabe: TagAendern) => daten(api.POST('/api/tags', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/tags/{id}. */
export function useTagAendern() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: TagAendern & { id: string }) =>
      daten(api.PUT('/api/tags/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** DELETE /api/tags/{id}. */
export function useTagLoeschen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.DELETE('/api/tags/{id}', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** GET /api/kategorien – mit Anzahl und Summe je Kategorie im Jahr. */
export function useKategorien(jahr?: number) {
  return useQuery({
    queryKey: kategorienKeys.liste(jahr),
    queryFn: ({ signal }) => daten(api.GET('/api/kategorien', { params: { query: { jahr } }, signal })),
  })
}

/** POST /api/kategorien. */
export function useKategorieAnlegen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (eingabe: KategorieAendern) => daten(api.POST('/api/kategorien', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/kategorien/{id}. */
export function useKategorieAendern() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: KategorieAendern & { id: string }) =>
      daten(api.PUT('/api/kategorien/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** DELETE /api/kategorien/{id}. */
export function useKategorieLoeschen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.DELETE('/api/kategorien/{id}', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** POST /api/matching/testen – Muster gegen alle Belege testen (ändert nichts). */
export function useMatchingTesten() {
  return useMutation({
    mutationFn: (eingabe: MatchingTest) => daten(api.POST('/api/matching/testen', { body: eingabe })),
  })
}

/** POST /api/matching/anwenden – Matching eines Ziels auf alle Belege anwenden. */
export function useMatchingAnwenden() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (eingabe: MatchingAnwenden) => daten(api.POST('/api/matching/anwenden', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** GET /api/matching/auto-status – Trainingsstand des Auto-Matchings je Ziel. */
export function useAutoMatchingStatus() {
  return useQuery({
    queryKey: matchingKeys.autoStatus(),
    queryFn: ({ signal }) => daten(api.GET('/api/matching/auto-status', { signal })),
  })
}
