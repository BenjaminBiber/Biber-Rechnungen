import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect, useSyncExternalStore } from 'react'
import { ApiFehler } from '@/api/client'
import { dateiHochladen, importKeys } from '@/api/import'
import type { ImportVorgang } from '@/api/typen'
import { ablehnungsgrund } from './pruefung'

export type UploadStatus = 'laedt' | 'verarbeitung' | 'fertig' | 'dublette' | 'abgelehnt' | 'fehler'

export type UploadEintrag = {
  id: string
  name: string
  groesse: number
  status: UploadStatus
  prozent: number
  vorgang?: ImportVorgang
  /** Grund bei „abgelehnt“/„fehler“. */
  meldung?: string
  /** Netzwerkfehler – „Erneut“ ist möglich. */
  wiederholbar?: boolean
}

/** Höchstens so viele Dateien werden gleichzeitig übertragen. */
export const maxGleichzeitig = 3

let eintraege: UploadEintrag[] = []
const dateien = new Map<string, File>()
const wartend: string[] = []
let aktiv = 0
let zaehler = 0
let queryClient: QueryClient | undefined
const hoerer = new Set<() => void>()

function setzen(id: string, aenderung: Partial<UploadEintrag>) {
  eintraege = eintraege.map((e) => (e.id === id ? { ...e, ...aenderung } : e))
  hoerer.forEach((h) => h())
}

function hinzu(eintrag: UploadEintrag) {
  eintraege = [eintrag, ...eintraege]
  hoerer.forEach((h) => h())
}

function weiter() {
  while (aktiv < maxGleichzeitig && wartend.length) {
    const id = wartend.shift()!
    const datei = dateien.get(id)
    if (!datei) continue
    aktiv++
    void dateiHochladen(datei, (prozent) => setzen(id, { prozent }))
      .then((vorgaenge) => {
        const vorgang = vorgaenge[0]
        setzen(id, vorgang ? { prozent: 100, ...ausVorgang(vorgang) } : { status: 'fertig', prozent: 100 })
        void queryClient?.invalidateQueries({ queryKey: importKeys.alle })
      })
      .catch((fehler: unknown) => {
        const netzwerk = fehler instanceof ApiFehler ? fehler.status === 0 : true
        setzen(id, {
          status: 'fehler',
          wiederholbar: netzwerk,
          meldung: netzwerk
            ? 'Netzwerkfehler beim Hochladen.'
            : fehler instanceof Error
              ? fehler.message
              : 'Hochladen fehlgeschlagen.',
        })
      })
      .finally(() => {
        aktiv--
        weiter()
      })
  }
}

/** Status des Eintrags aus dem Stand des Import-Vorgangs. */
export function ausVorgang(vorgang: ImportVorgang): Partial<UploadEintrag> {
  switch (vorgang.status) {
    case 'Fertig':
    case 'OhneBeleg':
      return { status: 'fertig', vorgang }
    case 'Dublette':
      return { status: 'dublette', vorgang }
    case 'Abgelehnt':
      return { status: 'abgelehnt', vorgang, meldung: vorgang.fehler ?? vorgang.ergebnis ?? 'Abgelehnt.' }
    case 'Fehler':
      return { status: 'fehler', vorgang, meldung: vorgang.fehler ?? 'Verarbeitung fehlgeschlagen.' }
    default:
      return { status: 'verarbeitung', vorgang }
  }
}

export const warteschlange = {
  stand: (): UploadEintrag[] => eintraege,
  /** QueryClient, dessen Import-Abfragen nach einem Upload neu geladen werden. */
  clientSetzen(client: QueryClient) {
    queryClient = client
  },
  hinzufuegen(neu: File[]) {
    for (const datei of neu) {
      const id = `upload-${++zaehler}`
      const grund = ablehnungsgrund(datei)
      hinzu({
        id,
        name: datei.name,
        groesse: datei.size,
        status: grund ? 'abgelehnt' : 'laedt',
        prozent: 0,
        ...(grund ? { meldung: grund } : {}),
      })
      if (grund) continue
      dateien.set(id, datei)
      wartend.push(id)
    }
    weiter()
  },
  entfernen(id: string) {
    dateien.delete(id)
    eintraege = eintraege.filter((e) => e.id !== id)
    hoerer.forEach((h) => h())
  },
  erneut(id: string) {
    if (!dateien.has(id)) return
    setzen(id, { status: 'laedt', prozent: 0, meldung: undefined, wiederholbar: false })
    wartend.push(id)
    weiter()
  },
  /** Neuer Stand des verfolgten Vorgangs (aus `useImportVorgang`). */
  vorgangAktualisiert(id: string, vorgang: ImportVorgang) {
    const eintrag = eintraege.find((e) => e.id === id)
    if (!eintrag || eintrag.status === 'laedt') return
    setzen(id, ausVorgang(vorgang))
  },
  /** Nur für Tests. */
  zuruecksetzen() {
    eintraege = []
    dateien.clear()
    wartend.length = 0
    aktiv = 0
    hoerer.forEach((h) => h())
  },
}

function abonnieren(h: () => void) {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

/** Upload-Warteschlange der Sitzung – überlebt das Verlassen der Seite. */
export function useUploadWarteschlange(): {
  eintraege: UploadEintrag[]
  hinzufuegen: (dateien: File[]) => void
  entfernen: (id: string) => void
  erneut: (id: string) => void
} {
  const client = useQueryClient()
  useEffect(() => warteschlange.clientSetzen(client), [client])
  const stand = useSyncExternalStore(abonnieren, () => eintraege)
  return {
    eintraege: stand,
    hinzufuegen: warteschlange.hinzufuegen,
    entfernen: warteschlange.entfernen,
    erneut: warteschlange.erneut,
  }
}
