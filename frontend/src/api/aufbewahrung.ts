import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { belegeNeuLaden } from './belege'
import { api, daten } from './client'
import type { Loeschen } from './typen'

export const aufbewahrungKeys = {
  alle: ['aufbewahrung'] as const,
  loeschbar: () => ['aufbewahrung', 'loeschbar'] as const,
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
