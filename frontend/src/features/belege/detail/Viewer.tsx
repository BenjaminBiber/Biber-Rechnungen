import { useEffect, useEffectEvent, useState } from 'react'
import { belegDateiUrl, belegVisualisierungUrl } from '@/api/belege'
import type { BelegDetail, ExtraktionsQuelle } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Seitenpanel } from '@/components/shell/Seitenpanel'
import { formatDatum, formatDatumZeit, formatEuro, formatZahl } from '@/lib/format'
import { istPdf, istXml, original } from './gemeinsam'
import { PdfSeite } from './PdfSeite'

const ZOOM_MIN = 50
const ZOOM_MAX = 300
const ZOOM_SCHRITT = 25
/** Höchstbreite der Seite bei 100 %; sonst füllt sie den Anzeigebereich. */
const BLATT_BREITE = 820

const quellen: Record<ExtraktionsQuelle, string> = {
  DocumentIntelligence: 'Document Intelligence',
  XRechnung: 'XRechnung',
  ZUGFeRD: 'ZUGFeRD',
  Manuell: 'Manuell',
}

const knopf =
  'inline-flex size-8 cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-gedaempft-stark no-underline hover:bg-hover disabled:cursor-not-allowed disabled:opacity-40'

/**
 * Anzeigebereich der Seite: seine Breite (ohne ResizeObserver, z. B. in jsdom, die Blattbreite) und
 * Zoomen mit Umschalt- oder Strg/⌘-Taste + Mausrad, solange der Mauszeiger darüber steht.
 * Als Callback-Ref, damit beides nach dem Umschalten von „Erkannte Felder“ zurück wieder greift.
 */
