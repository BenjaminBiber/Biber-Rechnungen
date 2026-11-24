import { useQuery } from '@tanstack/react-query'
import { api, apiUrl, daten } from './client'

export const verfahrensdokuKeys = {
  alle: ['verfahrensdoku'] as const,
  info: () => ['verfahrensdoku', 'info'] as const,
}

/** GET /api/verfahrensdoku – Version, Stand und offene Stellen der Verfahrensdokumentation. */
export function useVerfahrensdoku() {
  return useQuery({
    queryKey: verfahrensdokuKeys.info(),
    queryFn: ({ signal }) => daten(api.GET('/api/verfahrensdoku', { signal })),
  })
}

/** GET /api/verfahrensdoku/pdf – Verfahrensdokumentation als PDF. */
export function verfahrensdokuPdfUrl(): string {
  return apiUrl('/api/verfahrensdoku/pdf')
}
