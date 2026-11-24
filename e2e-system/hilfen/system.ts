import { expect, type APIRequestContext, type Page } from '@playwright/test'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { frischerTotpCode } from './totp'

const hier = dirname(fileURLToPath(import.meta.url))
const zustandOrdner = join(hier, '..', '.zustand')

/** Angemeldete Sitzung des Admins (Cookies), von 01 angelegt und von allen späteren Tests genutzt. */
export const ADMIN_SITZUNG = join(zustandOrdner, 'admin-sitzung.json')

export function testdatei(name: string): Buffer {
  return readFileSync(join(hier, '..', 'testdateien', name))
}

export interface Zugang {
  name: string
  email: string
  passwort: string
  totpSchluessel?: string
}

export const ADMIN: Omit<Zugang, 'totpSchluessel'> = {
  name: 'Maria Keller',
  email: 'm.keller@e2e.local',
  passwort: 'biber-e2e-passwort',
}

export function zugangSpeichern(zugang: Zugang): void {
  mkdirSync(zustandOrdner, { recursive: true })
  writeFileSync(join(zustandOrdner, 'admin.json'), JSON.stringify(zugang, null, 2))
}

export function adminZugang(): Zugang {
  return JSON.parse(readFileSync(join(zustandOrdner, 'admin.json'), 'utf8')) as Zugang
}

export async function sitzungSpeichern(page: Page): Promise<void> {
  mkdirSync(zustandOrdner, { recursive: true })
  await page.context().storageState({ path: ADMIN_SITZUNG })
}

/** Anmeldung über die API (Passwort, ggf. TOTP); Cookies landen im Kontext von `request`. */
export async function anmeldenPerApi(request: APIRequestContext, zugang: Zugang): Promise<string> {
  const antwort = await request.post('/api/auth/anmelden', {
    data: { email: zugang.email, passwort: zugang.passwort, angemeldetBleiben: false },
  })
  expect(antwort.status(), await antwort.text()).toBe(200)
  const { status } = (await antwort.json()) as { status: string }
  if (status !== 'TotpErforderlich') return status
  const code = await frischerTotpCode(zugang.totpSchluessel!)
  const totp = await request.post('/api/auth/totp', { data: { code, geraetVertrauen: false } })
  expect(totp.status(), await totp.text()).toBe(200)
  return ((await totp.json()) as { status: string }).status
}

export interface Vorgang {
  id: string
  status: string
  quelle: string
  dateiname: string
  ergebnis?: string | null
  fehler?: string | null
  belegId?: string | null
  duplikatVonBelegId?: string | null
}

/** Lädt Dateien über die Upload-API hoch (wie der Dialog „Hochladen“) und liefert die Vorgänge. */
export async function hochladen(request: APIRequestContext, ...dateien: { name: string; inhalt: Buffer }[]): Promise<Vorgang[]> {
  const formular = new FormData()
  for (const d of dateien) formular.append('dateien', new File([new Uint8Array(d.inhalt)], d.name, { type: mimeTyp(d.name) }))
  const antwort = await request.post('/api/import/upload', { multipart: formular })
  expect(antwort.status(), await antwort.text()).toBe(202)
  return (await antwort.json()) as Vorgang[]
}

function mimeTyp(name: string): string {
  if (name.endsWith('.pdf')) return 'application/pdf'
  if (name.endsWith('.xml')) return 'application/xml'
  if (name.endsWith('.heic')) return 'image/heic'
  return 'application/octet-stream'
}

/** Wartet, bis der Vorgang einen Endstatus erreicht (Hangfire fragt die Warteschlange nur alle paar Sekunden ab). */
export async function aufVorgangWarten(request: APIRequestContext, id: string, timeout = 90_000): Promise<Vorgang> {
  const ende = ['Fertig', 'Dublette', 'OhneBeleg', 'Abgelehnt', 'Fehler', 'Pausiert']
  let vorgang: Vorgang | undefined
  await expect
    .poll(
      async () => {
        vorgang = (await (await request.get(`/api/import/vorgaenge/${id}`)).json()) as Vorgang
        return vorgang.status
      },
      { timeout, intervals: [1000] },
    )
    .toMatch(new RegExp(`^(${ende.join('|')})$`))
  return vorgang!
}

/** Lädt hoch und wartet auf das Ende der Verarbeitung. */
export async function hochladenUndWarten(request: APIRequestContext, name: string, inhalt = testdatei(name)): Promise<Vorgang> {
  const [vorgang] = await hochladen(request, { name, inhalt })
  return aufVorgangWarten(request, vorgang.id)
}

export async function belegLesen(request: APIRequestContext, id: string): Promise<any> {
  const antwort = await request.get(`/api/belege/${id}`)
  expect(antwort.status()).toBe(200)
  return antwort.json()
}

/** Setzt Einstellungen eines Bereichs: liest den aktuellen Stand und überschreibt die genannten Felder. */
export async function einstellungenSetzen(request: APIRequestContext, bereich: string, aenderung: Record<string, unknown>): Promise<void> {
  const aktuell = (await (await request.get(`/api/einstellungen/${bereich}`)).json()) as Record<string, unknown>
  const antwort = await request.put(`/api/einstellungen/${bereich}`, { data: { ...aktuell, ...aenderung } })
  expect(antwort.ok(), await antwort.text()).toBeTruthy()
}
