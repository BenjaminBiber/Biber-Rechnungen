import type {
  ApiToken,
  AuditEintrag,
  AutoMatchingStatus,
  BackupStatus,
  BelegDetail,
  Benachrichtigung,
  Benutzer,
  Dashboard,
  DiVerbrauch,
  Einstellungen,
  ExportLauf,
  GespeicherteAnsicht,
  ImportVorgang,
  IntegritaetsLauf,
  Kategorie,
  Konto,
  Loeschbar,
  MailStatus,
  PreisDetail,
  StammartikelListeneintrag,
  SystemStatusEintrag,
  Tag,
  Verfahrensdoku,
  ArtikelListeneintrag,
} from '@/api/typen'
import { ansichten } from './fixtures/ansichten'
import { artikelListe, preisDetails, stammartikelListe } from './fixtures/artikel'
import { angemeldeterBenutzer, mockZugang, wiederherstellungscodes } from './fixtures/auth'
import { belegHistorien, belege } from './fixtures/belege'
import { benachrichtigungen } from './fixtures/benachrichtigungen'
import { benutzerListe } from './fixtures/benutzer'
import { backupStatus, dashboard, diVerbrauch, systemStatus } from './fixtures/dashboard'
import { einstellungen, integritaetsLaeufe, loeschbar, mailStatus, verfahrensdoku } from './fixtures/einstellungen'
import { exportLaeufe } from './fixtures/export'
import { importVorgaenge } from './fixtures/import'
import { konto } from './fixtures/konto'
import { korrespondenten, type KorrespondentDatensatz } from './fixtures/korrespondenten'
import { autoMatchingStatus, kategorien, tags } from './fixtures/tags'
import { apiTokens } from './fixtures/tokens'

/** Vom Test erzwungene Fehlerantwort (`szenario.fehler`). */
export type ErzwungenerFehler = { pfad: string; methode?: string; status: number }

/** Veränderbarer Zustand des Mock-Servers. Handler lesen und schreiben nur hier. */
export type MockZustand = {
  /** Erster Admin angelegt? */
  eingerichtet: boolean
  /** Sitzung aktiv? */
  angemeldet: boolean
  /** Passwort geprüft, zweiter Faktor steht aus. */
  wartetAufTotp: boolean
  /** Benutzer der Sitzung (Rolle steuert 403). */
  benutzer: Benutzer
  passwort: string
  wiederherstellungscodes: string[]
  konto: Konto
  benutzerListe: Benutzer[]
  belege: BelegDetail[]
  historien: Record<string, AuditEintrag[]>
  ansichten: GespeicherteAnsicht[]
  korrespondenten: KorrespondentDatensatz[]
  tags: Tag[]
  kategorien: Kategorie[]
  autoMatching: AutoMatchingStatus[]
  artikel: ArtikelListeneintrag[]
  stammartikel: StammartikelListeneintrag[]
  preisDetails: Record<string, PreisDetail>
  importVorgaenge: ImportVorgang[]
  /** Im Mock hochgeladene Vorgänge – sie laufen bei jedem Abruf einen Schritt weiter. */
  simulierteVorgaenge: string[]
  dashboard: Dashboard
  systemStatus: SystemStatusEintrag[]
  backup: BackupStatus
  diVerbrauch: DiVerbrauch
  exportLaeufe: ExportLauf[]
  einstellungen: Einstellungen
  mailStatus: MailStatus
  benachrichtigungen: Benachrichtigung[]
  loeschbar: Loeschbar[]
  integritaetsLaeufe: IntegritaetsLauf[]
  tokens: ApiToken[]
  verfahrensdoku: Verfahrensdoku
  fehler: ErzwungenerFehler[]
  /** Fortlaufender Zähler für neue IDs. */
  naechsteNummer: number
}

function erzeuge(): MockZustand {
  return structuredClone({
    eingerichtet: true,
    angemeldet: true,
    wartetAufTotp: false,
    benutzer: angemeldeterBenutzer,
    passwort: mockZugang.passwort,
    // Das Konto zeigt, wie viele Codes noch übrig sind.
    wiederherstellungscodes: wiederherstellungscodes.codes.slice(0, konto.wiederherstellungscodes),
    konto,
    benutzerListe,
    belege,
    historien: belegHistorien,
    ansichten,
    korrespondenten,
    tags,
    kategorien,
    autoMatching: autoMatchingStatus,
    artikel: artikelListe,
    stammartikel: stammartikelListe,
    preisDetails,
    importVorgaenge,
    simulierteVorgaenge: [],
    dashboard,
    systemStatus,
    backup: backupStatus,
    diVerbrauch,
    exportLaeufe,
    einstellungen,
    mailStatus,
    benachrichtigungen,
    loeschbar,
    integritaetsLaeufe,
    tokens: apiTokens,
    verfahrensdoku,
    fehler: [],
    naechsteNummer: 1,
  })
}

/** Der Zustand. Das Objekt bleibt dasselbe; `zuruecksetzen()` ersetzt nur seinen Inhalt. */
export const db: MockZustand = erzeuge()

/** Stellt die Beispieldaten des Designs wieder her (nach jedem Test, siehe `server.ts`). */
export function zuruecksetzen(): void {
  Object.assign(db, erzeuge())
}

/** Neue, eindeutige UUID für im Mock angelegte Datensätze. */
export function neueId(): string {
  const nr = db.naechsteNummer++
  return `f0000000-0000-4000-8000-${String(nr).padStart(12, '0')}`
}
