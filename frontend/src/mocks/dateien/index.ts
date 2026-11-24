/**
 * Kleine Beispieldateien für die Datei-Endpunkte des Mocks (Original-PDF, Vorschaubild, E-Rechnung).
 * Als Quelltext eingebettet, damit sie in Vitest (Node) und im Service Worker gleich funktionieren.
 */

/** Einseitige Beispiel-PDF mit dem Text „Beispielbeleg“. pdf.js baut die Querverweistabelle selbst auf. */
export const beispielPdf = `%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length 88 >>
stream
BT /F1 24 Tf 72 760 Td (Beispielbeleg) Tj ET
BT /F1 12 Tf 72 730 Td (Biber-Rechnungen Mock) Tj ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
trailer
<< /Root 1 0 R /Size 6 >>
%%EOF
`

/** Platzhalterbild 1 × 1 Pixel als PNG, Base64. */
const platzhalterPngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='

export function platzhalterPng(): Uint8Array<ArrayBuffer> {
  const binaer = atob(platzhalterPngBase64)
  const bytes = new Uint8Array(binaer.length)
  for (let i = 0; i < binaer.length; i++) bytes[i] = binaer.charCodeAt(i)
  return bytes
}

/** Eingebettete XRechnung (gekürzt). */
export function beispielXml(rechnungsnummer: string): string {
  return `<?xml version="1.0" encoding="UTF-8"?>
<ubl:Invoice xmlns:ubl="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
  <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0</cbc:CustomizationID>
  <cbc:ID>${rechnungsnummer}</cbc:ID>
</ubl:Invoice>
`
}

/** HTML-Visualisierung einer E-Rechnung (gekürzt). */
export function beispielVisualisierung(titel: string): string {
  return `<!doctype html><html lang="de"><head><meta charset="utf-8"><title>${titel}</title></head><body><h1>${titel}</h1><p>Visualisierung der E-Rechnung (Mock).</p></body></html>`
}

/** Platzhalter für ZIP-Exporte (leeres ZIP-Archiv). */
export function leeresZip(): Uint8Array<ArrayBuffer> {
  const bytes = new Uint8Array(22)
  bytes.set([0x50, 0x4b, 0x05, 0x06])
  return bytes
}
