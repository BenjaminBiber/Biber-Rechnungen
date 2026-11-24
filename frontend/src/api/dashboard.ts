import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, daten } from './client'
import type { DashboardAbfrage } from './typen'

export const dashboardKeys = {
  alle: ['dashboard'] as const,
  uebersicht: (filter: DashboardAbfrage) => ['dashboard', 'uebersicht', filter] as const,
}

export const systemKeys = {
  alle: ['system'] as const,
  status: () => ['system', 'status'] as const,
  backup: () => ['system', 'backup'] as const,
}

/** GET /api/dashboard – Kennzahlen, Diagramme und Listen für Zeitraum/Jahr. */
export function useDashboard(filter: DashboardAbfrage = {}) {
  return useQuery({
    queryKey: dashboardKeys.uebersicht(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/dashboard', { params: { query: filter }, signal })),
    placeholderData: keepPreviousData,
  })
}

/** GET /api/system/status – Statusmeldungen (Dienste, Limits, Prüfungen). */
export function useSystemStatus() {
  return useQuery({
    queryKey: systemKeys.status(),
    queryFn: ({ signal }) => daten(api.GET('/api/system/status', { signal })),
  })
}

/** GET /api/system/backup – Stand der Datenbank-Sicherung. */
export function useBackupStatus() {
  return useQuery({
    queryKey: systemKeys.backup(),
    queryFn: ({ signal }) => daten(api.GET('/api/system/backup', { signal })),
  })
}