function useAnzeigebereich(onZoom: (faktor: number) => void) {
  const [element, setElement] = useState<HTMLDivElement | null>(null)
  const [breite, setBreite] = useState(BLATT_BREITE)

  useEffect(() => {
    if (!element || typeof ResizeObserver === 'undefined') return
    const beobachter = new ResizeObserver(([eintrag]) => setBreite(eintrag.contentRect.width))
    beobachter.observe(element)
    return () => beobachter.disconnect()
  }, [element])

  const beiRad = useEffectEvent((e: WheelEvent) => {
    if (!e.shiftKey && !e.ctrlKey && !e.metaKey) return
    e.preventDefault()
    // Mit Umschalt melden manche Browser das Mausrad als waagerechtes Scrollen (deltaX).
    const delta = e.deltaY !== 0 ? e.deltaY : e.deltaX
    onZoom(Math.exp(-delta * (e.deltaMode === 1 ? 0.05 : 0.0025)))
  })
  useEffect(() => {
    if (!element) return
    const horcher = (e: WheelEvent) => beiRad(e)
    element.addEventListener('wheel', horcher, { passive: false })
    return () => element.removeEventListener('wheel', horcher)
  }, [element])

  return { ref: setElement, breite }
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
    <div className="relative min-h-0 flex-1 overflow-auto">
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

/**
 * Bild-Original (Foto, Scan) in seinem echten Seitenverhältnis, auf `breite` eingepasst.
 * Gedreht belegt es den gedrehten Platz, statt über den Rand zu laufen und abgeschnitten zu werden.
 */
function BildSeite({ url, alt, breite, drehung }: { url: string; alt: string; breite: number; drehung: number }) {
  // Breite/Höhe des Bildes; bis es geladen ist DIN A4 hochkant.
  const [verhaeltnis, setVerhaeltnis] = useState(Math.SQRT1_2)
  const [fehler, setFehler] = useState(false)
  if (fehler) return <p className="gedaempft m-auto text-sm">Das Bild konnte nicht geladen werden.</p>

  const quer = drehung % 180 !== 0
  const bildBreite = quer ? breite * verhaeltnis : breite
  const bildHoehe = quer ? breite : breite / verhaeltnis
  return (
    <div className="relative m-auto flex-none" style={{ width: breite, height: quer ? bildBreite : bildHoehe }}>
      <img
        src={url}
        alt={alt}
        draggable={false}
        onLoad={(e) => {
          const { naturalWidth: b, naturalHeight: h } = e.currentTarget
          if (b > 0 && h > 0) setVerhaeltnis(b / h)
        }}
        onError={() => setFehler(true)}
        className="absolute top-1/2 left-1/2 max-w-none bg-white shadow-blatt select-none"
        style={{
          width: bildBreite,
          height: bildHoehe,
          transform: `translate(-50%, -50%) rotate(${drehung}deg)`,
        }}
      />
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
    <div className="flex flex-none items-center gap-2 border-t border-rand pt-3 text-xs text-gedaempft-stark">
      <Icon
        name={datei?.pruefsummeOk === false ? 'gpp_bad' : 'lock'}
        className={`text-[18px] ${datei?.pruefsummeOk === false ? 'text-rot-text' : 'text-gruen-text'}`}
      />
      Unveränderbares Original · {pruefung} · Löschsperre bis {formatDatum(beleg.loeschsperreBis)}
    </div>
  )
}

/**
 * Rechte Spalte „Original“ – wie die Vorschau der Belegliste ein Panel unter der Kopfleiste bis zum Fensterrand:
 * PDF, Bild oder Visualisierung der E-Rechnung bzw. die erkannten Felder.
 */
export function Viewer({
  beleg,
  offen,
  onSchliessen,
}: {
  beleg: BelegDetail
  offen: boolean
  onSchliessen: () => void
}) {
  const datei = original(beleg)
  const nurXml = istXml(datei)
  const pdf = istPdf(datei)
  const bild = !!datei?.mimeType.startsWith('image/')
  const [modus, setModus] = useState<'original' | 'felder'>('original')
  const [seite, setSeite] = useState(1)
  const [seiten, setSeiten] = useState(Math.max(1, beleg.seitenanzahl))
  const [zoom, setZoom] = useState(100)
  const [drehung, setDrehung] = useState(0)
  const { ref, breite } = useAnzeigebereich((faktor) =>
    setZoom((z) => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, z * faktor))),
  )

  const blattBreite = Math.max(120, Math.min(BLATT_BREITE, breite - 32)) * (zoom / 100)
  const blaetterbar = modus === 'original' && (pdf || bild)
  const dateiname = datei?.pfad.split(/[/\\]/).pop()

  return (
    <Seitenpanel
      spalte
      offen={offen}
      // Laptop-Breiten: Titel nur für Screenreader, sichtbar bleibt der Dateiname.
      titel={<span className="max-2xl:sr-only">Original</span>}
      beschreibung={dateiname}
      onSchliessen={onSchliessen}
      aktionen={
        <div className="flex flex-none items-center text-[13px]">
          {blaetterbar && (
            <>
              <button
                type="button"
                className={knopf}
                aria-label="Verkleinern"
                disabled={zoom <= ZOOM_MIN}
                onClick={() => setZoom((z) => Math.max(ZOOM_MIN, z - ZOOM_SCHRITT))}
              >
                <Icon name="zoom_out" />
              </button>
              <span className="min-w-[44px] text-center tabular-nums" aria-label="Zoom">
                {Math.round(zoom)} %
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
          {/* Herunterladen steht im Menü „Weitere Aktionen“ des Kopfs. */}
          {datei && (
            <a
              href={nurXml ? belegVisualisierungUrl(beleg.id) : belegDateiUrl(beleg.id, datei.id, true)}
              target="_blank"
              rel="noreferrer"
              className={knopf}
              aria-label="In neuem Tab öffnen"
            >
              <Icon name="open_in_new" />
            </a>
          )}
        </div>
      }
    >
      <section aria-label="Original" className="flex min-h-0 flex-1 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
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
            <div className="flex items-center gap-1">
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
            </div>
          )}
        </div>

        {modus === 'felder' ? (
          <ErkannteFelder beleg={beleg} />
        ) : (
          <div
            ref={ref}
            data-testid="anzeigebereich"
            className="min-h-0 flex-1 overflow-auto rounded-lg bg-segment"
            data-drehung={drehung}
          >
            {/* w-max + m-auto: zentriert, solange die Seite passt; gezoomt bleibt sie nach allen Seiten scrollbar. */}
            <div className={`box-border flex p-4 ${nurXml ? 'h-full' : 'min-h-full w-max min-w-full'}`}>
              {!datei ? (
                <p className="gedaempft m-auto text-sm">Kein Original vorhanden.</p>
              ) : nurXml ? (
                <iframe
                  title="Visualisierung der E-Rechnung"
                  src={belegVisualisierungUrl(beleg.id)}
                  sandbox=""
                  className="h-full min-h-[480px] w-full border-0 bg-karte"
                />
              ) : pdf ? (
                <div className="m-auto">
                  <PdfSeite
                    url={belegDateiUrl(beleg.id, datei.id, true)}
                    seite={seite}
                    breite={blattBreite}
                    drehung={drehung}
                    onSeiten={setSeiten}
                  />
                </div>
              ) : bild ? (
                <BildSeite
                  url={belegDateiUrl(beleg.id, datei.id, true)}
                  alt={`Original ${beleg.rechnungsnummer ?? beleg.titel}`}
                  breite={blattBreite}
                  drehung={drehung}
                />
              ) : (
                <p className="gedaempft m-auto text-sm">Für dieses Format gibt es keine Vorschau.</p>
              )}
            </div>
          </div>
        )}
        <Fusszeile beleg={beleg} />
      </section>
    </Seitenpanel>
  )
}
