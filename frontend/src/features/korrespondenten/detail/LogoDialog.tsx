import { useEffect, useId, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react'
import { belegVorschauUrl, useLogoUebernehmen } from '@/api/belege'
import { useLogoEntfernen, useLogoFavicon, useLogoHochladen } from '@/api/korrespondenten'
import type { KorrespondentDetail } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Skelett } from '@/components/ui/skelett'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDatum } from '@/lib/format'
import { cn } from '@/lib/utils'
import { skalieren, startAusschnitt, verschieben, type Rechteck } from './ausschnitt'

export type LogoReiter = 'ausschnitt' | 'upload' | 'favicon'

const MAX_BYTES = 2 * 1024 * 1024
const ERLAUBT = ['image/png', 'image/jpeg', 'image/webp']
/** Schrittweite der Pfeiltasten als Anteil der Seite. */
const SCHRITT = 0.01

/** Domain ohne Schema und Pfad, z. B. „buerowelt-kraemer.de“. */
export function domainAus(website?: string | null): string | null {
  const roh = website?.trim()
  if (!roh) return null
  return roh.replace(/^https?:\/\//i, '').replace(/\/.*$/, '') || null
}

/** Prüft eine Logo-Datei: PNG, JPG oder WebP bis 2 MB. Liefert den Fehlertext oder null. */
export function dateiFehler(datei: File): string | null {
  if (!ERLAUBT.includes(datei.type)) return 'Nur PNG, JPG oder WebP.'
  if (datei.size > MAX_BYTES) return 'Die Datei ist größer als 2 MB.'
  return null
}

function vorschauUrl(belegId: string): string {
  const url = belegVorschauUrl(belegId, 1)
  return `${url}${url.includes('?') ? '&' : '?'}breite=1200`
}

/** Seite 1 mit verschieb- und skalierbarem Auswahlrechteck (Maus und Tastatur). */
function AusschnittWaehler({
  bild,
  wert,
  onAendern,
}: {
  bild: string
  wert: Rechteck
  onAendern: (r: Rechteck) => void
}) {
  const flaeche = useRef<HTMLDivElement>(null)
  const zug = useRef<{ art: 'move' | 'size'; x: number; y: number; start: Rechteck } | null>(null)
  const [geladenBild, setGeladenBild] = useState<string | null>(null)
  const geladen = geladenBild === bild

  const tasten = (e: KeyboardEvent) => {
    const d = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[e.key]
    if (!d) return
    e.preventDefault()
    const [dx, dy] = [d[0] * SCHRITT, d[1] * SCHRITT]
    onAendern(e.shiftKey ? skalieren(wert, dx, dy) : verschieben(wert, dx, dy))
  }

  const beginne = (art: 'move' | 'size', e: PointerEvent) => {
    e.stopPropagation()
    e.currentTarget.setPointerCapture?.(e.pointerId)
    zug.current = { art, x: e.clientX, y: e.clientY, start: wert }
  }
  const startVerschieben = (e: PointerEvent) => beginne('move', e)
  const startSkalieren = (e: PointerEvent) => beginne('size', e)
  const bewegen = (e: PointerEvent) => {
    const z = zug.current
    const box = flaeche.current?.getBoundingClientRect()
    if (!z || !box?.width || !box.height) return
    const dx = (e.clientX - z.x) / box.width
    const dy = (e.clientY - z.y) / box.height
    onAendern(z.art === 'move' ? verschieben(z.start, dx, dy) : skalieren(z.start, dx, dy))
  }
  const ende = () => (zug.current = null)

  return (
    <div
      ref={flaeche}
      aria-busy={!geladen || undefined}
      className={cn(
        'relative select-none overflow-hidden rounded-lg border border-rand bg-karte',
        !geladen && 'aspect-[1000/1414]',
      )}
    >
      {!geladen && <Skelett className="absolute inset-0 !h-full w-full" />}
      <img
        src={bild}
        alt="Seite 1 des Belegs"
        className="block w-full"
        draggable={false}
        onLoad={() => setGeladenBild(bild)}
        onError={() => setGeladenBild(bild)}
      />
      <div
        role="slider"
        tabIndex={0}
        aria-label="Auswahlrechteck"
        aria-valuetext={`x ${Math.round(wert.x * 100)} %, y ${Math.round(wert.y * 100)} %, Breite ${Math.round(wert.breite * 100)} %, Höhe ${Math.round(wert.hoehe * 100)} %`}
        aria-valuenow={Math.round(wert.x * 100)}
        aria-keyshortcuts="ArrowLeft ArrowRight ArrowUp ArrowDown Shift+ArrowLeft Shift+ArrowRight Shift+ArrowUp Shift+ArrowDown"
        onKeyDown={tasten}
        onPointerDown={startVerschieben}
        onPointerMove={bewegen}
        onPointerUp={ende}
        className="absolute cursor-move border-2 border-[var(--color-akzent)] bg-[color-mix(in_srgb,var(--color-akzent)_12%,transparent)] outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-akzent)]"
        style={{
          left: `${wert.x * 100}%`,
          top: `${wert.y * 100}%`,
          width: `${wert.breite * 100}%`,
          height: `${wert.hoehe * 100}%`,
        }}
      >
        <span
          aria-hidden="true"
          onPointerDown={startSkalieren}
          onPointerMove={bewegen}
          onPointerUp={ende}
          className="absolute -bottom-1.5 -right-1.5 size-3 cursor-nwse-resize rounded-sm bg-[var(--color-akzent)]"
        />
      </div>
    </div>
  )
}

/** Seitenverhältnis bis das Bild geladen ist: DIN A4 hochkant. */
const A4 = { breite: 1000, hoehe: 1414 }

/**
 * Vorschau des Ausschnitts: dieselbe Seite, so vergrößert, dass nur das Rechteck sichtbar ist.
 * Die viewBox liegt in Bildpixeln, damit das Seitenverhältnis des Ausschnitts erhalten bleibt
 * und er unverzerrt in die Box eingepasst wird.
 */
function AusschnittVorschau({ bild, wert }: { bild: string; wert: Rechteck }) {
  const [groesse, setGroesse] = useState<{ bild: string; breite: number; hoehe: number } | null>(null)

  useEffect(() => {
    let aktiv = true
    const img = new Image()
    img.onload = () => {
      if (aktiv && img.naturalWidth && img.naturalHeight)
        setGroesse({ bild, breite: img.naturalWidth, hoehe: img.naturalHeight })
    }
    img.src = bild
    return () => {
      aktiv = false
    }
  }, [bild])

  const { breite: b, hoehe: h } = groesse?.bild === bild ? groesse : A4
  return (
    <svg
      role="img"
      aria-label="Vorschau des Ausschnitts"
      className="h-24 w-full rounded-lg border border-rand bg-karte"
      viewBox={`${wert.x * b} ${wert.y * h} ${Math.max(wert.breite * b, 1)} ${Math.max(wert.hoehe * h, 1)}`}
      preserveAspectRatio="xMidYMid meet"
    >
      <image href={bild} x={0} y={0} width={b} height={h} preserveAspectRatio="none" />
    </svg>
  )
}

function ReiterAusschnitt({
  detail,
  startBelegId,
  onFertig,
}: {
  detail: KorrespondentDetail
  startBelegId?: string
  onFertig: () => void
}) {
  const id = useId()
  const toast = useToast()
  const uebernehmen = useLogoUebernehmen()
  const belege = detail.letzteBelege
  const [belegId, setBelegId] = useState(startBelegId ?? belege[0]?.id ?? '')
  const [wert, setWert] = useState<Rechteck>(startAusschnitt)
  if (!belegId) return <p className="gedaempft m-0 text-sm">Zu diesem Korrespondenten gibt es noch keine Belege.</p>
  const bild = vorschauUrl(belegId)
  const ausgewaehlt = belege.some((b) => b.id === belegId)
  return (
    <div className="grid gap-3">
      <label htmlFor={`${id}-beleg`} className="label m-0">
        Beleg
      </label>
      <select id={`${id}-beleg`} className="eingabe" value={belegId} onChange={(e) => setBelegId(e.target.value)}>
        {!ausgewaehlt && <option value={belegId}>Beleg aus dem Link</option>}
        {belege.map((b) => (
          <option key={b.id} value={b.id}>
            {[b.rechnungsnummer ?? 'ohne Nummer', b.rechnungsdatum && formatDatum(b.rechnungsdatum)]
              .filter(Boolean)
              .join(' · ')}
          </option>
        ))}
      </select>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_180px]">
        <AusschnittWaehler bild={bild} wert={wert} onAendern={setWert} />
        <div className="grid content-start gap-2">
          <span className="label m-0">Vorschau</span>
          <AusschnittVorschau bild={bild} wert={wert} />
          <p className="gedaempft m-0 text-xs">Pfeiltasten verschieben, Umschalt + Pfeiltasten ändern die Größe.</p>
        </div>
      </div>
      <div className="flex justify-end">
        <Button
          variant="primaer"
          laedt={uebernehmen.isPending}
          onClick={() =>
            uebernehmen.mutateAsync({ id: belegId, seite: 1, ...wert }).then(
              () => {
                toast.erfolg('Logo aus dem Beleg übernommen')
                onFertig()
              },
              (f: Error) => toast.fehler(f.message),
            )
          }
        >
          Als Logo speichern
        </Button>
      </div>
    </div>
  )
}

