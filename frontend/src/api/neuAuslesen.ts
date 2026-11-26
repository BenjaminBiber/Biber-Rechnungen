import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten } from './client'
import type { NeuAuslesenStatus } from './typen'

export const neuAuslesenKeys = {
  alle: ['neu-auslesen'] as const,
  status: () => ['neu-auslesen', 'status'] as const,
}

/** Takt, in dem der Fortschritt abgefragt wird, solange ein Lauf läuft. */
export const neuAuslesenPollIntervall = 2000

/** GET /api/neu-auslesen – Stand von „Alle Belege erneut auslesen“ (Admin); pollt, solange ein Lauf läuft. */
export function useNeuAuslesenStatus(aktiv = true) {
  return useQuery({
    queryKey: neuAuslesenKeys.status(),
    queryFn: ({ signal }) => daten(api.GET('/api/neu-auslesen', { signal })),
    enabled: aktiv,
    refetchInterval: (query) => (query.state.data?.lauf?.laeuft ? neuAuslesenPollIntervall : false),
  })
}

/** Übernimmt den Stand aus der Antwort, damit die Übersicht sofort erscheint und das Polling anläuft. */
function useStandSetzen() {
  const queryClient = useQueryClient()
  return (stand: NeuAuslesenStatus) => queryClient.setQueryData(neuAuslesenKeys.status(), stand)
}

/** POST /api/neu-auslesen/starten – alle Belege im Hintergrund erneut auslesen. */
export function useNeuAuslesenStarten() {
  const setzen = useStandSetzen()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/neu-auslesen/starten')),
    onSuccess: setzen,
  })
}

/** POST /api/neu-auslesen/abbrechen – laufenden Lauf nach dem aktuellen Beleg beenden. */
export function useNeuAuslesenAbbrechen() {
  const setzen = useStandSetzen()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/neu-auslesen/abbrechen')),
    onSuccess: setzen,
  })
}
