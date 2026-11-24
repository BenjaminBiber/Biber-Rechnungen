import type { Rolle } from '@/api/typen'
import { db } from './db'

/**
 * Schaltbare Zustände des Mock-Servers für Tests. Gelten bis zum Ende des Tests
 * (`zuruecksetzen()` nach jedem Test, siehe `server.ts`).
 *
 * @example
 * szenario.rolle('NurLesen')
 * szenario.fehler('/api/belege/{id}', 500)
 */
export const szenario = {
  /** Noch kein Admin angelegt: Status „nicht eingerichtet“, Ersteinrichtung möglich. */
  nichtEingerichtet(): void {
    db.eingerichtet = false
    db.angemeldet = false
    db.benutzerListe = []
  },

  /** Keine Sitzung: geschützte Endpunkte antworten mit 401. */
  abgemeldet(): void {
    db.angemeldet = false
    db.wartetAufTotp = false
  },

  /** Angemeldeter Benutzer mit anderer Rolle (403 bei fehlendem Recht). */
  rolle(rolle: Rolle): void {
    db.benutzer = { ...db.benutzer, rolle }
    db.benutzerListe = db.benutzerListe.map((b) => (b.id === db.benutzer.id ? db.benutzer : b))
  },

  /** Leeres Archiv: keine Belege, Korrespondenten, Artikel, Vorgänge, Exporte, Benachrichtigungen. */
  leer(): void {
    db.belege = []
    db.historien = {}
    db.ansichten = []
    db.korrespondenten = []
    db.tags = db.tags.filter((t) => t.istSystem || t.istInbox).map((t) => ({ ...t, anzahlBelege: 0 }))
    db.kategorien = []
    db.artikel = []
    db.stammartikel = []
    db.preisDetails = {}
    db.importVorgaenge = []
    db.exportLaeufe = []
    db.benachrichtigungen = []
    db.integritaetsLaeufe = []
    db.tokens = []
    db.dashboard = {
      ...db.dashboard,
      kosten: { anzahl: 0, betrag: 0 },
      kostenJahr: { anzahl: 0, betrag: 0 },
      offen: { anzahl: 0, betrag: 0 },
      ueberfaellig: { anzahl: 0, betrag: 0 },
      inbox: { anzahl: 0 },
      zuPruefen: { anzahl: 0 },
      kostenProMonat: db.dashboard.kostenProMonat.map((m) => ({ ...m, summe: 0, summeVorjahr: 0, nachKategorie: [] })),
      topKorrespondenten: [],
      zuPruefenListe: [],
      offeneBelege: [],
      preisaenderungen: [],
    }
  },

  /**
   * Erzwingt eine Fehlerantwort (ProblemDetails) für einen Endpunkt.
   * `pfad` wie im Vertrag (`/api/belege/{id}`); ohne `methode` gilt es für alle Methoden.
   */
  fehler(pfad: string, status: number, methode?: 'GET' | 'POST' | 'PUT' | 'DELETE'): void {
    db.fehler.push({ pfad, status, methode })
  },
}
