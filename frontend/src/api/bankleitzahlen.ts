import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { belegeNeuLaden } from './belege'
import { api, daten } from './client'
import type { BankleitzahlenStand } from './typen'

export const bankleitzahlenKeys = {
  stand: () => ['bankleitzahlen'] as const,
}

/**
 * Übernimmt den neuen Stand und lädt Belege und Korrespondenten neu – deren IBANs zeigen den Banknamen aus den
 * Bankleitzahlen.
 */
function useStandSetzen() {
  const queryClient = useQueryClient()
  return (stand: BankleitzahlenStand) => {
    queryClient.setQueryData(bankleitzahlenKeys.stand(), stand)
    void belegeNeuLaden(queryClient)
  }
}

/** GET /api/bankleitzahlen – Stand der Bankleitzahlen der Bundesbank (Admin). */
export function useBankleitzahlen() {
  return useQuery({
    queryKey: bankleitzahlenKeys.stand(),
    queryFn: ({ signal }) => daten(api.GET('/api/bankleitzahlen', { signal })),
  })
}

/** POST /api/bankleitzahlen/abrufen – Datei sofort von der Bundesbank holen; ein Fehler steht danach im Stand. */
export function useBankleitzahlenAbrufen() {
  const setzen = useStandSetzen()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/bankleitzahlen/abrufen')),
    onSuccess: setzen,
  })
}

/** PUT /api/bankleitzahlen – Bankleitzahlendatei (TXT) der Bundesbank hochladen. */
export function useBankleitzahlenHochladen() {
  const setzen = useStandSetzen()
  return useMutation({
    mutationFn: (datei: File) => {
      const formular = new FormData()
      formular.append('datei', datei, datei.name)
      return daten(
        api.PUT('/api/bankleitzahlen', {
          // Der Vertrag beschreibt `datei` als Binär-String; gesendet wird das Formular unverändert.
          body: formular as unknown as { datei: string },
          bodySerializer: (koerper) => koerper,
        }),
      )
    },
    onSuccess: setzen,
  })
}
