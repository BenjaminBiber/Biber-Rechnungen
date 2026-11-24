import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten, erledigt } from './client'
import type { ArtikelAbfrage, StammartikelAendern } from './typen'

export const artikelKeys = {
  alle: ['artikel'] as const,
  liste: (filter: ArtikelAbfrage) => ['artikel', 'liste', filter] as const,
  detail: (id: string, jahr?: number) => ['artikel', 'detail', id, jahr ?? null] as const,
}

export const stammartikelKeys = {
  alle: ['stammartikel'] as const,
  liste: () => ['stammartikel', 'liste'] as const,
  detail: (id: string, jahr?: number) => ['stammartikel', 'detail', id, jahr ?? null] as const,
}

/** Stammartikel-Zuordnung ändert Artikel- und Stammartikel-Ansichten. */
function useArtikelNeuLaden() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: artikelKeys.alle }),
      queryClient.invalidateQueries({ queryKey: stammartikelKeys.alle }),
      queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      queryClient.invalidateQueries({ queryKey: ['suche'] }),
    ])
}

/** GET /api/artikel – Artikelliste mit Filtern und Blättern. */
export function useArtikel(filter: ArtikelAbfrage = {}) {
  return useQuery({
    queryKey: artikelKeys.liste(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/artikel', { params: { query: filter }, signal })),
    placeholderData: keepPreviousData,
  })
}

/** GET /api/artikel/{id} – Preisverlauf und Käufe eines Artikels. */
export function useArtikelDetail(id: string, jahr?: number) {
  return useQuery({
    queryKey: artikelKeys.detail(id, jahr),
    queryFn: ({ signal }) => daten(api.GET('/api/artikel/{id}', { params: { path: { id }, query: { jahr } }, signal })),
  })
}

/** GET /api/stammartikel. */
export function useStammartikel() {
  return useQuery({
    queryKey: stammartikelKeys.liste(),
    queryFn: ({ signal }) => daten(api.GET('/api/stammartikel', { signal })),
  })
}

/** GET /api/stammartikel/{id} – Preisvergleich über alle zugeordneten Artikel. */
export function useStammartikelDetail(id: string, jahr?: number) {
  return useQuery({
    queryKey: stammartikelKeys.detail(id, jahr),
    queryFn: ({ signal }) =>
      daten(api.GET('/api/stammartikel/{id}', { params: { path: { id }, query: { jahr } }, signal })),
  })
}

/** POST /api/stammartikel – anlegen (z. B. „Zu Stammartikel zusammenführen“). */
export function useStammartikelAnlegen() {
  const neuLaden = useArtikelNeuLaden()
  return useMutation({
    mutationFn: (eingabe: StammartikelAendern) => daten(api.POST('/api/stammartikel', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/stammartikel/{id}. */
export function useStammartikelAendern() {
  const neuLaden = useArtikelNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: StammartikelAendern & { id: string }) =>
      daten(api.PUT('/api/stammartikel/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** DELETE /api/stammartikel/{id}/artikel/{artikelId} – Artikel aus dem Stammartikel lösen. */
export function useStammartikelArtikelEntfernen() {
  const neuLaden = useArtikelNeuLaden()
  return useMutation({
    mutationFn: ({ id, artikelId }: { id: string; artikelId: string }) =>
      erledigt(api.DELETE('/api/stammartikel/{id}/artikel/{artikelId}', { params: { path: { id, artikelId } } })),
    onSuccess: neuLaden,
  })
}
