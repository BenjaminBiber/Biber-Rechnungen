import type { BelegFilter, GespeicherteAnsicht } from '@/api/typen'
import { ansichtId, kategorieIds } from './ids'

/** Filter mit den Vorgaben des Backends (`BelegFilterDto`). */
export const standardFilter: BelegFilter = {
  suche: null,
  suchfeld: 'TitelInhalt',
  ansicht: 'Alle',
  tagIds: [],
  korrespondentIds: [],
  kategorieIds: [],
  von: null,
  bis: null,
  betragVon: null,
  betragBis: null,
  betragArt: 'Brutto',
  zahlstatus: null,
  quellen: [],
  dublettenAusschliessen: false,
  sortierung: 'Rechnungsdatum',
  absteigend: true,
  seite: 1,
  seitengroesse: 25,
}

/**
 * Gespeicherte Ansichten hinter dem Knopf „Ansicht“ in `Belege.dc.html`. Das Design zeigt keine Einträge;
 * zwei schlüssige Beispiele, damit Menü und Löschen testbar sind.
 */
export const ansichten: GespeicherteAnsicht[] = [
  {
    id: ansichtId(1),
    name: 'Offen 2026',
    filter: { ...standardFilter, ansicht: 'Offen', von: '2026-01-01', bis: '2026-12-31' },
    spalten: ['korrespondent', 'rechnungsnummer', 'rechnungsdatum', 'faelligkeit', 'brutto'],
    darstellung: 'tabelle',
  },
  {
    id: ansichtId(2),
    name: 'Software & Hosting',
    filter: { ...standardFilter, kategorieIds: [kategorieIds.softwareHosting] },
    spalten: ['korrespondent', 'rechnungsnummer', 'rechnungsdatum', 'kategorie', 'brutto'],
    darstellung: 'klein',
  },
]
