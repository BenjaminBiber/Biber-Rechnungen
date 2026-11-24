import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, daten } from './client'

export const sucheKeys = {
  alle: ['suche'] as const,
  global: (q: string) => ['suche', 'global', q] as const,
}

/** GET /api/suche – Befehlspalette: Belege, Korrespondenten, Artikel, Stammartikel. Leerer Begriff fragt nicht an. */
export function useSuche(q: string) {
  const begriff = q.trim()
  return useQuery({
    queryKey: sucheKeys.global(begriff),
    queryFn: ({ signal }) => daten(api.GET('/api/suche', { params: { query: { q: begriff } }, signal })),
    enabled: begriff.length > 0,
    placeholderData: keepPreviousData,
  })
}
