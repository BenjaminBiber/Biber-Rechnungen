import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { authKeys } from './auth'
import { api, daten, erledigt } from './client'
import type { Code, KontoAendern, PasswortAendern } from './typen'

export const kontoKeys = {
  alle: ['konto'] as const,
  eigenes: () => ['konto', 'eigenes'] as const,
}

/** Konto und Anmeldestatus (Name im Benutzermenü) neu laden. */
function useKontoNeuLaden() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: kontoKeys.alle }),
      queryClient.invalidateQueries({ queryKey: authKeys.alle }),
    ])
}

/** GET /api/konto – eigenes Konto. */
export function useKonto() {
  return useQuery({
    queryKey: kontoKeys.eigenes(),
    queryFn: ({ signal }) => daten(api.GET('/api/konto', { signal })),
  })
}

/** PUT /api/konto – Name und E-Mail ändern. */
export function useKontoAendern() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: (eingabe: KontoAendern) => daten(api.PUT('/api/konto', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/konto/passwort – eigenes Passwort ändern. */
export function usePasswortAendern() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: (eingabe: PasswortAendern) => erledigt(api.POST('/api/konto/passwort', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/konto/totp/einrichten – neuen TOTP-Schlüssel erzeugen. */
export function useTotpEinrichten() {
  return useMutation({
    mutationFn: () => daten(api.POST('/api/konto/totp/einrichten')),
  })
}

/** POST /api/konto/totp/aktivieren – Code bestätigen, liefert Wiederherstellungscodes. */
export function useTotpAktivieren() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: (eingabe: Code) => daten(api.POST('/api/konto/totp/aktivieren', { body: eingabe })),
    onSuccess: neuLaden,
  })
}

/** POST /api/konto/totp/deaktivieren. */
export function useTotpDeaktivieren() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: () => erledigt(api.POST('/api/konto/totp/deaktivieren')),
    onSuccess: neuLaden,
  })
}

/** POST /api/konto/wiederherstellungscodes – neue Codes erzeugen (alte werden ungültig). */
export function useWiederherstellungscodesErzeugen() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/konto/wiederherstellungscodes')),
    onSuccess: neuLaden,
  })
}

/** POST /api/konto/sitzungen/andere-abmelden. */
export function useAndereAbmelden() {
  const neuLaden = useKontoNeuLaden()
  return useMutation({
    mutationFn: () => erledigt(api.POST('/api/konto/sitzungen/andere-abmelden')),
    onSuccess: neuLaden,
  })
}
