import { useEffect, useRef, useState } from 'react'
import { belegDateiUrl, belegVisualisierungUrl } from '@/api/belege'
import type { BelegDetail, ExtraktionsQuelle } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatDatum, formatDatumZeit, formatEuro, formatZahl } from '@/lib/format'
import { istPdf, istXml, original } from './gemeinsam'
import { PdfSeite } from './PdfSeite'

const ZOOM_MIN = 50
const ZOOM_MAX = 300
const ZOOM_SCHRITT = 25
/** Breite der Seite bei 100 % (wie das Blatt im Design). */
const BLATT_BREITE = 560

const quellen: Record<ExtraktionsQuelle, string> = {
  DocumentIntelligence: 'Document Intelligence',
  XRechnung: 'XRechnung',
  ZUGFeRD: 'ZUGFeRD',
  Manuell: 'Manuell',
}

const knopf =
  'inline-flex size-8 cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-gedaempft-stark no-underline hover:bg-hover disabled:cursor-not-allowed disabled:opacity-40'

/** Breite des Anzeigebereichs; ohne ResizeObserver (jsdom) die Blattbreite. */
function useBreite() {
  const ref = useRef<HTMLDivElement>(null)
  const [breite, setBreite] = useState(BLATT_BREITE)
  useEffect(() => {
    const element = ref.current
    if (!element || typeof ResizeObserver === 'undefined') return
    const beobachter = new ResizeObserver(([eintrag]) => setBreite(eintrag.contentRect.width))
    beobachter.observe(element)
    return () => beobachter.disconnect()
  }, [])
  return { ref, breite }
}

