import { Link } from '@tanstack/react-router'
import { useEffect, useEffectEvent, useLayoutEffect, useRef, useState, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { belegVorschauUrl } from '@/api/belege'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Seitenpanel } from '@/components/shell/Seitenpanel'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'

type VorschauProps = {
  beleg: BelegListeneintrag | null
  onSchliessen: () => void
}

function titelVon(beleg: BelegListeneintrag) {
  return [beleg.korrespondent?.name, beleg.titel].filter(Boolean).join(': ')
}

const knopf =
  'inline-flex size-8 flex-none cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-gedaempft-stark no-underline hover:bg-hover hover:text-gedaempft-stark disabled:cursor-not-allowed disabled:opacity-40'

/**
 * Desktop: große Vorschau als Panel von rechts unter der Kopfleiste; die Liste rückt dafür zur Seite.
 * Ein Klick auf eine andere Vorschau tauscht den Inhalt.
 */
export function VorschauPanel({ beleg, onSchliessen }: VorschauProps) {
  // Der zuletzt gezeigte Beleg bleibt stehen, bis das Panel ausgefahren ist.
  const [angezeigt, setAngezeigt] = useState(beleg)
  if (beleg && beleg !== angezeigt) setAngezeigt(beleg)
  // Die Zoom-Werkzeuge stehen in der Kopfzeile des Panels neben „Beleg öffnen“ und „Schließen“.
  const [werkzeugZiel, setWerkzeugZiel] = useState<HTMLDivElement | null>(null)

  return (
    <Seitenpanel
      offen={beleg !== null}
      titel="Vorschau"
      beschreibung={angezeigt ? titelVon(angezeigt) : undefined}
      aktionen={
        <>
          <div ref={setWerkzeugZiel} className="flex flex-none items-center" />
          {angezeigt && (
            <Link
              to="/belege/$belegId"
              params={{ belegId: angezeigt.id }}
              className={knopf}
              aria-label="Beleg öffnen"
              title="Beleg öffnen"
            >
              <Icon name="open_in_full" />
            </Link>
          )}
        </>
      }
      onSchliessen={onSchliessen}
      onGeschlossen={() => setAngezeigt(null)}
    >
      {angezeigt && <VorschauSeiten key={angezeigt.id} beleg={angezeigt} werkzeugZiel={werkzeugZiel} />}
    </Seitenpanel>
  )
}

/** Mobil: große Vorschau als Dialog. */
export function VorschauDialog({ beleg, onSchliessen }: VorschauProps) {
  return (
    <Dialog open={beleg !== null} onOpenChange={(offen) => !offen && onSchliessen()}>
      {beleg && (
        <DialogContent key={beleg.id} className="max-w-[760px]">
          <DialogHeader>
            <DialogTitle>Vorschau</DialogTitle>
            <DialogDescription>{titelVon(beleg)}</DialogDescription>
          </DialogHeader>
          <VorschauSeiten beleg={beleg} bereichKlasse="h-[65vh] flex-none" />
        </DialogContent>
      )}
    </Dialog>
  )
}

/* ---------------- Seitenansicht mit Zoom, Drehen und Verschieben ---------------- */

/** Zoom in Prozent der eingepassten Größe (100 % = ganze Seite sichtbar). */
const ZOOM_MIN = 50
const ZOOM_MAX = 500
const ZOOM_STUFEN = [50, 75, 100, 125, 150, 200, 300, 400, 500]
/** Innenabstand des Anzeigebereichs (p-4). */
const RAND = 16
/** Seitenverhältnis (Breite/Höhe) bis das Bild geladen ist: DIN A4 hochkant. */
const A4 = Math.SQRT1_2

/**
 * Renderbreiten, die beim Server angefragt werden. Wenige feste Stufen, damit dessen Zwischenspeicher greift;
 * 1600 ist das Maximum des Servers.
 */
