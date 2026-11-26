import { Link } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import type { BelegListeneintrag, BelegSortierung } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatDatum, formatEuro, formatProzent } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useBelegAuswahl } from './auswahl'
import { auswahlLabel, faelligText, TagChips } from './bausteine'
import { spaltenDefinitionen, type SpaltenDefinition, type SpaltenSchluessel } from './spaltenDefinitionen'
import { quelleIcon, quelleText, type ListenZustand } from './suche'
import { useListenKuerzel } from './useListenKuerzel'
import { Zeilenmenue } from './Zeilenmenue'

function betrag(wert: number | null | undefined, waehrung: string): string {
  return wert === null || wert === undefined ? '–' : formatEuro(wert, waehrung)
}

/** Inhalt einer Zelle je Spalte. */
function zelle(schluessel: SpaltenSchluessel, beleg: BelegListeneintrag): { inhalt: ReactNode; className?: string } {
  switch (schluessel) {
    case 'rechnungsnummer':
      return { inhalt: beleg.rechnungsnummer ?? '–', className: 'mono whitespace-nowrap text-gedaempft-stark' }
    case 'korrespondent':
      return { inhalt: beleg.korrespondent?.name ?? '–', className: 'min-w-[120px] min-[1760px]:whitespace-nowrap' }
    case 'titel':
      return {
        inhalt: (
          <Link to="/belege/$belegId" params={{ belegId: beleg.id }} className="font-medium underline">
            {beleg.titel}
          </Link>
        ),
        className: 'min-w-[160px]',
      }
    case 'tags':
      return {
        inhalt:
          beleg.tags.length > 0 ? (
            <span className="flex flex-wrap items-center gap-1">
              <TagChips tags={beleg.tags} />
            </span>
          ) : (
            '–'
          ),
        className: 'min-w-[90px]',
      }
    case 'kategorie':
      return { inhalt: beleg.kategorie?.name ?? '–', className: 'min-w-[100px] min-[1760px]:whitespace-nowrap' }
    case 'rechnungsdatum':
      return {
        inhalt: beleg.rechnungsdatum ? formatDatum(beleg.rechnungsdatum) : '–',
        className: 'num !text-left',
      }
    case 'faelligkeit':
      return {
        inhalt: faelligText(beleg),
        className: cn(
          'num !text-left whitespace-normal min-[1760px]:whitespace-nowrap',
          beleg.ueberfaellig ? 'font-semibold text-rot-text' : 'text-gedaempft-stark',
        ),
      }
    case 'brutto':
      return { inhalt: betrag(beleg.brutto, beleg.waehrung), className: 'num font-semibold' }
    case 'netto':
      return { inhalt: betrag(beleg.netto, beleg.waehrung), className: 'num' }
    case 'seiten':
      return { inhalt: beleg.seitenanzahl, className: 'num !text-left' }
    case 'leistungsdatum':
      return { inhalt: beleg.leistungsdatum ? formatDatum(beleg.leistungsdatum) : '–', className: 'num !text-left' }
    case 'konfidenz':
      return {
        inhalt: beleg.konfidenz === null || beleg.konfidenz === undefined ? '–' : formatProzent(beleg.konfidenz),
        className: 'num !text-left',
      }
    case 'herkunft':
      return {
        inhalt: (
          <span className="inline-flex flex-wrap items-center gap-1.5 text-[13px] text-gedaempft-stark">
            <Icon name={quelleIcon[beleg.quelle]} label={quelleText[beleg.quelle]} className="text-[18px]" />
            <Chip ton="grau">{beleg.format}</Chip>
          </span>
        ),
      }
  }
}

/** Komposita wie „Rechnungsdatum“ dürfen auf Laptop-Breiten vor „datum“ umbrechen (Name bleibt gleich). */
function kopfText(label: string): ReactNode {
  const i = label.lastIndexOf('datum')
  if (i <= 0) return label
  return (
    <>
      {label.slice(0, i)}
      <wbr />
      {label.slice(i)}
    </>
  )
}

/**
 * Spaltenbeschriftung wie der Sortier-Knopf (`.th-sort` im Design), auch für nicht sortierbare Spalten.
 * Füllt die Breite bis zu den Icons am rechten Rand des Spaltenkopfs.
 */
const kopfStil =
  'flex min-w-0 flex-1 items-center gap-1 rounded-md border-0 bg-transparent px-1 py-1.5 text-left text-[12px] leading-[18px] font-semibold text-text min-[1760px]:px-1.5 min-[1760px]:text-[13px]'

