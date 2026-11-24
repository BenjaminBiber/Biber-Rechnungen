import { useInfiniteQuery } from '@tanstack/react-query'
import { belegeKeys } from '@/api/belege'
import { api, daten } from '@/api/client'
import type { BelegeAbfrage } from '@/api/typen'

/** Belege je nachgeladenem Block in der mobilen Liste. */
export const BLOCKGROESSE = 20

/** GET /api/belege blockweise für endloses Nachladen; `filter` ohne `seite`. */
export function useBelegeEndlos(filter: Omit<BelegeAbfrage, 'seite' | 'seitengroesse'>) {
  return useInfiniteQuery({
    queryKey: [...belegeKeys.listen(), 'endlos', filter] as const,
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) =>
      daten(
        api.GET('/api/belege', {
          params: { query: { ...filter, seite: pageParam, seitengroesse: BLOCKGROESSE } },
          signal,
        }),
      ),
    getNextPageParam: (letzte) => (letzte.seite * letzte.seitengroesse < letzte.gesamt ? letzte.seite + 1 : undefined),
  })
}