const BILD_BREITEN = [600, 900, 1200, 1600]
const bildBreiteFuer = (px: number) => BILD_BREITEN.find((b) => b >= px) ?? BILD_BREITEN[BILD_BREITEN.length - 1]

const begrenzt = (z: number) => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, z))
const stufeHoch = (z: number) => ZOOM_STUFEN.find((s) => s > z + 0.5) ?? ZOOM_MAX
const stufeRunter = (z: number) => [...ZOOM_STUFEN].reverse().find((s) => s < z - 0.5) ?? ZOOM_MIN

/** Innenmaße eines Elements; ohne ResizeObserver (jsdom) eine feste Annahme. */
function useInnenmasse(ref: RefObject<HTMLElement | null>) {
  const [masse, setMasse] = useState({ breite: 440, hoehe: 620 })
  useEffect(() => {
    const element = ref.current
    if (!element || typeof ResizeObserver === 'undefined') return
    const beobachter = new ResizeObserver(() => setMasse({ breite: element.clientWidth, hoehe: element.clientHeight }))
    beobachter.observe(element)
    return () => beobachter.disconnect()
  }, [ref])
  return masse
}

/**
 * Seitenbild mit Werkzeugleiste: Blättern, Zoom (Knöpfe, Umschalt oder Strg/⌘ + Mausrad, Doppelklick, Tasten + − 0),
 * Einpassen und Drehen. Ist die Seite größer als der Bereich, lässt sie sich mit der Maus verschieben.
 */
