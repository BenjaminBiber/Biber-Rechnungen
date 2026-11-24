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
      return { inhalt: beleg.korrespondent?.name ?? '–', className: 'whitespace-nowrap' }
    case 'titel':
      return {
        inhalt: (
          <span className="flex flex-wrap items-center gap-1.5">
            <Icon name="visibility" className="text-[18px] text-gedaempft" />
            <Link to="/belege/$belegId" params={{ belegId: beleg.id }} className="font-medium underline">
              {beleg.titel}
            </Link>
            <TagChips tags={beleg.tags} />
          </span>
        ),
      }
    case 'kategorie':
      return { inhalt: beleg.kategorie?.name ?? '–', className: 'whitespace-nowrap' }
    case 'rechnungsdatum':
      return {
        inhalt: beleg.rechnungsdatum ? formatDatum(beleg.rechnungsdatum) : '–',
        className: 'num !text-left',
      }
    case 'faelligkeit':
      return {
        inhalt: faelligText(beleg),
        className: cn('num !text-left', beleg.ueberfaellig ? 'font-semibold text-rot-text' : 'text-gedaempft-stark'),
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
          <span className="inline-flex items-center gap-1.5 text-[13px] text-gedaempft-stark">
            <Icon name={quelleIcon[beleg.quelle]} label={quelleText[beleg.quelle]} className="text-[18px]" />
            <Chip ton="grau">{beleg.format}</Chip>
          </span>
        ),
      }
  }
}

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
  return (
    <th
      scope="col"
      className={cn(definition.betrag && 'num')}
      aria-sort={aktiv ? (absteigend ? 'descending' : 'ascending') : sortierbar ? 'none' : undefined}
    >
      {sortierbar ? (
        <button
          type="button"
          onClick={() => onSortieren(sortierbar)}
          className="inline-flex cursor-pointer items-center gap-1 border-0 bg-transparent p-0 font-[inherit] text-inherit"
        >
          {definition.label}
          {aktiv && <Icon name={absteigend ? 'arrow_downward' : 'arrow_upward'} className="text-[16px]" />}
        </button>
      ) : (
        definition.label
      )}
    </th>
  )
}

/**
 * Belegliste als Tabelle: Auswahl, gewählte Spalten in ihrer Reihenfolge, ⋮-Menü. Klick auf einen
 * sortierbaren Spaltenkopf sortiert danach; erneuter Klick kehrt die Richtung um.
 */
export function BelegTabelle({
  eintraege,
  spalten,
  zustand: { suche, setze },
}: {
  eintraege: BelegListeneintrag[]
  spalten: SpaltenSchluessel[]
  zustand: ListenZustand
}) {
  const auswahl = useBelegAuswahl()
  const sortierung = suche.sortierung ?? 'Rechnungsdatum'
  const absteigend = suche.absteigend ?? true
  const definitionen = spalten.map((s) => spaltenDefinitionen.find((d) => d.schluessel === s)!)
  const seitenIds = eintraege.map((b) => b.id)
  const alleGewaehlt = seitenIds.length > 0 && seitenIds.every(auswahl.istGewaehlt)

  function sortieren(neu: BelegSortierung) {
    const neuAbsteigend = neu === sortierung ? !absteigend : true
    setze({
      sortierung: neu === 'Rechnungsdatum' ? undefined : neu,
      absteigend: neuAbsteigend ? undefined : false,
    })
  }

  return (
    <div className="karte overflow-x-auto">
      <table className="tabelle min-w-[1080px]">
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
        <tbody>
          {eintraege.map((beleg) => {
            const gewaehlt = auswahl.istGewaehlt(beleg.id)
            return (
              <tr
                key={beleg.id}
                data-gewaehlt={gewaehlt || undefined}
                className={gewaehlt ? 'bg-akzent-zart' : 'bg-karte'}
              >
                <td>
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
                <td>
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