function ReiterUpload({ korrespondentId, onFertig }: { korrespondentId: string; onFertig: () => void }) {
  const id = useId()
  const toast = useToast()
  const hochladen = useLogoHochladen()
  const [datei, setDatei] = useState<File | null>(null)
  const [fehler, setFehler] = useState<string | null>(null)
  const [vorschau, setVorschau] = useState<string | null>(null)

  useEffect(() => (vorschau ? () => URL.revokeObjectURL(vorschau) : undefined), [vorschau])

  return (
    <div className="grid gap-3">
      <label htmlFor={`${id}-datei`} className="label m-0">
        Logo-Datei (PNG, JPG oder WebP, bis 2 MB)
      </label>
      <input
        id={`${id}-datei`}
        type="file"
        accept={ERLAUBT.join(',')}
        aria-describedby={fehler ? `${id}-fehler` : undefined}
        onChange={(e) => {
          const neu = e.target.files?.[0] ?? null
          const problem = neu ? dateiFehler(neu) : null
          setFehler(problem)
          const gueltig = problem ? null : neu
          setDatei(gueltig)
          setVorschau(gueltig && typeof URL.createObjectURL === 'function' ? URL.createObjectURL(gueltig) : null)
        }}
      />
      {fehler && (
        <p id={`${id}-fehler`} role="alert" className="m-0 text-[13px] text-[var(--color-rot-text)]">
          {fehler}
        </p>
      )}
      {vorschau && (
        <img src={vorschau} alt="Vorschau des Logos" className="size-24 rounded-lg border border-rand object-contain" />
      )}
      <div className="flex justify-end">
        <Button
          variant="primaer"
          disabled={!datei || hochladen.isPending}
          laedt={hochladen.isPending}
          onClick={() =>
            datei &&
            hochladen.mutateAsync({ id: korrespondentId, datei }).then(
              () => {
                toast.erfolg('Logo hochgeladen')
                onFertig()
              },
              (f: Error) => toast.fehler(f.message),
            )
          }
        >
          Logo speichern
        </Button>
      </div>
    </div>
  )
}

