import AdmZip from 'adm-zip'

/** Zellwert einer gelesenen Mappe: Text, Zahl oder leer. */
export type Zelle = string | number | null

/** Blätter einer xlsx-Datei als Zeilen mit Zellwerten (Spalte A = Index 0). */
export type Mappe = Map<string, Zelle[][]>

// Elemente mit beliebigem Namensraum-Präfix: ClosedXML (OpenXML SDK) schreibt z. B. <x:sheet>, das kann exceljs nicht lesen.
const element = (name: string) => new RegExp(`<(?:\\w+:)?${name}\\b([^>]*?)(?:/>|>([\\s\\S]*?)</(?:\\w+:)?${name}>)`, 'g')

function attribut(attribute: string, name: string): string | undefined {
  return new RegExp(`(?:^|\\s)(?:\\w+:)?${name}="([^"]*)"`).exec(attribute)?.[1]
}

function entschluesseln(text: string): string {
  return text
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#(\d+);/g, (_, code: string) => String.fromCodePoint(Number(code)))
    .replace(/&amp;/g, '&')
}

/** Verketteter Text aller <t>-Elemente (Rich-Text-Läufe eingeschlossen). */
function text(inhalt: string): string {
  return [...inhalt.matchAll(element('t'))].map((t) => entschluesseln(t[2] ?? '')).join('')
}

function spaltenIndex(bezug: string): number {
  const buchstaben = /^[A-Z]+/.exec(bezug)?.[0] ?? 'A'
  return [...buchstaben].reduce((summe, b) => summe * 26 + b.charCodeAt(0) - 64, 0) - 1
}

function lesen(zip: AdmZip, pfad: string): string {
  const eintrag = zip.getEntry(pfad.replace(/^\//, ''))
  if (!eintrag) throw new Error(`${pfad} fehlt in der xlsx-Datei`)
  return eintrag.getData().toString('utf8')
}

export function xlsxLesen(inhalt: Buffer): Mappe {
  const zip = new AdmZip(inhalt)
  const geteilt = zip.getEntry('xl/sharedStrings.xml')
    ? [...lesen(zip, 'xl/sharedStrings.xml').matchAll(element('si'))].map((si) => text(si[2] ?? ''))
    : []
  const ziele = new Map(
    [...lesen(zip, 'xl/_rels/workbook.xml.rels').matchAll(element('Relationship'))].map((r) => [
      attribut(r[1], 'Id')!,
      attribut(r[1], 'Target')!,
    ]),
  )

  const mappe: Mappe = new Map()
  for (const blatt of lesen(zip, 'xl/workbook.xml').matchAll(element('sheet'))) {
    const name = entschluesseln(attribut(blatt[1], 'name')!)
    const ziel = ziele.get(attribut(blatt[1], 'id')!)!
    const xml = lesen(zip, ziel.startsWith('/') ? ziel : `xl/${ziel}`)
    const zeilen: Zelle[][] = []
    for (const zeile of xml.matchAll(element('row'))) {
      const werte: Zelle[] = []
      for (const zelle of (zeile[2] ?? '').matchAll(element('c'))) {
        const typ = attribut(zelle[1], 't')
        const roh = /<(?:\w+:)?v>([\s\S]*?)<\/(?:\w+:)?v>/.exec(zelle[2] ?? '')?.[1]
        let wert: Zelle = null
        if (typ === 's') wert = geteilt[Number(roh)]
        else if (typ === 'inlineStr') wert = text(zelle[2] ?? '')
        else if (typ === 'str' || typ === 'e') wert = roh === undefined ? null : entschluesseln(roh)
        else if (typ === 'b') wert = roh === undefined ? null : Number(roh)
        else if (roh !== undefined) wert = Number(roh)
        werte[spaltenIndex(attribut(zelle[1], 'r') ?? 'A')] = wert
      }
      zeilen[Number(attribut(zeile[1], 'r') ?? zeilen.length + 1) - 1] = Array.from(werte, (w) => w ?? null)
    }
    mappe.set(name, Array.from(zeilen, (z) => z ?? []))
  }
  return mappe
}
