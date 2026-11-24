import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiUrl, daten, erledigt } from './client'
import type { Anmelden, Code, Ersteinrichtung, Registrieren, Totp } from './typen'

export const authKeys = {
  alle: ['auth'] as const,
  status: () => ['auth', 'status'] as const,
  totpVorschlag: () => ['auth', 'totp-vorschlag'] as const,
}

/** GET /api/auth/status – Einrichtungs- und Anmeldestatus. */
export function useAuthStatus() {
  return useQuery({
    queryKey: authKeys.status(),
    queryFn: ({ signal }) => daten(api.GET('/api/auth/status', { signal })),
  })
}

/** GET /api/auth/totp-vorschlag – TOTP-Schlüssel für die Ersteinrichtung. */
export function useTotpVorschlag({ aktiv = true }: { aktiv?: boolean } = {}) {
  return useQuery({
    queryKey: authKeys.totpVorschlag(),
    queryFn: ({ signal }) => daten(api.GET('/api/auth/totp-vorschlag', { signal })),
    enabled: aktiv,
    staleTime: Infinity,
  })
}

/** Nach jedem Anmeldeschritt ist der Status neu zu laden; alles andere hängt am Benutzer. */
function useNachAnmeldung() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries()
}

/** POST /api/auth/ersteinrichtung – legt den ersten Admin an. */
export function useErsteinrichtung() {
  const nachAnmeldung = useNachAnmeldung()
  return useMutation({
    mutationFn: (eingabe: Ersteinrichtung) => daten(api.POST('/api/auth/ersteinrichtung', { body: eingabe })),
    onSuccess: nachAnmeldung,
  })
}

/** POST /api/auth/anmelden – E-Mail und Passwort. */
export function useAnmelden() {
  const nachAnmeldung = useNachAnmeldung()
  return useMutation({
    mutationFn: (eingabe: Anmelden) => daten(api.POST('/api/auth/anmelden', { body: eingabe })),
    onSuccess: nachAnmeldung,
  })
}

/** POST /api/auth/totp – zweiter Faktor. */
export function useTotp() {
  const nachAnmeldung = useNachAnmeldung()
  return useMutation({
    mutationFn: (eingabe: Totp) => daten(api.POST('/api/auth/totp', { body: eingabe })),
    onSuccess: nachAnmeldung,
  })
}

/** POST /api/auth/wiederherstellungscode – Anmeldung mit Wiederherstellungscode. */
export function useWiederherstellungscode() {
  const nachAnmeldung = useNachAnmeldung()
  return useMutation({
    mutationFn: (eingabe: Code) => daten(api.POST('/api/auth/wiederherstellungscode', { body: eingabe })),
    onSuccess: nachAnmeldung,
  })
}

/** POST /api/auth/abmelden – beendet die Sitzung und leert den Cache. */
export function useAbmelden() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => erledigt(api.POST('/api/auth/abmelden')),
    onSuccess: () => {
      queryClient.clear()
    },
  })
}

/** POST /api/auth/registrieren – Konto beantragen (wartet auf Freigabe). */
export function useRegistrieren() {
  return useMutation({
    mutationFn: (eingabe: Registrieren) => erledigt(api.POST('/api/auth/registrieren', { body: eingabe })),
  })
}

/**
 * GET /api/auth/google/start – Browser-Weiterleitung zu Google (kein fetch).
 * GET /api/auth/google/rueckruf ruft Google selbst auf; dafür gibt es keinen Hook.
 */
export function googleStartUrl(verknuepfen?: boolean): string {
  return apiUrl('/api/auth/google/start', { verknuepfen })
}
