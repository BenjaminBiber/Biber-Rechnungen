import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useEffect, useSyncExternalStore } from 'react'
import { ApiFehler } from '@/api/client'
import { dateiHochladen, importKeys } from '@/api/import'
import type { ImportVorgang } from '@/api/typen'
import * as ablage from '@/pwa/warteschlange'
import { ablehnungsgrund } from './pruefung'

/** `wartet`: liegt in der IndexedDB und wird gesendet, sobald das Netz wieder da ist. */
export type UploadStatus = 'wartet' | 'laedt' | 'verarbeitung' | 'fertig' | 'dublette' | 'abgelehnt' | 'fehler'

export type UploadEintrag = {
  id: string
  name: string
  groesse: number
  status: UploadStatus
  prozent: number
  vorgang?: ImportVorgang
  /** Vorgang eines übertragenen Eintrags, dessen Stand noch geladen wird (z. B. vom Service Worker gesendet). */
  vorgangId?: string
  /** Grund bei „abgelehnt“/„fehler“. */
  meldung?: string
  /** Netzwerkfehler – „Erneut“ ist möglich. */
  wiederholbar?: boolean
  herkunft?: ablage.Herkunft
  /** Zeitpunkt der Auswahl bzw. der Übertragung (ms). */
  erstelltAm?: number
  uebertragenAm?: number
}

/** Höchstens so viele Dateien werden gleichzeitig übertragen. */
export const maxGleichzeitig = 3

let eintraege: UploadEintrag[] = []
const dateien = new Map<string, File>()
const wartend: string[] = []
/** IDs, die eingereiht sind oder gerade gesendet werden. */
const inArbeit = new Set<string>()
let aktiv = 0
let queryClient: QueryClient | undefined
/** Letzter Sendeversuch endete mit 401 – wartende Uploads brauchen eine Anmeldung. */
let anmeldungNoetig = false
let gestartet = false
/** Laufende Schreibvorgänge in die IndexedDB. */
const ausstehend = new Set<Promise<void>>()
/** Wird beim Zurücksetzen erhöht, damit ein laufender Abgleich nichts Altes wiederherstellt. */
let generation = 0
/** Vom Benutzer entfernte Einträge – ein gleichzeitig laufender Abgleich holt sie nicht zurück. */
const entfernt = new Set<string>()
const hoerer = new Set<() => void>()

function melden() {
  hoerer.forEach((h) => h())
}

function setzen(id: string, aenderung: Partial<UploadEintrag>) {
  eintraege = eintraege.map((e) => (e.id === id ? { ...e, ...aenderung } : e))
  melden()
}

function hinzu(eintrag: UploadEintrag) {
  eintraege = [eintrag, ...eintraege]
  melden()
}

function online(): boolean {
  return typeof navigator === 'undefined' || navigator.onLine !== false
}

/** Bittet den Service Worker, wartende Uploads per Background Sync nachzusenden (wo vorhanden). */
function syncAnmelden() {
  if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return
  void navigator.serviceWorker.ready
    .then((registrierung) =>
      (registrierung as ServiceWorkerRegistration & { sync?: SyncManagerArt }).sync?.register(ablage.syncTag),
    )
    .catch(() => {})
}

type SyncManagerArt = { register: (tag: string) => Promise<void> }

function einreihen(id: string) {
  if (inArbeit.has(id) || !dateien.has(id)) return
  if (!online()) {
    setzen(id, { status: 'wartet' })
    syncAnmelden()
    return
  }
  inArbeit.add(id)
  wartend.push(id)
  weiter()
}

function weiter() {
  while (aktiv < maxGleichzeitig && wartend.length) {
    const id = wartend.shift()!
    const datei = dateien.get(id)
    if (!datei) {
      inArbeit.delete(id)
      continue
    }
    aktiv++
    void senden(id, datei).finally(() => {
      aktiv--
      inArbeit.delete(id)
      weiter()
    })
  }
}

