import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten, erledigt } from './client'

export const integritaetKeys = {
  alle: ['integritaet'] as const,
  laeufe: () => ['integritaet', 'laeufe'] as const,
}

/** GET /api/integritaet/laeufe – bisherige Prüfläufe der Ablage. */
export function useIntegritaetsLaeufe() {
  return useQuery({
    queryKey: integritaetKeys.laeufe(),
    queryFn: ({ signal }) => daten(api.GET('/api/integritaet/laeufe', { signal })),
  })
}

/** POST /api/integritaet/starten – Prüflauf sofort starten. */
export function useIntegritaetStarten() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => erledigt(api.POST('/api/integritaet/starten')),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: integritaetKeys.alle }),
  })
}
