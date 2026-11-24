import { Link } from '@tanstack/react-router'
import { useEffect, useRef, useState } from 'react'
import type { BelegListeneintrag } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { ansichtTitel, zuAbfrage, type ListenZustand } from '@/features/belege/liste/suche'
import { formatDatum, formatEuro } from '@/lib/format'
import { cn } from '@/lib/utils'
import { FilterSheet, type FilterAuswahl } from './FilterSheet'
import { useBelegeEndlos } from './useBelegeEndlos'

/** Wartezeit, bevor die Eingabe im Suchfeld als Suche übernommen wird. */
const ENTPRELLUNG_MS = 300

/** Mobile Belegliste: Suche, Filter-Sheet und endlos nachladende Liste; Zustand in denselben Suchparametern wie am Desktop. */
export function MobilBelege({ zustand }: { zustand: ListenZustand }) {
  const { suche, setze } = zustand
  const [sheetOffen, setSheetOffen] = useState(false)
  const { seite: _seite, seitengroesse: _groesse, ...filter } = zuAbfrage(suche)
  const liste = useBelegeEndlos(filter)
  const eintraege = liste.data?.pages.flatMap((s) => s.eintraege) ?? []

  const aktuell: FilterAuswahl = {
    ansicht: suche.ansicht ?? 'Alle',
    sortierung: suche.sortierung === 'Brutto' ? 'Brutto' : 'Rechnungsdatum',
  }
  const aktiv = (aktuell.ansicht !== 'Alle' ? 1 : 0) + (aktuell.sortierung !== 'Rechnungsdatum' ? 1 : 0)

  return (
    <main className="flex w-full min-w-0 flex-col text-text">
      <h1 className="sr-only">{ansichtTitel[aktuell.ansicht]}</h1>
      <div className="sticky top-0 z-10 flex items-center gap-2 border-b border-linie bg-flaeche px-4 py-2.5">
        <Suchfeld wert={suche.suche ?? ''} onSuche={(s) => setze({ suche: s || undefined })} />
        <button
          type="button"
          aria-label={aktiv ? `Filter, ${aktiv} aktiv` : 'Filter'}
          aria-haspopup="dialog"
          onClick={() => setSheetOffen(true)}
          className={cn(
            'relative flex size-11 flex-none cursor-pointer items-center justify-center rounded-[10px] border border-akzent',
            aktiv ? 'bg-akzent text-auf-akzent' : 'bg-karte text-akzent-text',
          )}
        >
          <Icon name="filter_alt" />
          {aktiv > 0 && (
            <span
              aria-hidden="true"
              className="absolute -top-1.5 -right-1.5 flex h-[18px] min-w-[18px] items-center justify-center rounded-[9px] border-2 border-karte bg-orange-text px-1 text-[11px] font-semibold text-white"
            >
              {aktiv}
            </span>
          )}
        </button>
      </div>

      {liste.isPending && (
        <p role="status" className="gedaempft px-4 py-5">
          Belege werden geladen …
        </p>
      )}
      {liste.isError && !liste.data && (
        <div role="alert" className="flex flex-col items-start gap-3 px-4 py-5">
          <p className="m-0 text-sm">Die Belege konnten nicht geladen werden.</p>
          <button type="button" className="btn" onClick={() => void liste.refetch()}>
            <Icon name="refresh" />
            Erneut versuchen
          </button>
        </div>
      )}
      {liste.data && eintraege.length === 0 && <p className="gedaempft px-4 py-5">Keine Belege für diese Filter</p>}

      <ul className="m-0 list-none p-0" aria-label="Belege">
        {eintraege.map((b) => (
          <li key={b.id}>
            <Zeile beleg={b} />
          </li>
        ))}
      </ul>
      {liste.hasNextPage && (
        <Nachlader laedt={liste.isFetchingNextPage} onNachladen={() => void liste.fetchNextPage()} />
      )}

      <FilterSheet
        offen={sheetOffen}
        aktuell={aktuell}
        suche={suche.suche}
        onSchliessen={() => setSheetOffen(false)}
        onUebernehmen={(a) => {
          setSheetOffen(false)
          setze({
            ansicht: a.ansicht === 'Alle' ? undefined : a.ansicht,
            sortierung: a.sortierung === 'Brutto' ? 'Brutto' : undefined,
          })
        }}
      />
    </main>
  )
}

