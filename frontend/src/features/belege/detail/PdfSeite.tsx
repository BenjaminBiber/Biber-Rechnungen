import { Document, Page, pdfjs } from 'react-pdf'
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
import { Suspense } from 'react'

// Worker lokal gebündelt (Vite liefert ihn als eigene Datei aus), kein CDN.
pdfjs.GlobalWorkerOptions.workerSrc = workerUrl

/**
 * Nachgeladene Hilfsdateien (vite.config.ts → pdfjsDateien): WASM-Decoder für JPEG2000/JBIG2 (sonst fehlen
 * solche Bilder, oft der Briefkopf), CMaps und Standardschriften für PDFs ohne eingebettete Schriften.
 * Konstantes Objekt, damit <Document> das PDF nicht bei jedem Rendern neu lädt.
 */
const optionen = {
  wasmUrl: '/pdfjs/wasm/',
  cMapUrl: '/pdfjs/cmaps/',
  cMapPacked: true,
  standardFontDataUrl: '/pdfjs/standard_fonts/',
  iccUrl: '/pdfjs/iccs/',
}

/** Seitenverhältnis Höhe : Breite eines A4-Blatts. */
const A4 = 1.414

/** Eine Seite des Original-PDFs als Canvas (ohne Text- und Anmerkungsebene). */
export function PdfSeite({
  url,
  seite,
  breite,
  drehung,
  onSeiten,
}: {
  url: string
  seite: number
  breite: number
  drehung: number
  onSeiten: (anzahl: number) => void
}) {
  // Platz für ein A4-Blatt freihalten, damit die Seite beim Laden nicht springt.
  const hoehe = drehung % 180 === 0 ? breite * A4 : breite / A4
  const laden = <p className="gedaempft m-0 text-sm">PDF wird geladen …</p>
  return (
    <div style={{ minHeight: hoehe }}>
      {/* react-pdf suspendiert beim Laden (React `use`); ohne eigene Grenze blendet React die ganze Shell aus. */}
      <Suspense fallback={laden}>
        <Document
          file={url}
          options={optionen}
          onLoadSuccess={({ numPages }) => onSeiten(numPages)}
          loading={laden}
          error={<p className="m-0 text-sm text-rot-text">Das PDF konnte nicht angezeigt werden.</p>}
        >
          <Page
            pageNumber={seite}
            width={breite}
            rotate={drehung}
            renderTextLayer={false}
            renderAnnotationLayer={false}
            className="shadow-blatt"
          />
        </Document>
      </Suspense>
    </div>
  )
}