function Spaltenkopf({
  definition,
  sortierung,
  absteigend,
  onSortieren,
}: {
  definition: SpaltenDefinition
  sortierung: BelegSortierung
  absteigend: boolean
  onSortieren: (sortierung: BelegSortierung) => void
}) {
  const sortierbar = definition.sortierung
  const aktiv = sortierbar !== undefined && sortierbar === sortierung
  // Absteigend ist der „leere“ Zustand (grauer ↓ nur bei Hover); nur aufsteigend wird dauerhaft angezeigt.
  const aufsteigend = aktiv && !absteigend
  const richtung = absteigend ? 'absteigend' : 'aufsteigend'
  // Der Sortierpfeil steht rechts im Spaltenkopf und erscheint erst bei Hover/Fokus (Platz bleibt reserviert,
  // damit nichts springt). Aufsteigende Sortierung bleibt immer sichtbar.
  const beiHover = 'opacity-0 group-has-[:focus-visible]:opacity-100 group-hover:opacity-100'
  // Beträge: Beschriftung rechtsbündig über den Zahlen, direkt links neben den Icons.
  const text = (
    <span className={cn('min-w-0 flex-1', definition.betrag && 'text-right')}>{kopfText(definition.label)}</span>
  )
  return (
    <th
      scope="col"
      className="group !py-1.5 !pr-1.5 whitespace-normal min-[1760px]:whitespace-nowrap"
      aria-sort={aktiv ? (absteigend ? 'descending' : 'ascending') : sortierbar ? 'none' : undefined}
    >
      {/* Hover-Hintergrund umschließt Beschriftung und Sortierpfeil. */}
      <div className="-ml-1 flex items-center rounded-md group-hover:bg-linie min-[1760px]:-ml-1.5 min-[1760px]:gap-0.5">
        {sortierbar ? (
          <button
            type="button"
            title="Sortieren"
            aria-label={`${definition.label} sortieren${aktiv ? `, derzeit ${richtung}` : ''}`}
            onClick={() => onSortieren(sortierbar)}
            className={cn(kopfStil, 'cursor-pointer')}
          >
            {text}
            <Icon
              name={aufsteigend ? 'arrow_upward' : 'arrow_downward'}
              className={cn(
                'flex-none text-[16px] [vertical-align:0]',
                // Unter 1760 px ohne Platz für den Hinweispfeil: dort zeigt nur der Hover-Hintergrund des
                // Knopfs, dass die Spalte sortierbar ist – so passen alle Standardspalten ohne Querscrollen.
                aufsteigend ? 'text-akzent-text' : cn('text-[#9AA4B5] max-[1760px]:!hidden', beiHover),
              )}
            />
          </button>
        ) : (
          <span className={kopfStil}>{text}</span>
        )}
      </div>
    </th>
  )
}

/**
 * Belegliste als Tabelle: Auswahl, gewählte Spalten in ihrer Reihenfolge, ⋮-Menü. Jeder Spaltenkopf trägt
 * einen Sortier-Knopf (erster Klick aufsteigend, jeder weitere kehrt die Richtung um). Gefiltert wird über die
 * Filterleiste über der Liste.
 */
export function BelegTabelle({
  eintraege,
  spalten,
  zustand,
}: {
  eintraege: BelegListeneintrag[]
  spalten: SpaltenSchluessel[]
  zustand: ListenZustand
}) {
  const { suche, setze } = zustand
  const auswahl = useBelegAuswahl()
  const tbody = useListenKuerzel(eintraege)
  const sortierung = suche.sortierung ?? 'Rechnungsdatum'
  const absteigend = suche.absteigend ?? true
  const definitionen = spalten.map((s) => spaltenDefinitionen.find((d) => d.schluessel === s)!)
  const seitenIds = eintraege.map((b) => b.id)
  const alleGewaehlt = seitenIds.length > 0 && seitenIds.every(auswahl.istGewaehlt)

  function sortieren(neu: BelegSortierung) {
    // Neue Spalte: erst aufsteigend; erneuter Klick schaltet zwischen aufsteigend und absteigend (leerer Zustand).
    const neuAbsteigend = neu === sortierung ? !absteigend : false
    setze({
      sortierung: neu === 'Rechnungsdatum' ? undefined : neu,
      absteigend: neuAbsteigend ? undefined : false,
    })
  }

  return (
    <div className="karte overflow-x-auto">
      <table className="tabelle min-w-[960px]">
        <thead>
          <tr>
            <th scope="col" className="w-9">
              <input
                type="checkbox"
                aria-label="Alle auswählen"
                checked={alleGewaehlt}
                onChange={() => (alleGewaehlt ? auswahl.leeren() : auswahl.alleSetzen(seitenIds))}
                className="h-4 w-4"
              />
            </th>
            {definitionen.map((d) => (
              <Spaltenkopf
                key={d.schluessel}
                definition={d}
                sortierung={sortierung}
                absteigend={absteigend}
                onSortieren={sortieren}
              />
            ))}
            <th scope="col" className="w-10">
              <span className="sr-only">Aktionen</span>
            </th>
          </tr>
        </thead>
        <tbody ref={tbody}>
          {eintraege.map((beleg) => {
            const gewaehlt = auswahl.istGewaehlt(beleg.id)
            return (
              <tr
                key={beleg.id}
                tabIndex={0}
                data-gewaehlt={gewaehlt || undefined}
                className={cn('cursor-pointer', gewaehlt ? 'bg-akzent-zart' : 'bg-karte')}
              >
                <td data-kein-zeilenklick>
                  <input
                    type="checkbox"
                    aria-label={auswahlLabel(beleg)}
                    checked={gewaehlt}
                    onChange={() => auswahl.umschalten(beleg.id)}
                    className="h-4 w-4"
                  />
                </td>
                {spalten.map((s) => {
                  const { inhalt, className } = zelle(s, beleg)
                  return (
                    <td key={s} className={className}>
                      {inhalt}
                    </td>
                  )
                })}
                <td data-kein-zeilenklick>
                  <Zeilenmenue beleg={beleg} />
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
