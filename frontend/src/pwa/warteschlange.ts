import { openDB, type DBSchema, type IDBPDatabase } from 'idb'

/**
 * Dauerhafte Upload-Warteschlange in der IndexedDB `biber-uploads`. Wird von der App und vom
 * Service Worker gemeinsam benutzt – deshalb nur relative Importe und keine DOM-Abhängigkeiten.
 */

export type WarteschlangenStatus = 'wartet' | 'sendet' | 'uebertragen' | 'fehler'
export type Herkunft = 'dateien' | 'geteilt'

export type WarteschlangenEintrag = {
  id: string
  name: string
  groesse: number
  typ: string
  blob: Blob
  herkunft: Herkunft
  status: WarteschlangenStatus
  /** Zeitpunkt in Millisekunden (Date.now()). */
  erstelltAm: number
  vorgangId?: string
  /** Seit wann der Eintrag gesendet wird – hängengebliebene Sendungen werden wieder freigegeben. */
  sendetSeit?: number
  uebertragenAm?: number
  meldung?: string
}

export const datenbankName = 'biber-uploads'
const ablage = 'eintraege'

/** Background-Sync-Tag, unter dem der Service Worker wartende Uploads nachsendet. */
export const syncTag = 'uploads'

/** Übertragene Einträge bleiben so lange sichtbar. */
export const sichtbarNachUebertragung = 24 * 60 * 60 * 1000

/** Nach dieser Zeit gilt eine Sendung als abgebrochen (Tab geschlossen) und darf erneut starten. */
const sendenAbgebrochenNach = 2 * 60 * 1000

interface UploadSchema extends DBSchema {
  eintraege: { key: string; value: WarteschlangenEintrag }
}

let verbindung: Promise<IDBPDatabase<UploadSchema>> | undefined

function db(): Promise<IDBPDatabase<UploadSchema>> {
  verbindung ??= openDB<UploadSchema>(datenbankName, 1, {
    upgrade(datenbank) {
      datenbank.createObjectStore(ablage, { keyPath: 'id' })
    },
    // Eine neuere Version (anderer Tab) will die Datenbank aktualisieren – Verbindung freigeben.
    blocking() {
      void verbindung?.then((d) => d.close())
      verbindung = undefined
    },
  })
  return verbindung
}

let zaehler = 0

/** Eindeutige ID, auch über App und Service Worker hinweg. */
export function neueId(): string {
  return `upload-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}-${++zaehler}`
}

/** Legt eine Datei als wartenden Eintrag an. */
export async function ablegen(
  datei: Blob & { name?: string },
  herkunft: Herkunft,
  id: string = neueId(),
): Promise<WarteschlangenEintrag> {
  const eintrag: WarteschlangenEintrag = {
    id,
    name: datei.name ?? 'Datei',
    groesse: datei.size,
    typ: datei.type,
    blob: datei,
    herkunft,
    status: 'wartet',
    erstelltAm: Date.now(),
  }
  await (await db()).put(ablage, eintrag)
  return eintrag
}

/** Alle Einträge, neueste zuerst. */
export async function auflisten(): Promise<WarteschlangenEintrag[]> {
  const alle = await (await db()).getAll(ablage)
  return alle.sort((a, b) => b.erstelltAm - a.erstelltAm)
}

export async function lesen(id: string): Promise<WarteschlangenEintrag | undefined> {
  return (await db()).get(ablage, id)
}

/** Ändert einen Eintrag; fehlt er, passiert nichts. */
export async function aktualisieren(
  id: string,
  aenderung: Partial<Omit<WarteschlangenEintrag, 'id'>>,
): Promise<WarteschlangenEintrag | undefined> {
  const tx = (await db()).transaction(ablage, 'readwrite')
  const vorhanden = await tx.store.get(id)
  if (!vorhanden) {
    await tx.done
    return undefined
  }
  const neu = { ...vorhanden, ...aenderung }
  await tx.store.put(neu)
  await tx.done
  return neu
}

export async function entfernen(id: string): Promise<void> {
  await (await db()).delete(ablage, id)
}

