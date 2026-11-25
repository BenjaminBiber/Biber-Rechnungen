import type {
  BelegAnsicht,
  BelegeAbfrage,
  BelegFilter,
  BelegSortierung,
  BelegSuchfeld,
  BetragArt,
  ImportQuelle,
  Zahlstatus,
} from '@/api/typen'

export type Darstellung = 'tabelle' | 'klein' | 'gross'

/**
 * Listen-Zustand der Belegliste – steht vollständig in den Suchparametern der URL (teilbar, Zurück-Taste).
 * Vorgaben fehlen in der URL: Ansicht „Alle“, Tabelle, Rechnungsdatum absteigend, Seite 1.
 */
export type BelegeSuche = {
  ansicht?: BelegAnsicht
  darstellung?: Darstellung
  suche?: string
  suchfeld?: BelegSuchfeld
  tags?: string[]
  korrespondenten?: string[]
  kategorien?: string[]
  von?: string
  bis?: string
  betragVon?: number
  betragBis?: number
  betragArt?: BetragArt
  zahlstatus?: Zahlstatus
  quellen?: ImportQuelle[]
  sortierung?: BelegSortierung
  absteigend?: boolean
  seite?: number
}

/**
 * Zugriff der Komponenten auf den Listen-Zustand. `setze` übernimmt die genannten Werte (`undefined` löscht)
 * und springt auf Seite 1, sofern nicht `seite` selbst gesetzt wird; `ersetze` setzt den Zustand komplett.
 */
export type ListenZustand = {
  suche: BelegeSuche
  setze: (teil: Partial<BelegeSuche>) => void
  ersetze: (neu: BelegeSuche) => void
}

/** Belege je Seite (das Design zeigt 248 Belege auf 21 Seiten). */
export const SEITENGROESSE = 12

export const ansichten: BelegAnsicht[] = ['Alle', 'Inbox', 'ZuPruefen', 'Offen', 'Ueberfaellig']
export const darstellungen: Darstellung[] = ['tabelle', 'klein', 'gross']
export const sortierungen: BelegSortierung[] = [
  'Rechnungsdatum',
  'Faelligkeit',
  'Brutto',
  'Korrespondent',
  'Titel',
  'Rechnungsnummer',
  'Kategorie',
  'Netto',
  'Leistungsdatum',
  'Seiten',
  'Herkunft',
  'Konfidenz',
  'ErstelltAm',
]
export const zahlstatusWerte: Zahlstatus[] = ['Offen', 'Bezahlt', 'Ueberfaellig']
export const quellenWerte: ImportQuelle[] = ['Upload', 'Ordner', 'Mail', 'Mcp']
const suchfelder: BelegSuchfeld[] = ['TitelInhalt', 'Rechnungsnummer']
const betragArten: BetragArt[] = ['Brutto', 'Netto']

const ISO_DATUM = /^\d{4}-\d{2}-\d{2}$/

function auswahl<T extends string>(werte: readonly T[], wert: unknown): T | undefined {
  return werte.includes(wert as T) ? (wert as T) : undefined
}

function text(wert: unknown): string | undefined {
  if (typeof wert === 'number') return String(wert)
  return typeof wert === 'string' && wert.trim() !== '' ? wert : undefined
}

function datum(wert: unknown): string | undefined {
  return typeof wert === 'string' && ISO_DATUM.test(wert) ? wert : undefined
}

function zahl(wert: unknown): number | undefined {
  const n = typeof wert === 'string' && wert.trim() !== '' ? Number(wert) : wert
  return typeof n === 'number' && Number.isFinite(n) ? n : undefined
}

function wahrheitswert(wert: unknown): boolean | undefined {
  if (wert === true || wert === 'true') return true
  if (wert === false || wert === 'false') return false
  return undefined
}

function liste<T extends string = string>(wert: unknown, erlaubt?: readonly T[]): T[] | undefined {
  const roh = Array.isArray(wert) ? wert : typeof wert === 'string' ? [wert] : []
  const gueltig = roh.filter((w): w is T => typeof w === 'string' && w !== '' && (!erlaubt || erlaubt.includes(w as T)))
  return gueltig.length ? gueltig : undefined
}

/** Prüft rohe Suchparameter; ungültige Werte und Vorgaben fallen weg. */
export function sucheValidieren(roh: Record<string, unknown>): BelegeSuche {
  const seite = zahl(roh.seite)
  const ansicht = auswahl(ansichten, roh.ansicht)
  const darstellung = auswahl(darstellungen, roh.darstellung)
  const suchfeld = auswahl(suchfelder, roh.suchfeld)
  const betragArt = auswahl(betragArten, roh.betragArt)
  const sortierung = auswahl(sortierungen, roh.sortierung)
  const absteigend = wahrheitswert(roh.absteigend)
  const suche: BelegeSuche = {
    ansicht: ansicht === 'Alle' ? undefined : ansicht,
    darstellung: darstellung === 'tabelle' ? undefined : darstellung,
    suche: text(roh.suche),
    suchfeld: suchfeld === 'TitelInhalt' ? undefined : suchfeld,
    tags: liste(roh.tags),
    korrespondenten: liste(roh.korrespondenten),
    kategorien: liste(roh.kategorien),
    von: datum(roh.von),
    bis: datum(roh.bis),
    betragVon: zahl(roh.betragVon),
    betragBis: zahl(roh.betragBis),
    betragArt: betragArt === 'Brutto' ? undefined : betragArt,
    zahlstatus: auswahl(zahlstatusWerte, roh.zahlstatus),
    quellen: liste(roh.quellen, quellenWerte),
    sortierung: sortierung === 'Rechnungsdatum' ? undefined : sortierung,
    absteigend: absteigend === false ? false : undefined,
    seite: seite !== undefined && Number.isInteger(seite) && seite > 1 ? seite : undefined,
  }
  return ohneLeere(suche)
}