async function senden(id: string, datei: File) {
  // Schon von jemand anderem (Service Worker, anderer Tab) in Arbeit? Dann nur den Stand übernehmen.
  const beansprucht = await ablage.beanspruchen(id).catch(() => undefined)
  if (!beansprucht && (await ablage.lesen(id).catch(() => undefined))) {
    setzen(id, { status: 'wartet' })
    return
  }
  setzen(id, { status: 'laedt', prozent: 0, meldung: undefined, wiederholbar: false })
  try {
    const vorgaenge = await dateiHochladen(datei, (prozent) => setzen(id, { prozent }))
    const vorgang = vorgaenge[0]
    const uebertragenAm = Date.now()
    await ablage
      .aktualisieren(id, { status: 'uebertragen', sendetSeit: undefined, uebertragenAm, vorgangId: vorgang?.id })
      .catch(() => {})
    anmeldungNoetig = false
    setzen(id, {
      prozent: 100,
      uebertragenAm,
      ...(vorgang ? ausVorgang(vorgang) : { status: 'fertig' }),
    })
    void queryClient?.invalidateQueries({ queryKey: importKeys.alle })
  } catch (fehler: unknown) {
    const status = fehler instanceof ApiFehler ? fehler.status : 0
    if (status === 0 || status === 401) {
      // Netzwerkfehler oder abgelaufene Anmeldung: Eintrag bleibt in der Warteschlange.
      await ablage.aktualisieren(id, { status: 'wartet', sendetSeit: undefined }).catch(() => {})
      if (status === 401) anmeldungNoetig = true
      setzen(id, { status: 'wartet', prozent: 0, wiederholbar: true })
      if (status === 0) syncAnmelden()
      return
    }
    const meldung = fehler instanceof Error ? fehler.message : 'Hochladen fehlgeschlagen.'
    await ablage.aktualisieren(id, { status: 'fehler', sendetSeit: undefined, meldung }).catch(() => {})
    setzen(id, { status: 'fehler', wiederholbar: false, meldung })
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

/** Eintrag der Oberfläche aus einem gespeicherten Eintrag (nach Neustart oder Senden durch den Service Worker). */
function ausAblage(e: ablage.WarteschlangenEintrag): UploadEintrag {
  const basis = {
    id: e.id,
    name: e.name,
    groesse: e.groesse,
    prozent: 0,
    herkunft: e.herkunft,
    erstelltAm: e.erstelltAm,
  }
  switch (e.status) {
    case 'uebertragen':
      return {
        ...basis,
        prozent: 100,
        uebertragenAm: e.uebertragenAm,
        ...(e.vorgangId ? { status: 'verarbeitung', vorgangId: e.vorgangId } : { status: 'fertig' }),
      }
    case 'fehler':
      return { ...basis, status: 'fehler', meldung: e.meldung ?? 'Hochladen fehlgeschlagen.' }
    default:
      return { ...basis, status: 'wartet', wiederholbar: true }
  }
}

/**
 * Gleicht die Oberfläche mit der IndexedDB ab und sendet alle wartenden Einträge, sofern online.
 * Läuft beim Start der App, beim `online`-Ereignis und wenn der Service Worker etwas geändert hat.
 */
async function nachsenden() {
  const stand = generation
  let gespeichert: ablage.WarteschlangenEintrag[]
  try {
    await ablage.aufraeumen()
    gespeichert = await ablage.auflisten()
  } catch {
    gespeichert = []
  }
  // Inzwischen zurückgesetzt – der gelesene Stand ist veraltet.
  if (stand !== generation) return
  for (const e of gespeichert) {
    if (entfernt.has(e.id)) continue
    const vorhanden = eintraege.find((x) => x.id === e.id)
    if (!vorhanden) {
      if (e.status === 'wartet' || e.status === 'sendet') dateien.set(e.id, new File([e.blob], e.name, { type: e.typ }))
      eintraege = [...eintraege, ausAblage(e)].sort((a, b) => (b.erstelltAm ?? 0) - (a.erstelltAm ?? 0))
      melden()
    } else if (
      vorhanden.status === 'wartet' &&
      !inArbeit.has(e.id) &&
      (e.status === 'uebertragen' || e.status === 'fehler')
    ) {
      setzen(e.id, ausAblage(e))
    }
  }
  for (const e of eintraege) if (e.status === 'wartet') einreihen(e.id)
}

export const warteschlange = {
  stand: (): UploadEintrag[] => eintraege,
  /** `true`, wenn wartende Uploads erst nach einer Anmeldung gesendet werden können. */
  anmeldungNoetig: (): boolean => anmeldungNoetig,
  /** QueryClient, dessen Import-Abfragen nach einem Upload neu geladen werden. */
  clientSetzen(client: QueryClient) {
    queryClient = client
  },
  /** Jede Datei wird zuerst in die IndexedDB geschrieben und von dort gesendet. */
  hinzufuegen(neu: File[], herkunft: ablage.Herkunft = 'dateien') {
    for (const datei of neu) {
      const id = ablage.neueId()
      const grund = ablehnungsgrund(datei)
      hinzu({
        id,
        name: datei.name,
        groesse: datei.size,
        status: grund ? 'abgelehnt' : online() ? 'laedt' : 'wartet',
        prozent: 0,
        herkunft,
        erstelltAm: Date.now(),
        ...(grund ? { meldung: grund } : {}),
      })
      if (grund) continue
      dateien.set(id, datei)
      const schreiben = ablage
        .ablegen(datei, herkunft, id)
        .catch(() => {})
        .then(() => einreihen(id))
      ausstehend.add(schreiben)
      void schreiben.finally(() => ausstehend.delete(schreiben))
    }
  },
  entfernen(id: string) {
    entfernt.add(id)
    dateien.delete(id)
    void ablage.entfernen(id).catch(() => {})
    eintraege = eintraege.filter((e) => e.id !== id)
    melden()
  },
  erneut(id: string) {
    if (!dateien.has(id)) return
    setzen(id, { status: online() ? 'laedt' : 'wartet', prozent: 0, meldung: undefined, wiederholbar: false })
    void ablage
      .aktualisieren(id, { status: 'wartet', sendetSeit: undefined })
      .catch(() => {})
      .then(() => einreihen(id))
  },
  /** Neuer Stand des verfolgten Vorgangs (aus `useImportVorgang`). */
  vorgangAktualisiert(id: string, vorgang: ImportVorgang) {
    const eintrag = eintraege.find((e) => e.id === id)
    if (!eintrag || eintrag.status === 'laedt' || eintrag.status === 'wartet') return
    setzen(id, ausVorgang(vorgang))
  },
  /**
   * Der Server kennt den Vorgang nicht (mehr), z. B. nach Zurücksetzen der Datenbank. Ohne diese Meldung
   * bliebe der Eintrag bis zum Aufräumen auf „In Verarbeitung“ und ließe sich nicht entfernen.
   */
  vorgangUnbekannt(id: string) {
    const eintrag = eintraege.find((e) => e.id === id)
    if (!eintrag || eintrag.status !== 'verarbeitung') return
    const meldung = 'Import-Vorgang nicht mehr vorhanden.'
    void ablage.aktualisieren(id, { status: 'fehler', meldung }).catch(() => {})
    setzen(id, { status: 'fehler', meldung, wiederholbar: false })
  },
  /** Abgleich mit der IndexedDB und Nachsenden wartender Einträge. */
  nachsenden,
  /**
   * Einmalig beim Start der App: lädt die gespeicherte Warteschlange und sendet beim `online`-Ereignis
   * bzw. nach einer Nachricht des Service Workers nach (Ersatzweg ohne Background Sync).
   */
  starten() {
    if (gestartet || typeof window === 'undefined') return
    gestartet = true
    window.addEventListener('online', () => void nachsenden())
    if ('serviceWorker' in navigator)
      navigator.serviceWorker.addEventListener('message', (ereignis: MessageEvent<{ typ?: string } | undefined>) => {
        if (ereignis.data?.typ === 'warteschlange-geaendert') void nachsenden()
        if (ereignis.data?.typ === 'anmeldung-noetig') {
          anmeldungNoetig = true
          void nachsenden()
        }
      })
    void nachsenden()
  },
  /** Nur für Tests. */
  async zuruecksetzen() {
    await Promise.all(ausstehend)
    generation++
    eintraege = []
    dateien.clear()
    wartend.length = 0
    inArbeit.clear()
    aktiv = 0
    anmeldungNoetig = false
    hoerer.forEach((h) => h())
    for (const e of await ablage.auflisten().catch(() => [])) await ablage.entfernen(e.id)
  },
}

function abonnieren(h: () => void) {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

/** Upload-Warteschlange – überlebt das Verlassen der Seite und (über die IndexedDB) einen Neustart. */
export function useUploadWarteschlange(): {
  eintraege: UploadEintrag[]
  hinzufuegen: (dateien: File[]) => void
  entfernen: (id: string) => void
  erneut: (id: string) => void
  /** `true`, wenn wartende Uploads erst nach einer Anmeldung gesendet werden. */
  anmeldungNoetig: boolean
} {
  const client = useQueryClient()
  useEffect(() => warteschlange.clientSetzen(client), [client])
  useEffect(() => warteschlange.starten(), [])
  const stand = useSyncExternalStore(abonnieren, () => eintraege)
  const anmelden = useSyncExternalStore(abonnieren, () => anmeldungNoetig)
  return {
    eintraege: stand,
    hinzufuegen: warteschlange.hinzufuegen,
    entfernen: warteschlange.entfernen,
    erneut: warteschlange.erneut,
    anmeldungNoetig: anmelden,
  }
}
