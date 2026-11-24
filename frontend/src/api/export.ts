import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiUrl, daten, zuApiFehler } from './client'
import type { ExportAnfrage } from './typen'

export const exportKeys = {
  alle: ['export'] as const,
  vorschau: (anfrage: ExportAnfrage) => ['export', 'vorschau', anfrage] as const,
  laeufe: () => ['export', 'laeufe'] as const,
}

/**
 * POST /api/export/vorschau – Anzahl, Summen und Kategorien für die gewählten Einstellungen.
 * Ändert nichts, daher als Query (neu bei jeder Änderung der Anfrage).
 */
export function useExportVorschau(anfrage: ExportAnfrage | undefined) {
  return useQuery({
    queryKey: exportKeys.vorschau(anfrage as ExportAnfrage),
    queryFn: ({ signal }) => daten(api.POST('/api/export/vorschau', { body: anfrage as ExportAnfrage, signal })),
    enabled: anfrage !== undefined,
    placeholderData: keepPreviousData,
  })
}

/** POST /api/export – Export erzeugen; danach erscheint er in den Läufen. */
export function useExportErzeugen() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (anfrage: ExportAnfrage) => daten(api.POST('/api/export', { body: anfrage })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: exportKeys.laeufe() }),
  })
}

/** GET /api/export/laeufe – bisherige Exporte. */
export function useExportLaeufe() {
  return useQuery({
    queryKey: exportKeys.laeufe(),
    queryFn: ({ signal }) => daten(api.GET('/api/export/laeufe', { signal })),
  })
}

/** GET /api/export/laeufe/{id}/datei – ZIP/CSV eines Laufs herunterladen. */
export function exportDateiUrl(id: string): string {
  return apiUrl(`/api/export/laeufe/${id}/datei`)
}

/**
 * Lädt die Datei eines Laufs und speichert sie über einen Download-Link.
 * Wirft `ApiFehler`, z. B. 410, wenn die Exportdatei auf dem Server fehlt.
 */
export async function exportDateiHerunterladen(id: string): Promise<void> {
  const antwort = await globalThis.fetch(exportDateiUrl(id), { credentials: 'include' })
  if (!antwort.ok) {
    const text = await antwort.text()
    let koerper: unknown = text
    try {
      koerper = JSON.parse(text)
    } catch {
      // Kein ProblemDetails – Text bleibt Körper.
    }
    throw zuApiFehler(antwort.status, koerper, antwort.statusText)
  }
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(antwort.headers.get('Content-Disposition') ?? '')?.[1]
  const url = URL.createObjectURL(await antwort.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = name ? decodeURIComponent(name) : ''
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
