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

function xmlText(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

/** Feste, gut unterscheidbare Logofarben; die Wahl hängt nur vom Namen ab. */
const LOGO_FARBEN = ['#c2410c', '#1d4ed8', '#047857', '#7c3aed', '#0f766e', '#b91c1c', '#a16207']

/** Beispiel-Logo als SVG: farbige Marke mit Initialen und daneben der Name (wie ein Briefkopf-Ausschnitt). */
export function beispielLogoSvg(initialen: string, name: string): string {
  let summe = 0
  for (const zeichen of name) summe = (summe * 31 + zeichen.charCodeAt(0)) >>> 0
  const farbe = LOGO_FARBEN[summe % LOGO_FARBEN.length]
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" width="120" height="120">
<rect x="10" y="10" width="100" height="100" rx="22" fill="${farbe}"/>
<text x="60" y="60" dy=".35em" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="44" font-weight="700" fill="#fff">${xmlText(initialen)}</text>
</svg>`
}

/** Beispiel-Seite (DIN A4) als SVG für Vorschaubilder und Bild-Originale. */
export function beispielSeiteSvg({
  absender,
  titel,
  nummer,
  betrag,
}: {
  absender: string
  titel: string
  nummer: string
  betrag: string
}): string {
  const zeilen = [0, 1, 2, 3, 4]
    .map(
      (i) =>
        `<rect x="60" y="${330 + i * 34}" width="${300 - i * 28}" height="10" rx="3" fill="#d4d7de"/><rect x="465" y="${330 + i * 34}" width="70" height="10" rx="3" fill="#d4d7de"/>`,
    )
    .join('')
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 595 842" width="595" height="842">
<rect width="595" height="842" fill="#fff"/>
<g font-family="Arial, Helvetica, sans-serif" fill="#1f2430">
<text x="60" y="90" font-size="22" font-weight="700">${xmlText(absender)}</text>
<text x="60" y="118" font-size="12" fill="#6b7280">Biber-Rechnungen Mock</text>
<text x="60" y="230" font-size="26" font-weight="700">Rechnung</text>
<text x="60" y="258" font-size="13" fill="#4b5563">${xmlText(titel)}</text>
<text x="535" y="230" font-size="12" text-anchor="end" fill="#4b5563">Nr. ${xmlText(nummer)}</text>
<line x1="60" y1="300" x2="535" y2="300" stroke="#1f2430" stroke-width="1.5"/>
${zeilen}
<line x1="320" y1="520" x2="535" y2="520" stroke="#1f2430" stroke-width="1.5"/>
<text x="320" y="548" font-size="15" font-weight="700">Gesamt</text>
<text x="535" y="548" font-size="15" font-weight="700" text-anchor="end">${xmlText(betrag)}</text>
<line x1="60" y1="780" x2="535" y2="780" stroke="#d4d7de"/>
</g>
</svg>`
}
