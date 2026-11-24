import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten, erledigt } from './client'
import type { BenachrichtigungenAbfrage } from './typen'

export const benachrichtigungenKeys = {
  alle: ['benachrichtigungen'] as const,
  liste: (filter: BenachrichtigungenAbfrage) => ['benachrichtigungen', 'liste', filter] as const,
  zaehler: () => ['benachrichtigungen', 'zaehler'] as const,
}

const zaehlerIntervall = 60_000

/** GET /api/benachrichtigungen – Liste, optional nur ungelesene. */
export function useBenachrichtigungen(filter: BenachrichtigungenAbfrage = {}) {
  return useQuery({
    queryKey: benachrichtigungenKeys.liste(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/benachrichtigungen', { params: { query: filter }, signal })),
  })
}

/** GET /api/benachrichtigungen/zaehler – Zahl an der Glocke, pollt alle 60 s. */
export function useBenachrichtigungZaehler() {
  return useQuery({
    queryKey: benachrichtigungenKeys.zaehler(),
    queryFn: ({ signal }) => daten(api.GET('/api/benachrichtigungen/zaehler', { signal })),
    refetchInterval: zaehlerIntervall,
  })
}

/** POST /api/benachrichtigungen/{id}/gelesen. */
export function useBenachrichtigungGelesen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      erledigt(api.POST('/api/benachrichtigungen/{id}/gelesen', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: benachrichtigungenKeys.alle }),
  })
}

/** POST /api/benachrichtigungen/alle-gelesen. */
export function useAlleBenachrichtigungenGelesen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => erledigt(api.POST('/api/benachrichtigungen/alle-gelesen')),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: benachrichtigungenKeys.alle }),
  })
}
