// Erzeugt die Testdateien des E2E-Systems und die dazu passenden WireMock-Stubs für azure-mock.
// Aufruf (im Ordner e2e-system): node testdateien/erzeugen.mjs
//
// Gescannte PDFs tragen ein Merkmal im Info-Dictionary (unkomprimiert). Der Document-Intelligence-Client
// schickt die Datei als `base64Source` im JSON – deshalb erkennen die Stubs das Merkmal an seinen drei
// möglichen Base64-Darstellungen (je nach Lage im Byte-Strom).
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { PDFDocument, PDFName, PDFString, StandardFonts, AFRelationship } from 'pdf-lib'

const hier = dirname(fileURLToPath(import.meta.url))
const repo = join(hier, '..', '..')
const stubs = join(hier, '..', 'azure-mock', 'mappings')
mkdirSync(stubs, { recursive: true })

/** Feste Zeitstempel, damit das Skript bei jedem Lauf dieselben Bytes erzeugt. */
const zeit = new Date('2026-10-01T08:00:00Z')

async function pdf({ seiten, zeilen, merkmal, xml }) {
  const doc = await PDFDocument.create()
  doc.setCreationDate(zeit)
  doc.setModificationDate(zeit)
  doc.setProducer('biber-e2e')
  doc.setCreator('biber-e2e')
  const schrift = await doc.embedFont(StandardFonts.Helvetica)
  for (let s = 1; s <= seiten; s++) {
    const seite = doc.addPage([595, 842])
    const text = [...zeilen, seiten > 1 ? `Seite ${s} von ${seiten}` : '']
    text.forEach((zeile, i) => seite.drawText(zeile, { x: 50, y: 780 - i * 18, size: 12, font: schrift }))
  }
  if (merkmal) doc.getInfoDict().set(PDFName.of('E2EMerkmal'), PDFString.of(merkmal))
  if (xml) {
    await doc.attach(xml, 'factur-x.xml', {
      mimeType: 'text/xml',
      description: 'Factur-X',
      creationDate: zeit,
      modificationDate: zeit,
      afRelationship: AFRelationship.Alternative,
    })
  }
  return Buffer.from(await doc.save({ useObjectStreams: false }))
}

/** Die drei Base64-Teilstücke, von denen genau eines in der Base64-Darstellung der Datei vorkommt. */
function base64Varianten(merkmal) {
  const roh = Buffer.from(merkmal, 'latin1')
  return [0, 1, 2].map((versatz) => {
    const b64 = Buffer.concat([Buffer.alloc(versatz), roh]).toString('base64')
    const stueck = b64.slice(4, -4)
    // System.Text.Json maskiert „+“ (+); „/“ bleibt. Das Merkmal ist so gewählt, dass „+“ nicht vorkommt.
    if (stueck.includes('+')) throw new Error(`Merkmal ${merkmal} ergibt „+“ in Base64 – anderes Merkmal wählen.`)
    return stueck
  })
}

function analyseStubs(name, merkmal) {
  base64Varianten(merkmal).forEach((stueck, i) => {
    writeFileSync(
      join(stubs, `analyze-${name}-${i}.json`),
      JSON.stringify(
        {
          priority: 1,
          request: {
            method: 'POST',
            urlPath: '/documentintelligence/documentModels/prebuilt-invoice:analyze',
            bodyPatterns: [{ contains: stueck }],
          },
          response: {
            status: 202,
            headers: {
              'Operation-Location': `http://azure-mock:8080/documentintelligence/documentModels/prebuilt-invoice/analyzeResults/${name}?api-version=2024-11-30`,
              'Retry-After': '0',
            },
          },
        },
        null,
        2,
      ) + '\n',
    )
  })
}

/**
 * XRechnung aus den KoSIT-Testdateien. Die Daten stammen aus 2016; die Datumsprüfung verlangt höchstens
 * zwei Jahre alte Belege – deshalb ins Jahr 2026 verschoben.
 */
function xrechnung(datei, nummer) {
  const xml = readFileSync(join(repo, 'docker/kosit/test', datei), 'utf8')
    .replace(/<ram:ID>123456XX<\/ram:ID>/, `<ram:ID>${nummer}</ram:ID>`)
    .replace(/>2016(\d{4})</g, '>2026$1<')
  return Buffer.from(xml, 'utf8')
}

const kopf = ['Bürobedarf Schulz GmbH', 'Lindenstraße 12, 10115 Berlin', 'Rechnung']

const dateien = {
  'scan-stimmig.pdf': await pdf({
    seiten: 1,
    zeilen: [...kopf, 'Rechnung Nr. SCH-2026-0815 vom 15.10.2026', 'Druckerpapier 2 x 25,00', 'Toner 1 x 50,00'],
    merkmal: 'BIBERE2ESCANSTIMMIG000000000000',
  }),
  'scan-fehlerhaft.pdf': await pdf({
    seiten: 1,
    zeilen: [...kopf, 'Rechnung Nr. SCH-2026-0816 vom 16.10.2026', 'Ordner 4 x 10,00', 'Locher 1 x 18,00'],
    merkmal: 'BIBERE2ESCANFEHLERHAFT000000000',
  }),
  'scan-vier-seiten.pdf': await pdf({
    seiten: 4,
    zeilen: ['Sammelscan', 'Zwei Rechnungen in einer Datei'],
    merkmal: 'BIBERE2ESCANVIERSEITEN000000000',
  }),
  'zugferd.pdf': await pdf({
    seiten: 1,
    zeilen: ['Elektro Huber KG', 'Rechnung ZF-2026-0004'],
    xml: readFileSync(join(repo, 'backend/tests/Biber.Invoice.UnitTests/Testdateien/erechnung/zugferd-en16931.xml')),
  }),
  'xrechnung.xml': xrechnung('xrechnung-cii.xml', '123456XX'),
  // Eigene Rechnungsnummer, damit sie nicht als inhaltliche Dublette der stimmigen XRechnung gilt.
  'xrechnung-ohne-kaeuferreferenz.xml': xrechnung('xrechnung-ohne-kaeuferreferenz.xml', '123456YY'),
  // HEIC wird vom System nicht unterstützt; Kopf genügt für die Erkennung als Bild ohne Unterstützung.
  'foto.heic': Buffer.concat([
    Buffer.from([0, 0, 0, 0x18]),
    Buffer.from('ftypheic', 'latin1'),
    Buffer.from([0, 0, 0, 0]),
    Buffer.from('mif1heic', 'latin1'),
    Buffer.alloc(512, 0x42),
  ]),
}

for (const [name, inhalt] of Object.entries(dateien)) writeFileSync(join(hier, name), inhalt)

analyseStubs('stimmig', 'BIBERE2ESCANSTIMMIG000000000000')
analyseStubs('fehlerhaft', 'BIBERE2ESCANFEHLERHAFT000000000')
analyseStubs('vierseiten', 'BIBERE2ESCANVIERSEITEN000000000')

console.log(`Erzeugt: ${Object.keys(dateien).join(', ')} und Stubs in azure-mock/mappings`)
