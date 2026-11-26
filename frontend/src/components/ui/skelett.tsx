import type { CSSProperties, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/*
 * Ladezustände in der Form des späteren Inhalts. Bausteine:
 * - `Skelett`: eine graue Fläche (Text, Icon, Chip …) mit Schimmer.
 * - `LadeBereich`: umschließt Skelette; meldet Screenreadern „wird geladen“ und blendet sich verzögert ein.
 * - fertige Muster: `SkelettText`, `SkelettTabelle`, `SkelettListe`, `SkelettFormular`, `SkelettKennzahlen`,
 *   `SkelettKarte`, `SkelettSeite`, `SkelettDetailSeite`.
 */

/** Graue Fläche als Platzhalter. Größe über `className` (z. B. `h-4 w-32`) oder `breite`/`hoehe`. */
export function Skelett({
  className,
  breite,
  hoehe,
  rund,
  style,
}: {
  className?: string
  breite?: number | string
  hoehe?: number | string
  /** Kreis (z. B. Avatar, Logo). */
  rund?: boolean
  style?: CSSProperties
}) {
  return (
    <span
      aria-hidden="true"
      data-testid="skelett"
      className={cn('skelett', rund && 'rounded-full', className)}
      style={{ width: breite, height: hoehe, ...style }}
    />
  )
}

/**
 * Rahmen um Skelette: `role="status"` mit Beschriftung für Screenreader, `aria-busy`, verzögertes Einblenden.
 * `als="main"` für ganze Seiten, damit der Seitenrahmen (`.seite`) gleich bleibt.
 */
export function LadeBereich({
  label = 'Wird geladen',
  als: Element = 'div',
  className,
  style,
  children,
}: {
  /** `null`: kein eigener Status, weil ein umgebender `LadeBereich` ihn schon meldet. */
  label?: string | null
  als?: 'div' | 'main' | 'section' | 'li'
  className?: string
  style?: CSSProperties
  children: ReactNode
}) {
  if (label === null)
    return (
      <Element className={className} style={style}>
        {children}
      </Element>
    )
  return (
    <Element
      role="status"
      aria-busy="true"
      aria-label={label}
      className={cn('skelett-bereich', className)}
      style={style}
    >
      {children}
    </Element>
  )
}

/** Breiten für Textzeilen, damit Skelette nicht wie ein Block wirken. */
const ZEILENBREITEN = ['92%', '78%', '86%', '64%', '81%', '70%']

/** Mehrere Textzeilen; die letzte ist kürzer wie ein Absatzende. */
export function SkelettText({
  zeilen = 3,
  hoehe = 12,
  className,
}: {
  zeilen?: number
  hoehe?: number
  className?: string
}) {
  return (
    <div className={cn('flex flex-col gap-2.5', className)}>
      {Array.from({ length: zeilen }, (_, i) => (
        <Skelett
          key={i}
          hoehe={hoehe}
          breite={zeilen > 1 && i === zeilen - 1 ? '45%' : ZEILENBREITEN[i % ZEILENBREITEN.length]}
        />
      ))}
    </div>
  )
}

/** Tabelle in einer Karte: Kopfzeile und `zeilen` Reihen mit `spalten` Zellen unterschiedlicher Breite. */
export function SkelettTabelle({
  zeilen = 8,
  spalten = 5,
  label = 'Tabelle wird geladen',
  mitKarte = true,
  className,
}: {
  zeilen?: number
  spalten?: number
  label?: string | null
  /** Ohne eigene Karte, wenn die Tabelle schon in einer steht. */
  mitKarte?: boolean
  className?: string
}) {
  const raster = { gridTemplateColumns: `repeat(${spalten}, minmax(0, 1fr))` }
  return (
    <LadeBereich label={label} className={cn(mitKarte && 'karte overflow-hidden', className)}>
      <div className="grid items-center gap-4 border-b border-rand bg-tabellenkopf px-3 py-3" style={raster}>
        {Array.from({ length: spalten }, (_, s) => (
          <Skelett key={s} hoehe={10} breite={s === 0 ? '55%' : '40%'} />
        ))}
      </div>
      {Array.from({ length: zeilen }, (_, z) => (
        <div
          key={z}
          className="grid items-center gap-4 border-b border-linie px-3 py-[13px] last:border-b-0"
          style={raster}
        >
          {Array.from({ length: spalten }, (_, s) => (
            <Skelett
              key={s}
              hoehe={12}
              breite={s === 0 ? ZEILENBREITEN[z % ZEILENBREITEN.length] : `${45 + ((z * 7 + s * 13) % 40)}%`}
              className={s === spalten - 1 && spalten > 2 ? 'justify-self-end' : undefined}
            />
          ))}
        </div>
      ))}
    </LadeBereich>
  )
}

/** Liste mit Symbol und zwei Textzeilen je Eintrag (Benachrichtigungen, Popover-Listen, Karten). */
export function SkelettListe({
  zeilen = 5,
  label = 'Liste wird geladen',
  symbol = true,
  kompakt = false,
  className,
}: {
  zeilen?: number
  label?: string | null
  /** Rundes Symbol links (Logo, Icon). */
  symbol?: boolean
  /** Eine Zeile statt zwei, engerer Abstand (Popover, Auswahllisten). */
  kompakt?: boolean
  className?: string
}) {
  return (
    <LadeBereich label={label} className={cn('flex flex-col', className)}>
      {Array.from({ length: zeilen }, (_, i) => (
        <div key={i} className={cn('flex items-center gap-3', kompakt ? 'h-[38px] px-2.5' : 'py-3')}>
          {symbol && <Skelett rund breite={kompakt ? 20 : 32} hoehe={kompakt ? 20 : 32} />}
          <div className="flex min-w-0 flex-1 flex-col gap-2">
            <Skelett hoehe={12} breite={ZEILENBREITEN[i % ZEILENBREITEN.length]} />
            {!kompakt && <Skelett hoehe={10} breite="40%" />}
          </div>
        </div>
      ))}
    </LadeBereich>
  )
}

/** Formular: Beschriftung und Eingabefeld je Feld, in `spalten` Spalten. */
export function SkelettFormular({
  felder = 4,
  spalten = 2,
  label = 'Formular wird geladen',
  mitKnopf = true,
  className,
}: {
  felder?: number
  spalten?: 1 | 2
  label?: string | null
  /** Speichern-Knopf unten. */
  mitKnopf?: boolean
  className?: string
}) {
  return (
    <LadeBereich label={label} className={cn('flex flex-col gap-5', className)}>
      <div className={cn('grid gap-4', spalten === 2 && 'grid-cols-[repeat(auto-fit,minmax(240px,1fr))]')}>
        {Array.from({ length: felder }, (_, i) => (
          <div key={i} className="flex flex-col gap-1.5">
            <Skelett hoehe={10} breite={`${30 + ((i * 17) % 30)}%`} />
            <Skelett hoehe={36} className="rounded-[var(--radius-eingabe)]" />
          </div>
        ))}
      </div>
      {mitKnopf && <Skelett hoehe={36} breite={110} className="rounded-[var(--radius-button)]" />}
    </LadeBereich>
  )
}

/** Kennzahlen-Kacheln (Dashboard, Detailseiten). */
export function SkelettKennzahlen({
  anzahl = 4,
  className,
  hoehe = 96,
}: {
  anzahl?: number
  className?: string
  hoehe?: number
}) {
  return (
    <div className={cn('grid grid-cols-2 gap-3.5 md:grid-cols-4', className)}>
      {Array.from({ length: anzahl }, (_, i) => (
        <div key={i} className="karte flex flex-col justify-between gap-3 p-4" style={{ minHeight: hoehe }}>
          <Skelett hoehe={10} breite="55%" />
          <Skelett hoehe={22} breite="70%" />
          <Skelett hoehe={10} breite="40%" />
        </div>
      ))}
    </div>
  )
}

/** Karte mit Überschrift; Inhalt frei oder als Textzeilen bzw. feste Höhe (z. B. Diagramm). */
export function SkelettKarte({
  hoehe,
  zeilen = 3,
  className,
  children,
}: {
  /** Fläche dieser Höhe statt Textzeilen (Diagramme). */
  hoehe?: number
  zeilen?: number
  className?: string
  children?: ReactNode
}) {
  return (
    <div className={cn('karte flex min-w-0 flex-col gap-4 p-5', className)}>
      <Skelett hoehe={14} breite={140} />
      {children ?? (hoehe ? <Skelett hoehe={hoehe} className="w-full rounded-lg" /> : <SkelettText zeilen={zeilen} />)}
    </div>
  )
}

/** Seitenkopf: Titel und optional eine Zeile darunter, rechts Knöpfe. */
export function SkelettKopf({ knoepfe = 1, untertitel = true }: { knoepfe?: number; untertitel?: boolean }) {
  return (
    <div className="mb-5 flex flex-wrap items-start justify-between gap-4">
      <div className="flex flex-col gap-2.5">
        <Skelett hoehe={22} breite={220} />
        {untertitel && <Skelett hoehe={12} breite={300} className="max-w-[60vw]" />}
      </div>
      {knoepfe > 0 && (
        <div className="flex gap-2">
          {Array.from({ length: knoepfe }, (_, i) => (
            <Skelett key={i} hoehe={36} breite={i === 0 ? 120 : 96} className="rounded-[var(--radius-button)]" />
          ))}
        </div>
      )}
    </div>
  )
}

/** Allgemeine Seite (Seitenwechsel ohne passendes Skelett): Kopf und eine Tabelle. */
export function SkelettSeite({ label = 'Seite wird geladen' }: { label?: string }) {
  return (
    <LadeBereich als="main" label={label} className="seite">
      <SkelettKopf />
      <SkelettTabelle zeilen={6} label={null} />
    </LadeBereich>
  )
}

/**
 * Detailseite (Korrespondent, Artikel): Brotkrumen, Kopf mit Symbol, Kennzahlen, zwei Karten nebeneinander,
 * darunter ein Diagramm und eine Tabelle.
 */
export function SkelettDetailSeite({
  label = 'Details werden geladen',
  symbolRund = false,
  zweiSpalten = true,
}: {
  label?: string
  /** Rundes Symbol (Logo) statt eckigem Icon. */
  symbolRund?: boolean
  /** Zwei Karten nebeneinander unter den Kennzahlen. */
  zweiSpalten?: boolean
}) {
  return (
    <LadeBereich als="main" label={label} className="seite">
      <Skelett hoehe={10} breite={160} className="mb-3" />
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          <Skelett
            rund={symbolRund}
            breite={symbolRund ? 56 : 40}
            hoehe={symbolRund ? 56 : 40}
            className={symbolRund ? undefined : 'rounded-[9px]'}
          />
          <div className="flex flex-col gap-2.5">
            <Skelett hoehe={22} breite={240} />
            <Skelett hoehe={12} breite={180} />
          </div>
        </div>
        <Skelett hoehe={36} breite={120} className="rounded-[var(--radius-button)]" />
      </div>
      <SkelettKennzahlen className="mb-4" />
      <div className="flex flex-col gap-4">
        {zweiSpalten && (
          <div className="grid gap-4 lg:grid-cols-2">
            <SkelettKarte zeilen={4} />
            <SkelettKarte zeilen={4} />
          </div>
        )}
        <SkelettKarte hoehe={220} />
        <SkelettTabelle zeilen={4} label={null} />
      </div>
    </LadeBereich>
  )
}

/** Drehkreis für Stellen ohne Knopf (z. B. „Suche läuft“ in einer Liste). Farbe folgt dem Text. */
export function Drehkreis({ className, label }: { className?: string; label?: string }) {
  return label ? (
    <span role="status" aria-label={label} className={cn('drehkreis', className)} />
  ) : (
    <span aria-hidden="true" className={cn('drehkreis', className)} />
  )
}
