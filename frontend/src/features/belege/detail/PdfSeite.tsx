import { Document, Page, pdfjs } from 'react-pdf'
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'

// Worker lokal gebündelt (Vite liefert ihn als eigene Datei aus), kein CDN.
pdfjs.GlobalWorkerOptions.workerSrc = workerUrl

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
  return (
    <div style={{ minHeight: hoehe }}>
      <Document
        file={url}
        onLoadSuccess={({ numPages }) => onSeiten(numPages)}
        loading={<p className="gedaempft m-0 text-sm">PDF wird geladen …</p>}
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
    </div>
  )
}
