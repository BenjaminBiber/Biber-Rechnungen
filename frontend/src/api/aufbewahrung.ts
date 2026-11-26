import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { belegeNeuLaden } from './belege'
import { api, daten, erledigt } from './client'
import type { BelegLoeschen, Loeschen } from './typen'

export const aufbewahrungKeys = {
  alle: ['aufbewahrung'] as const,
  loeschbar: () => ['aufbewahrung', 'loeschbar'] as const,
  geloeschte: () => ['aufbewahrung', 'geloeschte'] as const,
}

/** GET /api/aufbewahrung/loeschbar – Jahrgänge, deren Aufbewahrungsfrist abgelaufen ist. */
export function useLoeschbar() {
  return useQuery({
    queryKey: aufbewahrungKeys.loeschbar(),
    queryFn: ({ signal }) => daten(api.GET('/api/aufbewahrung/loeschbar', { signal })),
  })
}

/** POST /api/aufbewahrung/loeschen – Jahrgang endgültig löschen (mit Bestätigungstext). */
export function useAufbewahrungLoeschen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (eingabe: Loeschen) => daten(api.POST('/api/aufbewahrung/loeschen', { body: eingabe })),
    onSuccess: () =>
      Promise.all([queryClient.invalidateQueries({ queryKey: aufbewahrungKeys.alle }), belegeNeuLaden(queryClient)]),
  })
}

/** GET /api/belege/geloeschte – als falsch zugeordnet gelöschte Belege (nur Admin). */
export function useGeloeschteBelege() {
  return useQuery({
    queryKey: aufbewahrungKeys.geloeschte(),
    queryFn: ({ signal }) => daten(api.GET('/api/belege/geloeschte', { signal })),
  })
}

/**
 * POST /api/belege/{id}/loeschen – falsch zugeordneten Beleg trotz Löschsperre löschen (nur Admin). Der Beleg wird
 * nur ausgeblendet und kann unter Einstellungen → Gelöschte Belege wiederhergestellt werden.
 */
export function useBelegLoeschen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: BelegLoeschen & { id: string }) =>
      erledigt(api.POST('/api/belege/{id}/loeschen', { params: { path: { id } }, body: eingabe })),
    onSuccess: (_, { id }) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: aufbewahrungKeys.alle }),
        belegeNeuLaden(queryClient, id),
      ]),
  })
}

/** POST /api/belege/{id}/wiederherstellen – gelöschten Beleg wieder in die Belege holen (nur Admin). */
export function useBelegWiederherstellen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.POST('/api/belege/{id}/wiederherstellen', { params: { path: { id } } })),
    onSuccess: () =>
      Promise.all([queryClient.invalidateQueries({ queryKey: aufbewahrungKeys.alle }), belegeNeuLaden(queryClient)]),
  })
}