function Suchfeld({ wert, onSuche }: { wert: string; onSuche: (suche: string) => void }) {
  const [eingabe, setEingabe] = useState(wert)
  const [bisher, setBisher] = useState(wert)
  const melden = useRef(onSuche)
  useEffect(() => {
    melden.current = onSuche
  })

  // Von außen geänderte Suche (Zurück-Taste) übernehmen.
  if (wert !== bisher) {
    setBisher(wert)
    setEingabe(wert)
  }

  useEffect(() => {
    if (eingabe.trim() === wert) return
    const zeit = setTimeout(() => melden.current(eingabe.trim()), ENTPRELLUNG_MS)
    return () => clearTimeout(zeit)
  }, [eingabe, wert])

  return (
    <label className="flex h-11 min-w-0 flex-1 items-center gap-2 rounded-[10px] border border-eingabe-rand bg-karte px-3">
      <span className="text-gedaempft">
        <Icon name="search" />
      </span>
      <input
        type="search"
        aria-label="Belege durchsuchen"
        placeholder="Volltext durchsuchen"
        value={eingabe}
        onChange={(e) => setEingabe(e.target.value)}
        className="min-w-0 flex-1 border-0 bg-transparent text-[15px] text-text outline-none"
      />
    </label>
  )
}

function Zeile({ beleg }: { beleg: BelegListeneintrag }) {
  const tags = beleg.tags.slice(0, 2)
  const weitere = beleg.tags.slice(2)
  const weitereText = weitere.map((t) => t.name).join(', ')
  const unterzeile =
    beleg.ueberfaellig && beleg.faelligkeitsdatum
      ? `fällig ${formatDatum(beleg.faelligkeitsdatum)}`
      : [beleg.rechnungsdatum && formatDatum(beleg.rechnungsdatum), beleg.kategorie?.name].filter(Boolean).join(' · ')

  return (
    <Link
      to="/belege/$belegId"
      params={{ belegId: beleg.id }}
      className="flex min-h-11 gap-3 border-b border-linie px-4 py-3 text-text no-underline hover:text-text"
    >
      <span className="flex h-[64px] w-[48px] flex-none items-center justify-center overflow-hidden rounded-md bg-segment">
        {beleg.vorschauUrl ? (
          <img src={beleg.vorschauUrl} alt="" loading="lazy" className="h-full w-full bg-white object-contain" />
        ) : (
          <span aria-hidden="true" className="flex h-[52px] w-[38px] flex-col gap-[3px] bg-white p-1.5 shadow-blatt">
            <span className="h-[3px] w-3/4 rounded-[1px] bg-akzent opacity-55" />
            <span className="h-[3px] w-1/2 rounded-[1px] bg-[#DDE1E7]" />
            <span className="h-[3px] w-2/3 rounded-[1px] bg-[#DDE1E7]" />
            <span className="h-[3px] w-full rounded-[1px] bg-[#DDE1E7]" />
          </span>
        )}
      </span>
      <span className="grid min-w-0 flex-1 grid-cols-[minmax(0,1fr)_auto] gap-x-2 gap-y-0.5">
        <span className="truncate text-[15px] font-medium">{beleg.korrespondent?.name ?? beleg.titel}</span>
        <span className="num text-[15px] font-semibold">
          {beleg.brutto != null ? formatEuro(beleg.brutto, beleg.waehrung) : '–'}
        </span>
        <span className={cn('col-span-2 truncate text-[13px]', beleg.ueberfaellig ? 'text-rot-text' : 'gedaempft')}>
          {unterzeile}
        </span>
        {beleg.tags.length > 0 && (
          <span className="col-span-2 flex flex-wrap gap-1">
            {tags.map((t) => (
              <Chip key={t.id} ton={t.farbe as ChipTon}>
                {t.name}
              </Chip>
            ))}
            {weitere.length > 0 && (
              <span
                title={weitereText}
                aria-label={`${weitere.length} weitere: ${weitereText}`}
                className="chip chip-grau"
              >
                +{weitere.length}
              </span>
            )}
          </span>
        )}
      </span>
    </Link>
  )
}

/** Lädt den nächsten Block, sobald das Listenende sichtbar wird; ohne IntersectionObserver per Knopf. */
function Nachlader({ laedt, onNachladen }: { laedt: boolean; onNachladen: () => void }) {
  const ref = useRef<HTMLDivElement>(null)
  const nachladen = useRef(onNachladen)
  useEffect(() => {
    nachladen.current = onNachladen
  })

  useEffect(() => {
    const element = ref.current
    if (!element || typeof IntersectionObserver === 'undefined') return
    const beobachter = new IntersectionObserver((e) => {
      if (e.some((x) => x.isIntersecting)) nachladen.current()
    })
    beobachter.observe(element)
    return () => beobachter.disconnect()
  }, [])

  return (
    <div ref={ref} className="flex justify-center px-4 py-4">
      <button type="button" className="btn min-h-11" onClick={onNachladen} disabled={laedt}>
        {laedt ? 'Lädt …' : 'Weitere laden'}
      </button>
    </div>
  )
}
