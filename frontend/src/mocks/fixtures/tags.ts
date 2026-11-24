import type { AutoMatchingStatus, Kategorie, Matching, Tag, TagKurz } from '@/api/typen'
import { kategorieIds, tagIds } from './ids'

const keins: Matching = { algorithmus: 'Keiner', muster: null, grossKlein: false }

/**
 * Tags aus `TagsKategorien.dc.html` (Reiter „Tags (7)“). `farbe` ist der Chip-Ton aus
 * `src/components/Chip.tsx` (t-blue → blau usw.). `anzahlBelege` wie im Design.
 */
export const tags: Tag[] = [
  {
    id: tagIds.inbox,
    name: 'Inbox',
    farbe: 'blau',
    istInbox: true,
    istZahlungsTag: false,
    istSystem: false,
    matching: keins,
    anzahlBelege: 12,
  },
  {
    id: tagIds.pruef,
    name: 'zu prüfen',
    farbe: 'orange',
    istInbox: false,
    istZahlungsTag: false,
    istSystem: true,
    matching: keins,
    anzahlBelege: 5,
  },
  {
    id: tagIds.bez,
    name: 'bezahlt',
    farbe: 'gruen',
    istInbox: false,
    istZahlungsTag: true,
    istSystem: false,
    matching: keins,
    anzahlBelege: 214,
  },
  {
    id: tagIds.pp,
    name: 'bezahlt PayPal',
    farbe: 'petrol',
    istInbox: false,
    istZahlungsTag: true,
    istSystem: false,
    matching: { algorithmus: 'EinWort', muster: 'paypal', grossKlein: false },
    anzahlBelege: 37,
  },
  {
    id: tagIds.ls,
    name: 'Lastschrift',
    farbe: 'lila',
    istInbox: false,
    istZahlungsTag: true,
    istSystem: false,
    matching: { algorithmus: 'Regex', muster: 'lastschrift|sepa-mandat|wird .* abgebucht', grossKlein: false },
    anzahlBelege: 58,
  },
  {
    id: tagIds.afa,
    name: 'Anlage / AfA',
    farbe: 'grau',
    istInbox: false,
    istZahlungsTag: false,
    istSystem: false,
    matching: keins,
    anzahlBelege: 6,
  },
  {
    id: tagIds.reise,
    name: 'Reisekosten',
    farbe: 'rot',
    istInbox: false,
    istZahlungsTag: false,
    istSystem: false,
    matching: { algorithmus: 'AlleWoerter', muster: 'hotel übernachtung', grossKlein: false },
    anzahlBelege: 14,
  },
]

export function tagKurz(tag: Tag): TagKurz {
  const { id, name, farbe, istInbox, istZahlungsTag, istSystem } = tag
  return { id, name, farbe, istInbox, istZahlungsTag, istSystem }
}

/** Kategorien aus `TagsKategorien.dc.html` (Reiter „Kategorien (6)“), Summen 2026 wie im Design. */
export const kategorien: Kategorie[] = [
  {
    id: kategorieIds.softwareHosting,
    name: 'Software & Hosting',
    farbe: '#2F2D86',
    euerZeile: 'Übrige Betriebsausgaben',
    matching: { algorithmus: 'Auto', muster: null, grossKlein: false },
    anzahlBelege: 23,
    summeBrutto: 5188,
  },
  {
    id: kategorieIds.telekommunikation,
    name: 'Telekommunikation',
    farbe: '#5B8DEF',
    euerZeile: 'Telekommunikation',
    matching: { algorithmus: 'EinWort', muster: 'mobilfunk', grossKlein: false },
    anzahlBelege: 11,
    summeBrutto: 2033,
  },
  {
    id: kategorieIds.buerobedarf,
    name: 'Bürobedarf',
    farbe: '#E3893B',
    euerZeile: 'Bürobedarf',
    matching: { algorithmus: 'Auto', muster: null, grossKlein: false },
    anzahlBelege: 31,
    summeBrutto: 2223,
  },
  {
    id: kategorieIds.fahrzeug,
    name: 'Fahrzeug',
    farbe: '#1F8A70',
    euerZeile: 'Kfz-Kosten (Leasing)',
    matching: { algorithmus: 'Exakt', muster: 'Leasingrate', grossKlein: false },
    anzahlBelege: 22,
    summeBrutto: 5639,
  },
  {
    id: kategorieIds.hardware,
    name: 'Hardware',
    farbe: '#9AA4B5',
    euerZeile: 'GWG / AfA prüfen',
    matching: keins,
    anzahlBelege: 8,
    summeBrutto: 3808.5,
  },
  {
    id: kategorieIds.sonstiges,
    name: 'Sonstiges',
    farbe: '#CFCEEA',
    euerZeile: null,
    matching: keins,
    anzahlBelege: 41,
    summeBrutto: 2132.9,
  },
]

/** Stand des Auto-Matchings (Texte in `TagsKategorien.dc.html` und `KorrespondentDetail.dc.html`). */
export const autoMatchingStatus: AutoMatchingStatus[] = [
  { ziel: 'Korrespondent', trainiertAm: '2026-11-23T01:30:00Z', anzahlBeispiele: 248, guete: 0.96 },
  { ziel: 'Kategorie', trainiertAm: '2026-11-23T01:30:00Z', anzahlBeispiele: 54, guete: 0.96 },
  { ziel: 'Tag', trainiertAm: '2026-11-23T01:30:00Z', anzahlBeispiele: 0, guete: null },
]
