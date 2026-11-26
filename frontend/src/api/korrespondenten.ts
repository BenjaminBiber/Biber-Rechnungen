import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { belegeNeuLaden } from './belege'
import { api, apiUrl, daten, erledigt } from './client'
import type { KorrespondentAendern, KorrespondentenAbfrage, KorrespondentenZusammenfuehren } from './typen'

export const korrespondentenKeys = {
  alle: ['korrespondenten'] as const,
  liste: (filter: KorrespondentenAbfrage) => ['korrespondenten', 'liste', filter] as const,
  detail: (id: string, jahr?: number) => ['korrespondenten', 'detail', id, jahr ?? null] as const,
}

/** Korrespondenten erscheinen in Belegen, Dashboard und Suche – alles neu laden. */
function useNeuLaden() {
  const queryClient = useQueryClient()
  return () => belegeNeuLaden(queryClient)
}

/** GET /api/korrespondenten – Liste mit Suche, Typ (Lieferanten/Kunden/Alle) und Jahr für die Kosten. */
export function useKorrespondenten(filter: KorrespondentenAbfrage = {}) {
  return useQuery({
    queryKey: korrespondentenKeys.liste(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/korrespondenten', { params: { query: filter }, signal })),
  })
}

/** GET /api/korrespondenten/{id} – Detail mit Kennzahlen des Jahres. */
export function useKorrespondent(id: string, jahr?: number) {
  return useQuery({
    queryKey: korrespondentenKeys.detail(id, jahr),
    queryFn: ({ signal }) =>
      daten(api.GET('/api/korrespondenten/{id}', { params: { path: { id }, query: { jahr } }, signal })),
  })
}

/** POST /api/korrespondenten – neu anlegen. */
export function useKorrespondentAnlegen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (eingabe: KorrespondentAendern) => daten(api.POST('/api/korrespondenten', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/korrespondenten/{id} – Stammdaten und Matching ändern. */
export function useKorrespondentAendern() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: KorrespondentAendern & { id: string }) =>
      daten(api.PUT('/api/korrespondenten/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/korrespondenten/{id}/ustid-pruefen – USt-IdNr. sofort über VIES prüfen. */
export function useUstIdPruefen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (id: string) =>
      daten(api.POST('/api/korrespondenten/{id}/ustid-pruefen', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** POST /api/korrespondenten/zusammenfuehren – Quellen in den behaltenen Korrespondenten überführen. */
export function useKorrespondentenZusammenfuehren() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (eingabe: KorrespondentenZusammenfuehren) =>
      daten(api.POST('/api/korrespondenten/zusammenfuehren', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/korrespondenten/{id}/logo – Logo-Datei hochladen. */
export function useLogoHochladen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: ({ id, datei }: { id: string; datei: File }) => {
      const formular = new FormData()
      formular.append('datei', datei, datei.name)
      return daten(
        api.PUT('/api/korrespondenten/{id}/logo', {
          params: { path: { id } },
          // Der Vertrag beschreibt `datei` als Binär-String; gesendet wird das Formular unverändert.
          body: formular as unknown as { datei: string },
          bodySerializer: (koerper) => koerper,
        }),
      )
    },
    onSuccess: neuLaden,
  })
}

/** DELETE /api/korrespondenten/{id}/logo – zurück zu Initialen. */
export function useLogoEntfernen() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.DELETE('/api/korrespondenten/{id}/logo', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** POST /api/korrespondenten/{id}/logo/favicon – Favicon der Website als Logo laden. */
export function useLogoFavicon() {
  const neuLaden = useNeuLaden()
  return useMutation({
    mutationFn: (id: string) => daten(api.POST('/api/korrespondenten/{id}/logo/favicon', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}

/** GET /api/korrespondenten/{id}/logo – Logo als Bild. */
export function korrespondentLogoUrl(id: string): string {
  return apiUrl(`/api/korrespondenten/${id}/logo`)
}
