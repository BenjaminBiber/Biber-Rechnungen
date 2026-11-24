import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { belegeNeuLaden } from './belege'
import { api, beiNichtAngemeldet, daten, zuApiFehler } from './client'
import type { ImportStatus, ImportVorgang, ImportVorgaengeAbfrage } from './typen'

export const importKeys = {
  alle: ['import'] as const,
  vorgaenge: (filter: ImportVorgaengeAbfrage) => ['import', 'vorgaenge', filter] as const,
  vorgang: (id: string) => ['import', 'vorgang', id] as const,
}

/** Status, in denen ein Vorgang noch läuft – solange wird gepollt. */
export const laufendeImportStatus: readonly ImportStatus[] = ['Angenommen', 'InVerarbeitung']

export function laeuftNoch(vorgang: ImportVorgang): boolean {
  return laufendeImportStatus.includes(vorgang.status)
}

const pollIntervall = 3000

/** GET /api/import/vorgaenge – pollt alle 3 s, solange ein Vorgang läuft. */
export function useImportVorgaenge(filter: ImportVorgaengeAbfrage = {}) {
  return useQuery({
    queryKey: importKeys.vorgaenge(filter),
    queryFn: ({ signal }) => daten(api.GET('/api/import/vorgaenge', { params: { query: filter }, signal })),
    refetchInterval: (query) => (query.state.data?.eintraege.some(laeuftNoch) ? pollIntervall : false),
  })
}

/** GET /api/import/vorgaenge/{id} – pollt alle 3 s, solange der Vorgang läuft. */
export function useImportVorgang(id: string) {
  return useQuery({
    queryKey: importKeys.vorgang(id),
    queryFn: ({ signal }) => daten(api.GET('/api/import/vorgaenge/{id}', { params: { path: { id } }, signal })),
    refetchInterval: (query) => (query.state.data && laeuftNoch(query.state.data) ? pollIntervall : false),
  })
}

/**
 * Serialisiert das Multipart-Formular vorab (`Request` erzeugt Grenze und Content-Type). XHR sendet dann
 * nur Bytes – das verhält sich im Browser gleich und übersteht die XHR-Nachbildung von MSW unter Node,
 * die einen `FormData`-Körper sonst als Text „[object FormData]“ weiterreicht.
 */
async function multipart(datei: File): Promise<{ inhalt: ArrayBuffer; contentType: string }> {
  const formular = new FormData()
  formular.append('dateien', datei, datei.name)
  const verpackt = new Request(globalThis.location?.href ?? 'http://localhost/', { method: 'POST', body: formular })
  return { inhalt: await verpackt.arrayBuffer(), contentType: verpackt.headers.get('Content-Type') ?? '' }
}

/**
 * Lädt eine Datei per `XMLHttpRequest` hoch (fetch kennt keinen Upload-Fortschritt).
 * `beiFortschritt` erhält 0–100.
 */
export async function dateiHochladen(datei: File, beiFortschritt: (prozent: number) => void): Promise<ImportVorgang[]> {
  const { inhalt, contentType } = await multipart(datei)
  return new Promise((erfuellen, ablehnen) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', '/api/import/upload')
    xhr.withCredentials = true
    xhr.setRequestHeader('Content-Type', contentType)
    xhr.responseType = 'text'
    xhr.upload.onprogress = (ereignis) => {
      if (ereignis.lengthComputable) beiFortschritt(Math.round((ereignis.loaded / ereignis.total) * 100))
    }
    xhr.onload = () => {
      let koerper: unknown = xhr.responseText
      try {
        koerper = xhr.responseText ? JSON.parse(xhr.responseText) : undefined
      } catch {
        // Kein JSON – Text bleibt als Fehlertitel.
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        beiFortschritt(100)
        erfuellen((koerper as ImportVorgang[] | undefined) ?? [])
        return
      }
      if (xhr.status === 401) beiNichtAngemeldet()
      ablehnen(zuApiFehler(xhr.status, koerper, xhr.statusText))
    }
    xhr.onerror = () => ablehnen(zuApiFehler(0, undefined, 'Netzwerkfehler beim Hochladen'))
    xhr.send(inhalt)
  })
}

/**
 * POST /api/import/upload – je Datei eine Anfrage mit eigenem Fortschritt.
 * `fortschritt[i]` gehört zu `dateien[i]` (0–100).
 */
export function useUpload() {
  const queryClient = useQueryClient()
  const [fortschritt, setFortschritt] = useState<number[]>([])
  const mutation = useMutation({
    mutationFn: async (dateien: File[]) => {
      setFortschritt(dateien.map(() => 0))
      const ergebnisse = await Promise.all(
        dateien.map((datei, index) =>
          dateiHochladen(datei, (prozent) =>
            setFortschritt((alt) => alt.map((wert, i) => (i === index ? prozent : wert))),
          ),
        ),
      )
      return ergebnisse.flat()
    },
    onSuccess: () =>
      Promise.all([queryClient.invalidateQueries({ queryKey: importKeys.alle }), belegeNeuLaden(queryClient)]),
  })
  return { ...mutation, fortschritt }
}
