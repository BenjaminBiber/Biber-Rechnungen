import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten, erledigt } from './client'
import type { ApiTokenAnlegen } from './typen'

export const tokensKeys = {
  alle: ['tokens'] as const,
  liste: () => ['tokens', 'liste'] as const,
}

/** GET /api/tokens – API-Tokens (MCP, Upload). */
export function useTokens() {
  return useQuery({
    queryKey: tokensKeys.liste(),
    queryFn: ({ signal }) => daten(api.GET('/api/tokens', { signal })),
  })
}

/** POST /api/tokens – liefert den Klartext genau einmal. */
export function useTokenAnlegen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (eingabe: ApiTokenAnlegen) => daten(api.POST('/api/tokens', { body: eingabe })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tokensKeys.alle }),
  })
}

/** POST /api/tokens/{id}/widerrufen. */
export function useTokenWiderrufen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.POST('/api/tokens/{id}/widerrufen', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tokensKeys.alle }),
  })
}