function VorschauSeiten({
  beleg,
  bereichKlasse,
  werkzeugZiel,
}: {
  beleg: BelegListeneintrag
  bereichKlasse?: string
  /** Panel: Zoom-Werkzeuge wandern in dieses Element (Kopfzeile), Blättern unter die Seite. Ohne: eigene Leiste. */
  werkzeugZiel?: HTMLElement | null
}) {
  const [seite, setSeite] = useState(1)
  const [zoom, setZoom] = useState(100)
  const [drehung, setDrehung] = useState(0)
  const [seitenverhaeltnis, setSeitenverhaeltnis] = useState(A4)
  const [zieht, setZieht] = useState(false)
  const bereichRef = useRef<HTMLDivElement>(null)
  const anker = useRef<{ x: number; y: number; anteilX: number; anteilY: number } | null>(null)
  const ziehen = useRef<{ x: number; y: number; links: number; oben: number } | null>(null)
  const { breite, hoehe } = useInnenmasse(bereichRef)
  const seiten = Math.max(1, beleg.seitenanzahl)

  // Größe der (gedrehten) Seite: eingepasst in den Bereich, dann gezoomt.
  const quer = drehung % 180 !== 0
  const verhaeltnis = quer ? 1 / seitenverhaeltnis : seitenverhaeltnis
  const freiBreite = Math.max(80, breite - 2 * RAND)
  const freiHoehe = Math.max(80, hoehe - 2 * RAND)
  const boxBreite = Math.min(freiBreite, freiHoehe * verhaeltnis) * (zoom / 100)
  const boxHoehe = boxBreite / verhaeltnis
  const verschiebbar = boxBreite > freiBreite + 1 || boxHoehe > freiHoehe + 1

  // Scharf auch auf skalierten Bildschirmen und beim Zoomen: Bild in (angezeigte Breite × Pixeldichte) anfordern.
  // Nur hochstufen – beim Herauszoomen bleibt das schärfere Bild, statt ein kleineres nachzuladen.
  const [bildBreite, setBildBreite] = useState(BILD_BREITEN[0])
  const benoetigt = bildBreiteFuer((quer ? boxHoehe : boxBreite) * (window.devicePixelRatio || 1))
  if (benoetigt > bildBreite) setBildBreite(benoetigt)

  /** Zoomt so, dass der Punkt unter dem Mauszeiger (sonst die Mitte) an seiner Stelle bleibt. */
  function zoomeAuf(neu: (z: number) => number, punkt?: { x: number; y: number }) {
    const bereich = bereichRef.current
    if (bereich) {
      const rahmen = bereich.getBoundingClientRect()
      const x = punkt ? punkt.x - rahmen.left : bereich.clientWidth / 2
      const y = punkt ? punkt.y - rahmen.top : bereich.clientHeight / 2
      anker.current = {
        x,
        y,
        anteilX: (bereich.scrollLeft + x) / bereich.scrollWidth,
        anteilY: (bereich.scrollTop + y) / bereich.scrollHeight,
      }
    }
    setZoom((z) => begrenzt(neu(z)))
  }

  useLayoutEffect(() => {
    const bereich = bereichRef.current
    const a = anker.current
    if (!bereich || !a) return
    anker.current = null
    bereich.scrollLeft = a.anteilX * bereich.scrollWidth - a.x
    bereich.scrollTop = a.anteilY * bereich.scrollHeight - a.y
  }, [zoom])

  // Umschalt oder Strg/⌘ + Mausrad (auch Zwei-Finger-Zoom auf dem Trackpad) zoomt die Seite statt der ganzen App.
  const beiRad = useEffectEvent((e: WheelEvent) => {
    if (!e.shiftKey && !e.ctrlKey && !e.metaKey) return
    e.preventDefault()
    // Mit Umschalt melden manche Browser das Mausrad als waagerechtes Scrollen (deltaX).
    const delta = e.deltaY !== 0 ? e.deltaY : e.deltaX
    const faktor = Math.exp(-delta * (e.deltaMode === 1 ? 0.05 : 0.0025))
    zoomeAuf((z) => z * faktor, { x: e.clientX, y: e.clientY })
  })
  useEffect(() => {
    const bereich = bereichRef.current
    if (!bereich) return
    const horcher = (e: WheelEvent) => beiRad(e)
    bereich.addEventListener('wheel', horcher, { passive: false })
    return () => bereich.removeEventListener('wheel', horcher)
  }, [])

  const einpassen = () => setZoom(100)
  const zoomProzent = Math.round(zoom)

  const blaettern = seiten > 1 && (
    <>
      <button
        type="button"
        className={knopf}
        aria-label="Vorherige Seite"
        disabled={seite <= 1}
        onClick={() => setSeite(seite - 1)}
      >
        <Icon name="chevron_left" />
      </button>
      <span aria-live="polite" className="whitespace-nowrap text-gedaempft-stark">
        Seite {seite} von {seiten}
      </span>
      <button
        type="button"
        className={knopf}
        aria-label="Nächste Seite"
        disabled={seite >= seiten}
        onClick={() => setSeite(seite + 1)}
      >
        <Icon name="chevron_right" />
      </button>
    </>
  )

  const zoomWerkzeuge = (
    <>
      <button
        type="button"
        className={knopf}
        aria-label="Verkleinern"
        disabled={zoom <= ZOOM_MIN}
        onClick={() => zoomeAuf(stufeRunter)}
      >
        <Icon name="zoom_out" />
      </button>
      <button
        type="button"
        className="h-8 min-w-[52px] cursor-pointer rounded-md border-0 bg-transparent px-1 text-[13px] text-text tabular-nums hover:bg-hover"
        aria-label={`Zoom ${zoomProzent} %, einpassen`}
        title="Einpassen"
        onClick={einpassen}
      >
        {zoomProzent} %
      </button>
      <button
        type="button"
        className={knopf}
        aria-label="Vergrößern"
        disabled={zoom >= ZOOM_MAX}
        onClick={() => zoomeAuf(stufeHoch)}
      >
        <Icon name="zoom_in" />
      </button>
      <button
        type="button"
        className={knopf}
        aria-label="Einpassen"
        title="Einpassen"
        disabled={zoom === 100}
        onClick={einpassen}
      >
        <Icon name="fit_screen" />
      </button>
      <button
        type="button"
        className={knopf}
        aria-label="Drehen"
        title="Drehen"
        onClick={() => setDrehung((d) => (d + 90) % 360)}
      >
        <Icon name="rotate_right" />
      </button>
    </>
  )

  return (
    <>
      {werkzeugZiel === undefined ? (
        <div className="flex flex-wrap items-center gap-1 text-[13px]" role="toolbar" aria-label="Seitenansicht">
          {blaettern}
          <span className="flex-1" />
          {zoomWerkzeuge}
        </div>
      ) : (
        werkzeugZiel &&
        createPortal(
          <div className="flex items-center text-[13px]" role="toolbar" aria-label="Seitenansicht">
            {zoomWerkzeuge}
          </div>,
          werkzeugZiel,
        )
      )}

      {/* Scrollbare Region: per Tastatur fokussierbar (Pfeiltasten scrollen, + − 0 zoomen). */}
      {/* eslint-disable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex -- scrollbare Region muss fokussierbar sein */}
      <div
        ref={bereichRef}
        role="region"
        tabIndex={0}
        aria-label="Seite, Umschalt + Mausrad zum Zoomen"
        className={cn(
          'min-h-0 flex-1 overflow-auto rounded-lg bg-segment outline-none focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-akzent',
          verschiebbar && (zieht ? 'cursor-grabbing' : 'cursor-grab'),
          bereichKlasse,
        )}
        onKeyDown={(e) => {
          if (e.ctrlKey || e.metaKey || e.altKey) return
          if (e.key === '+' || e.key === '=') zoomeAuf(stufeHoch)
          else if (e.key === '-') zoomeAuf(stufeRunter)
          else if (e.key === '0') einpassen()
          else return
          e.preventDefault()
        }}
        onDoubleClick={(e) => (zoom > 100 ? einpassen() : zoomeAuf(() => 200, { x: e.clientX, y: e.clientY }))}
        onPointerDown={(e) => {
          if (e.pointerType !== 'mouse' || e.button !== 0 || !verschiebbar) return
          const bereich = e.currentTarget
          ziehen.current = { x: e.clientX, y: e.clientY, links: bereich.scrollLeft, oben: bereich.scrollTop }
          bereich.setPointerCapture(e.pointerId)
          setZieht(true)
        }}
        onPointerMove={(e) => {
          const z = ziehen.current
          if (!z) return
          e.currentTarget.scrollLeft = z.links - (e.clientX - z.x)
          e.currentTarget.scrollTop = z.oben - (e.clientY - z.y)
        }}
        onPointerUp={() => {
          ziehen.current = null
          setZieht(false)
        }}
        onPointerCancel={() => {
          ziehen.current = null
          setZieht(false)
        }}
      >
        {/* eslint-enable jsx-a11y/no-noninteractive-element-interactions, jsx-a11y/no-noninteractive-tabindex */}
        {/* m-auto zentriert, solange die Seite passt, und schneidet beim Überlaufen nichts ab. */}
        <div className="box-border flex min-h-full w-max min-w-full p-4">
          <div className="relative m-auto flex-none" style={{ width: boxBreite, height: boxHoehe }}>
            <img
              src={belegVorschauUrl(beleg.id, seite, bildBreite)}
              alt={`Seite ${seite} von ${seiten}`}
              draggable={false}
              onLoad={(e) => {
                const { naturalWidth: b, naturalHeight: h } = e.currentTarget
                if (b > 1 && h > 1) setSeitenverhaeltnis(b / h)
              }}
              className="absolute top-1/2 left-1/2 max-w-none bg-white shadow-blatt select-none"
              style={{
                width: quer ? boxHoehe : boxBreite,
                height: quer ? boxBreite : boxHoehe,
                transform: `translate(-50%, -50%) rotate(${drehung}deg)`,
              }}
            />
          </div>
        </div>
      </div>
      {werkzeugZiel !== undefined && blaettern && (
        <div className="flex items-center justify-center gap-1 text-[13px]" role="group" aria-label="Seiten">
          {blaettern}
        </div>
      )}
    </>
  )
}