/**
 * Setzt einen wartenden Eintrag atomar auf `sendet` und liefert ihn – oder `undefined`, wenn ihn
 * schon jemand anderes (App oder Service Worker) sendet. So geht keine Datei doppelt hinaus.
 */
export async function beanspruchen(id: string, jetzt = Date.now()): Promise<WarteschlangenEintrag | undefined> {
  const tx = (await db()).transaction(ablage, 'readwrite')
  const eintrag = await tx.store.get(id)
  const frei =
    eintrag?.status === 'wartet' ||
    (eintrag?.status === 'sendet' && jetzt - (eintrag.sendetSeit ?? 0) > sendenAbgebrochenNach)
  if (!eintrag || !frei) {
    await tx.done
    return undefined
  }
  const neu: WarteschlangenEintrag = { ...eintrag, status: 'sendet', sendetSeit: jetzt }
  await tx.store.put(neu)
  await tx.done
  return neu
}

/** Entfernt übertragene Einträge, die älter als 24 Stunden sind. */
export async function aufraeumen(jetzt = Date.now()): Promise<void> {
  const tx = (await db()).transaction(ablage, 'readwrite')
  for (const eintrag of await tx.store.getAll()) {
    if (
      eintrag.status === 'uebertragen' &&
      jetzt - (eintrag.uebertragenAm ?? eintrag.erstelltAm) > sichtbarNachUebertragung
    )
      await tx.store.delete(eintrag.id)
  }
  await tx.done
}

/** Nur für Tests: Verbindung schließen, damit eine „neue Instanz“ die Datenbank neu öffnet. */
export async function schliessen(): Promise<void> {
  const offen = verbindung
  verbindung = undefined
  if (offen) (await offen).close()
}

export type SendeErgebnis = 'uebertragen' | 'wartet' | 'nicht-angemeldet' | 'fehler'

/**
 * Sendet einen Eintrag per `fetch` an `POST /api/import/upload` (Service Worker, ohne Fortschritt).
 * Netzwerkfehler und 401 lassen ihn `wartet`, andere Fehler setzen `fehler`.
 */
export async function sendenPerFetch(eintrag: WarteschlangenEintrag, f: typeof fetch = fetch): Promise<SendeErgebnis> {
  const formular = new FormData()
  formular.append('dateien', eintrag.blob, eintrag.name)
  let antwort: Response
  try {
    antwort = await f('/api/import/upload', { method: 'POST', body: formular, credentials: 'include' })
  } catch {
    await aktualisieren(eintrag.id, { status: 'wartet', sendetSeit: undefined })
    return 'wartet'
  }
  if (antwort.status === 401) {
    await aktualisieren(eintrag.id, { status: 'wartet', sendetSeit: undefined })
    return 'nicht-angemeldet'
  }
  if (!antwort.ok) {
    await aktualisieren(eintrag.id, { status: 'fehler', sendetSeit: undefined, meldung: 'Hochladen fehlgeschlagen.' })
    return 'fehler'
  }
  const vorgaenge = (await antwort.json().catch(() => [])) as { id?: string }[]
  await aktualisieren(eintrag.id, {
    status: 'uebertragen',
    sendetSeit: undefined,
    uebertragenAm: Date.now(),
    vorgangId: vorgaenge[0]?.id,
  })
  return 'uebertragen'
}

/** Sendet alle wartenden Einträge nacheinander; liefert die Zahl der noch wartenden bei 401. */
export async function alleWartendenSenden(f: typeof fetch = fetch): Promise<{ nichtAngemeldet: number }> {
  let nichtAngemeldet = 0
  for (const kandidat of await auflisten()) {
    const eintrag = await beanspruchen(kandidat.id)
    if (!eintrag) continue
    const ergebnis = await sendenPerFetch(eintrag, f)
    if (ergebnis === 'nicht-angemeldet') nichtAngemeldet++
    // Ohne Netz hat es für die übrigen keinen Sinn – der nächste Sync versucht es erneut.
    if (ergebnis === 'wartet') throw new Error('Netzwerk nicht erreichbar')
  }
  return { nichtAngemeldet }
}
