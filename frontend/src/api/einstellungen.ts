import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { authKeys } from './auth'
import { api, daten, erledigt } from './client'
import { dashboardKeys, systemKeys } from './dashboard'
import type { EinstellungGruppe, EinstellungInhalt, Einstellungen, MailZugangSpeichern } from './typen'

export const einstellungenKeys = {
  alle: ['einstellungen'] as const,
  werte: () => ['einstellungen', 'werte'] as const,
  diVerbrauch: () => ['einstellungen', 'di-verbrauch'] as const,
  mailStatus: () => ['einstellungen', 'mail-status'] as const,
}

/** GET /api/einstellungen – alle Gruppen samt Umgebung (nur lesbar). */
export function useEinstellungen() {
  return useQuery({
    queryKey: einstellungenKeys.werte(),
    queryFn: ({ signal }) => daten(api.GET('/api/einstellungen', { signal })),
  })
}

/** Zuordnung Gruppe → Inhalt, damit `useEinstellungenSpeichern` nur passende Werte annimmt. */
export type EinstellungGruppen = {
  allgemein: Einstellungen['allgemein']
  ust: Einstellungen['ust']
  pruefungen: Einstellungen['pruefungen']
  di: Einstellungen['di']
  mail: Einstellungen['mail']
  ordner: Einstellungen['ordner']
  aufbewahrung: Einstellungen['aufbewahrung']
  zugriff: Einstellungen['zugriff']
}

export type EinstellungSpeichern = {
  [G in EinstellungGruppe]: { gruppe: G; werte: EinstellungGruppen[G] & EinstellungInhalt }
}[EinstellungGruppe]

/** PUT /api/einstellungen/{gruppe} – eine Gruppe speichern. */
export function useEinstellungenSpeichern() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ gruppe, werte }: EinstellungSpeichern) =>
      erledigt(api.PUT('/api/einstellungen/{gruppe}', { params: { path: { gruppe } }, body: werte })),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: einstellungenKeys.alle }),
        // Zugriff (Registrierung, Google) steht im Anmeldestatus, Limits im Dashboard.
        queryClient.invalidateQueries({ queryKey: authKeys.alle }),
        queryClient.invalidateQueries({ queryKey: dashboardKeys.alle }),
        queryClient.invalidateQueries({ queryKey: systemKeys.alle }),
      ]),
  })
}

/** GET /api/einstellungen/di/verbrauch – verbrauchte Document-Intelligence-Seiten im Monat. */
export function useDiVerbrauch() {
  return useQuery({
    queryKey: einstellungenKeys.diVerbrauch(),
    queryFn: ({ signal }) => daten(api.GET('/api/einstellungen/di/verbrauch', { signal })),
  })
}

/** POST /api/einstellungen/di/verbindung-testen. */
export function useDiVerbindungTesten() {
  return useMutation({
    mutationFn: () => daten(api.POST('/api/einstellungen/di/verbindung-testen')),
  })
}

/** POST /api/einstellungen/di/fortsetzen – pausierte Verarbeitung nach Limit fortsetzen. */
export function useDiFortsetzen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/einstellungen/di/fortsetzen')),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: einstellungenKeys.diVerbrauch() }),
        queryClient.invalidateQueries({ queryKey: ['import'] }),
        queryClient.invalidateQueries({ queryKey: dashboardKeys.alle }),
      ]),
  })
}

/** PUT /api/einstellungen/mail/zugang – IMAP-Zugang, nur wenn er nicht per .env gesetzt ist. Leeres Passwort = behalten. */
export function useMailZugangSpeichern() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (zugang: MailZugangSpeichern) => erledigt(api.PUT('/api/einstellungen/mail/zugang', { body: zugang })),
    onSuccess: () =>
      Promise.all([
        // Werte samt Zugang und Mail-Status (ein alter Fehler wird verworfen).
        queryClient.invalidateQueries({ queryKey: einstellungenKeys.alle }),
        queryClient.invalidateQueries({ queryKey: systemKeys.alle }),
      ]),
  })
}

/** POST /api/einstellungen/mail/verbindung-testen. */
export function useMailVerbindungTesten() {
  return useMutation({
    mutationFn: () => daten(api.POST('/api/einstellungen/mail/verbindung-testen')),
  })
}

/** POST /api/einstellungen/mail/jetzt-abrufen. */
export function useMailJetztAbrufen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => daten(api.POST('/api/einstellungen/mail/jetzt-abrufen')),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: einstellungenKeys.mailStatus() }),
        queryClient.invalidateQueries({ queryKey: ['import'] }),
      ]),
  })
}

/** GET /api/einstellungen/mail/status – Verbindung und letzter Abruf. */
export function useMailStatus() {
  return useQuery({
    queryKey: einstellungenKeys.mailStatus(),
    queryFn: ({ signal }) => daten(api.GET('/api/einstellungen/mail/status', { signal })),
  })
}