/** „Erkannte Felder“: ausgelesene Kopfdaten mit Quelle und Konfidenz. */
function ErkannteFelder({ beleg }: { beleg: BelegDetail }) {
  const datum = (iso: string | null | undefined) => (iso ? formatDatum(iso) : null)
  const betrag = (wert: number | null | undefined) =>
    wert === null || wert === undefined ? null : formatEuro(wert, beleg.waehrung)
  const felder: [string, string | null | undefined][] = [
    ['Korrespondent', beleg.korrespondent?.name],
    ['Rechnungsnummer', beleg.rechnungsnummer],
    ['Rechnungsdatum', datum(beleg.rechnungsdatum)],
    [
      'Leistungsdatum',
      beleg.leistungVon && beleg.leistungBis
        ? `${formatDatum(beleg.leistungVon)} – ${formatDatum(beleg.leistungBis)}`
        : datum(beleg.leistungsdatum),
    ],
    ['Fällig am', datum(beleg.faelligkeitsdatum)],
    ['Netto', betrag(beleg.netto)],
    ['USt', betrag(beleg.steuer)],
    ['Brutto', betrag(beleg.brutto)],
  ]
  const quelle = quellen[beleg.extraktion.quelle]
  const konfidenz = beleg.extraktion.konfidenz ?? beleg.konfidenz
  return (
    <div className="relative overflow-x-auto p-4">
      <table className="tabelle" aria-label="Erkannte Felder">
        <thead>
          <tr>
            <th scope="col">Feld</th>
            <th scope="col">Wert</th>
            <th scope="col">Quelle</th>
            <th scope="col" className="num">
              Konfidenz
            </th>
          </tr>
        </thead>
        <tbody>
          {felder.map(([name, wert]) => (
            <tr key={name}>
              <td className="gedaempft">{name}</td>
              <td>{wert || '—'}</td>
              <td>{wert ? quelle : '—'}</td>
              <td className="num">
                {wert && konfidenz !== null && konfidenz !== undefined ? formatZahl(konfidenz, 2) : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Fusszeile({ beleg }: { beleg: BelegDetail }) {
  const datei = original(beleg)
  const pruefung =
    datei?.pruefsummeOk === false
      ? 'SHA-256 weicht ab'
      : datei?.letztePruefungAm
        ? `SHA-256 geprüft ${formatDatumZeit(datei.letztePruefungAm)}`
        : 'SHA-256 noch nicht geprüft'
  return (
    <div className="flex items-center gap-2 border-t border-rand px-3 py-2.5 text-xs text-gedaempft-stark">
      <Icon
        name={datei?.pruefsummeOk === false ? 'gpp_bad' : 'lock'}
        className={`text-[18px] ${datei?.pruefsummeOk === false ? 'text-rot-text' : 'text-gruen-text'}`}
      />
      Unveränderbares Original · {pruefung} · Löschsperre bis {formatDatum(beleg.loeschsperreBis)}
    </div>
  )
}

/** Linke Karte „Original“: PDF, Bild oder Visualisierung der E-Rechnung bzw. die erkannten Felder. */
export function Viewer({ beleg }: { beleg: BelegDetail }) {
  const datei = original(beleg)
  const nurXml = istXml(datei)
  const pdf = istPdf(datei)
  const bild = !!datei?.mimeType.startsWith('image/')
  const [modus, setModus] = useState<'original' | 'felder'>('original')
  const [seite, setSeite] = useState(1)
  const [seiten, setSeiten] = useState(Math.max(1, beleg.seitenanzahl))
  const [zoom, setZoom] = useState(100)
  const [drehung, setDrehung] = useState(0)
  const { ref, breite } = useBreite()

  const blattBreite = Math.max(120, Math.min(BLATT_BREITE, breite - 48)) * (zoom / 100)
  const blaetterbar = modus === 'original' && (pdf || bild)

  return (
    <section aria-label="Original" className="karte min-w-0 overflow-hidden lg:flex-[1_1_400px]">
      <div className="flex flex-wrap items-center gap-2 border-b border-rand px-3 py-2">
        <div className="segment" role="group" aria-label="Ansicht">
          <button type="button" aria-pressed={modus === 'original'} onClick={() => setModus('original')}>
            {nurXml ? 'Visualisierung' : 'Original (PDF)'}
          </button>
          <button type="button" aria-pressed={modus === 'felder'} onClick={() => setModus('felder')}>
            Erkannte Felder
          </button>
        </div>
        <span className="flex-1" />
        {blaetterbar && (
          <>
            {pdf && seiten > 1 && (
              <button
                type="button"
                className={knopf}
                aria-label="Vorherige Seite"
                disabled={seite <= 1}
                onClick={() => setSeite((s) => s - 1)}
              >
                <Icon name="chevron_left" />
              </button>
            )}
            <span className="gedaempft text-[13px]">
              Seite {seite} / {pdf ? seiten : 1}
            </span>
            {pdf && seiten > 1 && (
              <button
                type="button"
                className={knopf}
                aria-label="Nächste Seite"
                disabled={seite >= seiten}
                onClick={() => setSeite((s) => s + 1)}
              >
                <Icon name="chevron_right" />
              </button>
            )}
            <button
              type="button"
              className={knopf}
              aria-label="Verkleinern"
              disabled={zoom <= ZOOM_MIN}
              onClick={() => setZoom((z) => Math.max(ZOOM_MIN, z - ZOOM_SCHRITT))}
            >
              <Icon name="zoom_out" />
            </button>
            <span className="text-[13px]" aria-label="Zoom">
              {zoom} %
            </span>
            <button
              type="button"
              className={knopf}
              aria-label="Vergrößern"
              disabled={zoom >= ZOOM_MAX}
              onClick={() => setZoom((z) => Math.min(ZOOM_MAX, z + ZOOM_SCHRITT))}
            >
              <Icon name="zoom_in" />
            </button>
            <button
              type="button"
              className={knopf}
              aria-label="Drehen"
              onClick={() => setDrehung((d) => (d + 90) % 360)}
            >
              <Icon name="rotate_right" />
            </button>
          </>
        )}
        {datei && (
          <>
            <a href={belegDateiUrl(beleg.id, datei.id)} download className={knopf} aria-label="Original herunterladen">
              <Icon name="download" />
            </a>
            <a
              href={nurXml ? belegVisualisierungUrl(beleg.id) : belegDateiUrl(beleg.id, datei.id, true)}
              target="_blank"
              rel="noreferrer"
              className={knopf}
              aria-label="In neuem Tab öffnen"
            >
              <Icon name="open_in_new" />
            </a>
          </>
        )}
      </div>

      {modus === 'felder' ? (
        <ErkannteFelder beleg={beleg} />
      ) : (
        <div ref={ref} className="flex justify-center overflow-auto bg-segment p-6" data-drehung={drehung}>
          {!datei ? (
            <p className="gedaempft m-0 text-sm">Kein Original vorhanden.</p>
          ) : nurXml ? (
            <iframe
              title="Visualisierung der E-Rechnung"
              src={belegVisualisierungUrl(beleg.id)}
              sandbox=""
              className="h-[720px] w-full border-0 bg-karte"
            />
          ) : pdf ? (
            <PdfSeite
              url={belegDateiUrl(beleg.id, datei.id, true)}
              seite={seite}
              breite={blattBreite}
              drehung={drehung}
              onSeiten={setSeiten}
            />
          ) : bild ? (
            <img
              src={belegDateiUrl(beleg.id, datei.id, true)}
              alt={`Original ${beleg.rechnungsnummer ?? beleg.titel}`}
              className="shadow-blatt"
              style={{ width: blattBreite, transform: `rotate(${drehung}deg)` }}
            />
          ) : (
            <p className="gedaempft m-0 text-sm">Für dieses Format gibt es keine Vorschau.</p>
          )}
        </div>
      )}
      <Fusszeile beleg={beleg} />
    </section>
  )
}
