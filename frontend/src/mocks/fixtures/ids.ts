/**
 * Feste IDs der Beispieldaten. Die Schlüssel entsprechen den Kürzeln in den `renderVals()` des
 * Designs (`b1` = erster Beleg in `Belege.dc.html` usw.), damit Fixtures aufeinander verweisen können.
 */
function uuid(praefix: string, nr: number): string {
  return `${praefix.padEnd(8, '0')}-0000-4000-8000-${String(nr).padStart(12, '0')}`
}

function reihe<K extends string>(praefix: string, schluessel: readonly K[]): Record<K, string> {
  return Object.fromEntries(schluessel.map((k, i) => [k, uuid(praefix, i + 1)])) as Record<K, string>
}

/** Belege b1–b12 aus `Belege.dc.html`. */
export const belegIds = reihe('b0', [
  'b1',
  'b2',
  'b3',
  'b4',
  'b5',
  'b6',
  'b7',
  'b8',
  'b9',
  'b10',
  'b11',
  'b12',
] as const)

/** Korrespondenten k1–k10 aus `Korrespondenten.dc.html`. */
export const korrespondentIds = reihe('c0', [
  'autohausBrenner',
  'hostwerkSued',
  'hardwareversandNord',
  'bueroweltKraemer',
  'netzfunkTelekom',
  'kanzleiHofmann',
  'druckereiWeller',
  'stadtwerkeLindenau',
  'elektroHaas',
  'bueroweltKraemerDublette',
] as const)

/** Tags aus `TagsKategorien.dc.html`. */
export const tagIds = reihe('a0', ['inbox', 'pruef', 'bez', 'pp', 'ls', 'afa', 'reise'] as const)

/** Kategorien k1–k6 aus `TagsKategorien.dc.html`. */
export const kategorieIds = reihe('ca', [
  'softwareHosting',
  'telekommunikation',
  'buerobedarf',
  'fahrzeug',
  'hardware',
  'sonstiges',
] as const)

/** Artikel a1–a9 aus `Artikel.dc.html`. */
export const artikelIds = reihe('a1', ['a1', 'a2', 'a3', 'a4', 'a5', 'a6', 'a7', 'a8', 'a9'] as const)

/** Stammartikel aus `Artikel.dc.html` (Reiter „Stammartikel“). */
export const stammartikelIds = reihe('5a', [
  'kopierpapier',
  'tonerSchwarz',
  'managedServer',
  'mobilfunkvertrag',
] as const)

/** Benutzer (Einstellungen → Benutzer); Martin Keller ist angemeldet. */
export const benutzerIds = reihe('be', ['martinKeller', 'benutzer2', 'benutzer3', 'benutzer4'] as const)

export function importVorgangId(nr: number): string {
  return uuid('1a', nr)
}

export function benachrichtigungId(nr: number): string {
  return uuid('bc', nr)
}

export function tokenId(nr: number): string {
  return uuid('70', nr)
}

export function exportLaufId(nr: number): string {
  return uuid('e0', nr)
}

export function integritaetsLaufId(nr: number): string {
  return uuid('1b', nr)
}

export function ansichtId(nr: number): string {
  return uuid('a5', nr)
}

/** Datei-IDs je Beleg: Original = 1, eingebettetes XML = 2, Visualisierung = 3. */
export function dateiId(belegNr: number, art: number): string {
  return uuid('da', belegNr * 10 + art)
}