function ReiterFavicon({ detail, onFertig }: { detail: KorrespondentDetail; onFertig: () => void }) {
  const toast = useToast()
  const favicon = useLogoFavicon()
  const domain = domainAus(detail.website)
  return (
    <div className="grid gap-3">
      {!domain && (
        <p className="gedaempft m-0 text-sm">
          Ohne Website lässt sich kein Favicon abrufen. Trage zuerst eine Website ein.
        </p>
      )}
      <div>
        <Button
          disabled={!domain || favicon.isPending}
          laedt={favicon.isPending}
          onClick={() =>
            favicon.mutateAsync(detail.id).then(
              () => {
                toast.erfolg('Favicon als Logo übernommen')
                onFertig()
              },
              (f: Error) => toast.fehler(f.message),
            )
          }
        >
          <Icon name="public" />
          {domain ? `Favicon von ${domain} abrufen` : 'Favicon abrufen'}
        </Button>
      </div>
    </div>
  )
}

/** Dialog „Logo festlegen“: Ausschnitt aus einem Beleg, Datei-Upload oder Favicon der Website. */
export function LogoDialog({
  detail,
  startReiter = 'ausschnitt',
  startBelegId,
  onSchliessen,
}: {
  detail: KorrespondentDetail
  startReiter?: LogoReiter
  startBelegId?: string
  onSchliessen: () => void
}) {
  const toast = useToast()
  const entfernen = useLogoEntfernen()
  const [reiter, setReiter] = useState<LogoReiter>(startReiter)
  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent className="max-w-[720px]">
        <DialogHeader>
          <DialogTitle>Logo festlegen</DialogTitle>
          <DialogDescription>{detail.name}</DialogDescription>
        </DialogHeader>
        <Tabs value={reiter} onValueChange={(v) => setReiter(v as LogoReiter)}>
          <TabsList>
            <TabsTrigger value="ausschnitt">Aus Beleg ausschneiden</TabsTrigger>
            <TabsTrigger value="upload">Datei hochladen</TabsTrigger>
            <TabsTrigger value="favicon">Favicon</TabsTrigger>
          </TabsList>
          <TabsContent value="ausschnitt" className="pt-4">
            <ReiterAusschnitt detail={detail} startBelegId={startBelegId} onFertig={onSchliessen} />
          </TabsContent>
          <TabsContent value="upload" className="pt-4">
            <ReiterUpload korrespondentId={detail.id} onFertig={onSchliessen} />
          </TabsContent>
          <TabsContent value="favicon" className="pt-4">
            <ReiterFavicon detail={detail} onFertig={onSchliessen} />
          </TabsContent>
        </Tabs>
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-linie pt-3">
          <span className="gedaempft text-xs">Reihenfolge: Upload › Ausschnitt aus Seite 1 › Favicon der Website</span>
          {detail.logo.quelle !== 'Keine' && (
            <Button
              variant="gefaehrlich"
              laedt={entfernen.isPending}
              onClick={() =>
                entfernen.mutateAsync(detail.id).then(
                  () => {
                    toast.erfolg('Logo entfernt')
                    onSchliessen()
                  },
                  (f: Error) => toast.fehler(f.message),
                )
              }
            >
              <Icon name="delete" />
              Logo entfernen
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}
