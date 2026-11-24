import type { Benachrichtigung } from '@/api/typen'
import { belegIds, benachrichtigungId } from './ids'

/**
 * Glocke aus `Topbar.dc.html` (`renderVals().notes`): die ersten drei ungelesen, die letzten zwei gelesen.
 * Relative Zeiten („heute“, „gestern“) beziehen sich wie im Design auf den 23.11.2026; MEZ → UTC.
 */
export const benachrichtigungen: Benachrichtigung[] = [
  {
    id: benachrichtigungId(1),
    art: 'DiLimitWarnung',
    schweregrad: 'Warnung',
    titel: 'Document Intelligence bei 82 %',
    text: '412 von 500 Seiten im November verbraucht. Ab 100 % warten neue Belege in der Warteschlange.',
    link: '/einstellungen/document-intelligence',
    erstelltAm: '2026-11-23T06:45:00Z',
    gelesen: false,
  },
  {
    id: benachrichtigungId(2),
    art: 'KontoWartetAufFreigabe',
    schweregrad: 'Info',
    titel: 'Neues Konto wartet auf Freigabe',
    text: 'Jonas Wagner hat sich registriert.',
    link: '/einstellungen/benutzer',
    erstelltAm: '2026-11-22T17:42:00Z',
    gelesen: false,
  },
  {
    id: benachrichtigungId(3),
    art: 'ImportFehler',
    schweregrad: 'Fehler',
    titel: 'Import fehlgeschlagen',
    text: '„foto_beleg.heic“: Format nicht unterstützt.',
    link: '/hochladen',
    erstelltAm: '2026-11-22T10:05:00Z',
    gelesen: false,
  },
  {
    id: benachrichtigungId(4),
    art: 'KositFehler',
    schweregrad: 'Warnung',
    titel: 'KoSIT-Validierung fehlgeschlagen',
    text: 'XRechnung 2026-1187 (Stadtwerke Lindenau): BR-DE-15 Käuferreferenz fehlt.',
    link: `/belege/${belegIds.b5}/pruefung`,
    erstelltAm: '2026-11-14T08:30:00Z',
    gelesen: true,
  },
  {
    id: benachrichtigungId(5),
    art: 'IntegritaetOk',
    schweregrad: 'Ok',
    titel: 'Integritätsprüfung ohne Befund',
    text: '2.184 Dateien geprüft, alle Prüfsummen stimmen.',
    link: '/einstellungen/integritaet',
    erstelltAm: '2026-11-23T02:00:00Z',
    gelesen: true,
  },
]
