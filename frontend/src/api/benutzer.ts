import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, daten, erledigt } from './client'
import type { BenutzerAendern, BenutzerAnlegen, BenutzerFreigeben } from './typen'

export const benutzerKeys = {
  alle: ['benutzer'] as const,
  liste: () => ['benutzer', 'liste'] as const,
}

function useBenutzerNeuLaden() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: benutzerKeys.alle })
}

/** GET /api/benutzer – alle Benutzer (Admin). */
export function useBenutzer() {
  return useQuery({
    queryKey: benutzerKeys.liste(),
    queryFn: ({ signal }) => daten(api.GET('/api/benutzer', { signal })),
  })
}

/** POST /api/benutzer – Benutzer anlegen. */
export function useBenutzerAnlegen() {
  const neuLaden = useBenutzerNeuLaden()
  return useMutation({
    mutationFn: (eingabe: BenutzerAnlegen) => daten(api.POST('/api/benutzer', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** PUT /api/benutzer/{id} – Rolle und Aktiv-Status ändern. */
export function useBenutzerAendern() {
  const neuLaden = useBenutzerNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: BenutzerAendern & { id: string }) =>
      daten(api.PUT('/api/benutzer/{id}', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/benutzer/{id}/passwort – Passwort eines Benutzers zurücksetzen. */
export function usePasswortZuruecksetzen() {
  return useMutation({
    mutationFn: ({ id, ...eingabe }: { id: string; neuesPasswort: string }) =>
      erledigt(api.POST('/api/benutzer/{id}/passwort', { params: { path: { id } }, body: eingabe })),
  })
}

/** POST /api/benutzer/{id}/freigeben – wartende Registrierung mit Rolle freigeben. */
export function useBenutzerFreigeben() {
  const neuLaden = useBenutzerNeuLaden()
  return useMutation({
    mutationFn: ({ id, ...eingabe }: BenutzerFreigeben & { id: string }) =>
      daten(api.POST('/api/benutzer/{id}/freigeben', { params: { path: { id } }, body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/benutzer/{id}/ablehnen – wartende Registrierung ablehnen. */
export function useBenutzerAblehnen() {
  const neuLaden = useBenutzerNeuLaden()
  return useMutation({
    mutationFn: (id: string) => erledigt(api.POST('/api/benutzer/{id}/ablehnen', { params: { path: { id } } })),
    onSuccess: neuLaden,
  })
}