/** Entfernt nicht gesetzte Schlüssel, damit die URL nur gesetzte Parameter trägt. */
export function ohneLeere(suche: BelegeSuche): BelegeSuche {
  return Object.fromEntries(Object.entries(suche).filter(([, w]) => w !== undefined)) as BelegeSuche
}

/** Suchparameter → Query-Parameter für `GET /api/belege`. */
export function zuAbfrage(suche: BelegeSuche): BelegeAbfrage {
  const abfrage: BelegeAbfrage = {
    ansicht: suche.ansicht,
    suche: suche.suche,
    suchfeld: suche.suche ? suche.suchfeld : undefined,
    tagIds: suche.tags,
    korrespondentIds: suche.korrespondenten,
    kategorieIds: suche.kategorien,
    von: suche.von,
    bis: suche.bis,
    betragVon: suche.betragVon,
    betragBis: suche.betragBis,
    betragArt: suche.betragVon !== undefined || suche.betragBis !== undefined ? suche.betragArt : undefined,
    zahlstatus: suche.zahlstatus,
    quellen: suche.quellen,
    sortierung: suche.sortierung,
    absteigend: suche.absteigend,
    seite: suche.seite,
    seitengroesse: SEITENGROESSE,
  }
  return Object.fromEntries(Object.entries(abfrage).filter(([, w]) => w !== undefined)) as BelegeAbfrage
}

/** Suchparameter → Filter einer gespeicherten Ansicht (vollständiges `BelegFilterDto`). */
export function zuFilter(suche: BelegeSuche): BelegFilter {
  return {
    suche: suche.suche ?? null,
    suchfeld: suche.suchfeld ?? 'TitelInhalt',
    ansicht: suche.ansicht ?? 'Alle',
    tagIds: suche.tags ?? [],
    korrespondentIds: suche.korrespondenten ?? [],
    kategorieIds: suche.kategorien ?? [],
    von: suche.von ?? null,
    bis: suche.bis ?? null,
    betragVon: suche.betragVon ?? null,
    betragBis: suche.betragBis ?? null,
    betragArt: suche.betragArt ?? 'Brutto',
    zahlstatus: suche.zahlstatus ?? null,
    quellen: suche.quellen ?? [],
    dublettenAusschliessen: true,
    sortierung: suche.sortierung ?? 'Rechnungsdatum',
    absteigend: suche.absteigend ?? true,
    seite: 1,
    seitengroesse: SEITENGROESSE,
  }
}

/** Filter einer gespeicherten Ansicht → Suchparameter (Seite 1). */
export function ausFilter(filter: BelegFilter, darstellung: string): BelegeSuche {
  return sucheValidieren({
    ansicht: filter.ansicht,
    darstellung,
    suche: filter.suche,
    suchfeld: filter.suchfeld,
    tags: filter.tagIds,
    korrespondenten: filter.korrespondentIds,
    kategorien: filter.kategorieIds,
    von: filter.von,
    bis: filter.bis,
    betragVon: filter.betragVon,
    betragBis: filter.betragBis,
    betragArt: filter.betragArt,
    zahlstatus: filter.zahlstatus,
    quellen: filter.quellen,
    sortierung: filter.sortierung,
    absteigend: filter.absteigend,
  })
}

/** Alle Filter samt Ansicht und Suche zurücksetzen; Darstellung und Sortierung bleiben. */
export function filterZurueckgesetzt(suche: BelegeSuche): BelegeSuche {
  return ohneLeere({ darstellung: suche.darstellung, sortierung: suche.sortierung, absteigend: suche.absteigend })
}

export const ansichtTitel: Record<BelegAnsicht, string> = {
  Alle: 'Alle Belege',
  Inbox: 'Inbox',
  ZuPruefen: 'Zu prüfen',
  Offen: 'Offen',
  Ueberfaellig: 'Überfällig',
}

export const sortierungText: Record<BelegSortierung, string> = {
  Rechnungsdatum: 'Rechnungsdatum',
  Faelligkeit: 'Fälligkeit',
  Brutto: 'Brutto',
  Korrespondent: 'Korrespondent',
  ErstelltAm: 'Erfasst am',
  Rechnungsnummer: 'Rechnungsnr.',
  Titel: 'Titel',
  Kategorie: 'Kategorie',
  Netto: 'Netto',
  Seiten: 'Seiten',
  Herkunft: 'Herkunft',
  Leistungsdatum: 'Leistungsdatum',
  Konfidenz: 'Konfidenz',
}

export const zahlstatusText: Record<Zahlstatus, string> = {
  Offen: 'Offen',
  Bezahlt: 'Bezahlt',
  Ueberfaellig: 'Überfällig',
}

export const quelleText: Record<ImportQuelle, string> = {
  Upload: 'Upload',
  Ordner: 'Ordner',
  Mail: 'E-Mail',
  Mcp: 'MCP',
}

export const quelleIcon: Record<ImportQuelle, string> = {
  Upload: 'upload',
  Ordner: 'folder_open',
  Mail: 'mail',
  Mcp: 'smart_toy',
}
