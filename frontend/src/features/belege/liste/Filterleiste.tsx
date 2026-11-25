import { useEffect, useId, useRef, useState, type ReactElement, type ReactNode } from 'react'
import { useKorrespondenten } from '@/api/korrespondenten'
import { useKategorien } from '@/api/tags'
import type { ImportQuelle, Zahlstatus } from '@/api/typen'
import { BetragAuswahl } from '@/components/auswahl/BetragAuswahl'
import { Option, Suchfeld, useListenNavigation } from '@/components/auswahl/gemeinsam'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { ZeitraumAuswahl } from '@/components/auswahl/ZeitraumAuswahl'
import { FilterPills, type PillFilter } from '@/components/filter/FilterPills'
import { Icon } from '@/components/Icon'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { filterWert, useNamen, type Namen } from './filterWerte'
import {
  filterZurueckgesetzt,
  quelleIcon,
  quellenWerte,
  quelleText,
  zahlstatusText,
  zahlstatusWerte,
  type BelegeSuche,
  type ListenZustand,
} from './suche'

export const SUCHE_ENTPRELLT_MS = 300

type LeistenFilter = 'tags' | 'korrespondent' | 'kategorie' | 'datum' | 'betrag' | 'zahlstatus' | 'herkunft'

/** Filter der Leiste in der Reihenfolge des Menüs „Filter hinzufügen“. */
const leistenFilter: LeistenFilter[] = [
  'tags',
  'korrespondent',
  'kategorie',
  'datum',
  'betrag',
  'zahlstatus',
  'herkunft',
]

const filterInfo: Record<LeistenFilter, { titel: string; icon: string }> = {
  tags: { titel: 'Tags', icon: 'sell' },
  korrespondent: { titel: 'Korrespondent', icon: 'storefront' },
  kategorie: { titel: 'Kategorie', icon: 'category' },
  datum: { titel: 'Rechnungsdatum', icon: 'calendar_month' },
  betrag: { titel: 'Betrag', icon: 'euro' },
  zahlstatus: { titel: 'Zahlstatus', icon: 'paid' },
  herkunft: { titel: 'Herkunft', icon: 'input' },
}

/** Kurztext des gesetzten Werts („Hostwerk Süd GmbH“, „2 gewählt“, „Netto ab 200 €“ …), leer wenn nicht gesetzt. */
function wertVon(art: LeistenFilter, suche: BelegeSuche, namen: Namen, heute: Date): string {
  if (art === 'betrag') {
    const netto = suche.betragArt === 'Netto'
    const text = filterWert(netto ? 'netto' : 'brutto', suche, namen, heute)
    return text && netto ? `Netto ${text}` : text
  }
  return filterWert(art, suche, namen, heute)
}

/** Suchparameter, die das Entfernen einer Filter-Pill löscht. */
function ohneFilter(art: LeistenFilter): Partial<BelegeSuche> {
  switch (art) {
    case 'tags':
      return { tags: undefined }
    case 'korrespondent':
      return { korrespondenten: undefined }
    case 'kategorie':
      return { kategorien: undefined }
    case 'datum':
      return { von: undefined, bis: undefined }
    case 'betrag':
      return { betragVon: undefined, betragBis: undefined, betragArt: undefined }
    case 'zahlstatus':
      return { zahlstatus: undefined }
    case 'herkunft':
      return { quellen: undefined }
  }
}

/** Filterleiste aller Darstellungen (Tabelle und Karten) als Filter-Pills. */
export function Filterleiste({ zustand, heute = new Date() }: { zustand: ListenZustand; heute?: Date }) {
  const { suche, setze, ersetze } = zustand
  const namen = useNamen()

  function auswahl(art: LeistenFilter, ausloeser: ReactElement, anfangsOffen: boolean) {
    switch (art) {
      case 'tags':
        return (
          <TagAuswahl
            systemTagsWaehlbar
            anfangsOffen={anfangsOffen}
            gewaehlt={suche.tags ?? []}
            onAendern={(ids) => setze({ tags: ids.length ? ids : undefined })}
            ausloeser={ausloeser}
          />
        )
      case 'korrespondent':
        return (
          <MehrfachAuswahl
            art="korrespondent"
            anfangsOffen={anfangsOffen}
            gewaehlt={suche.korrespondenten ?? []}
            onAendern={(ids) => setze({ korrespondenten: ids.length ? ids : undefined })}
            ausloeser={ausloeser}
          />
        )
      case 'kategorie':
        return (
          <MehrfachAuswahl
            art="kategorie"
            anfangsOffen={anfangsOffen}
            gewaehlt={suche.kategorien ?? []}
            onAendern={(ids) => setze({ kategorien: ids.length ? ids : undefined })}
            ausloeser={ausloeser}
          />
        )
      case 'datum':
        return (
          <ZeitraumAuswahl
            anfangsOffen={anfangsOffen}
            wert={{ von: suche.von ?? null, bis: suche.bis ?? null }}
            onAendern={(z) => setze({ von: z.von ?? undefined, bis: z.bis ?? undefined })}
            ausloeser={ausloeser}
          />
        )
      case 'betrag':
        return (
          <BetragAuswahl
            anfangsOffen={anfangsOffen}
            wert={{ von: suche.betragVon ?? null, bis: suche.betragBis ?? null, art: suche.betragArt ?? 'Brutto' }}
            onAendern={(b) =>
              setze({
                betragVon: b.von ?? undefined,
                betragBis: b.bis ?? undefined,
                betragArt: b.art === 'Brutto' || (b.von === null && b.bis === null) ? undefined : b.art,
              })
            }
            ausloeser={ausloeser}
          />
        )
      case 'zahlstatus':
        return (
          <DropdownMenu defaultOpen={anfangsOffen}>
            <DropdownMenuTrigger asChild>{ausloeser}</DropdownMenuTrigger>
            <DropdownMenuContent align="start">
              <DropdownMenuRadioGroup
                value={suche.zahlstatus ?? ''}
                onValueChange={(wert) => setze({ zahlstatus: (wert || undefined) as Zahlstatus | undefined })}
              >
                {zahlstatusWerte.map((z) => (
                  <DropdownMenuRadioItem key={z} value={z}>
                    {zahlstatusText[z]}
                  </DropdownMenuRadioItem>
                ))}
                <DropdownMenuSeparator />
                <DropdownMenuRadioItem value="">Alle</DropdownMenuRadioItem>
              </DropdownMenuRadioGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        )
      case 'herkunft':
        return (
          <DropdownMenu defaultOpen={anfangsOffen}>
            <DropdownMenuTrigger asChild>{ausloeser}</DropdownMenuTrigger>
            <DropdownMenuContent align="start">
              {quellenWerte.map((q) => (
                <DropdownMenuCheckboxItem
                  key={q}
                  checked={suche.quellen?.includes(q) ?? false}
                  onSelect={(e) => e.preventDefault()}
                  onCheckedChange={(an) => {
                    const alt = suche.quellen ?? []
                    const liste: ImportQuelle[] = an ? [...alt, q] : alt.filter((x) => x !== q)
                    setze({ quellen: liste.length ? liste : undefined })
                  }}
                >
                  <Icon name={quelleIcon[q]} />
                  {quelleText[q]}
                </DropdownMenuCheckboxItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        )
    }
  }

  return (
    <FilterPills
      filter={leistenFilter.map((art): PillFilter => ({
        schluessel: art,
        ...filterInfo[art],
        wert: wertVon(art, suche, namen, heute),
        auswahl: (ausloeser, anfangsOffen) => auswahl(art, ausloeser, anfangsOffen),
        entfernen: () => setze(ohneFilter(art)),
      }))}
      weitereAktiv={!!suche.suche}
      onZuruecksetzen={() => ersetze(filterZurueckgesetzt(suche))}
    />
  )
}

/** Suchfeld mit Feldwahl (Kopfzeile); schreibt den Begriff erst nach 300 ms Ruhe in die URL. */
export function Suchzeile({ zustand: { suche, setze } }: { zustand: ListenZustand }) {
  const suchfeld = suche.suchfeld ?? 'TitelInhalt'
  const [text, setText] = useState(suche.suche ?? '')
  const [stand, setStand] = useState(suche.suche)
  const zeitgeber = useRef<ReturnType<typeof setTimeout>>(undefined)

  // Von außen geänderte Suche (Zurücksetzen, Zurück-Taste, gespeicherte Ansicht) ins Feld übernehmen.
  if (stand !== suche.suche) {
    setStand(suche.suche)
    setText(suche.suche ?? '')
  }

  useEffect(() => () => clearTimeout(zeitgeber.current), [])

  function eingeben(wert: string) {
    setText(wert)
    clearTimeout(zeitgeber.current)
    zeitgeber.current = setTimeout(() => setze({ suche: wert.trim() || undefined }), SUCHE_ENTPRELLT_MS)
  }

  const label = suchfeld === 'Rechnungsnummer' ? 'Rechnungsnr.' : 'Titel & Inhalt'
  return (
    <div
      role="search"
      aria-label="Belege durchsuchen"
      className="flex h-[34px] max-w-[520px] min-w-0 flex-[1_1_320px] overflow-hidden rounded-[7px] border border-akzent"
    >
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            aria-label={`Suchfeld: ${label}`}
            className="inline-flex cursor-pointer items-center gap-0.5 border-0 border-r border-akzent bg-karte pr-1.5 pl-2.5 text-sm font-medium whitespace-nowrap text-akzent-text"
          >
            {label}
            <Icon name="arrow_drop_down" className="text-[18px]" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          <DropdownMenuRadioGroup
            value={suchfeld}
            onValueChange={(wert) => setze({ suchfeld: wert === 'Rechnungsnummer' ? 'Rechnungsnummer' : undefined })}
          >
            <DropdownMenuRadioItem value="TitelInhalt">Titel &amp; Inhalt</DropdownMenuRadioItem>
            <DropdownMenuRadioItem value="Rechnungsnummer">Rechnungsnr.</DropdownMenuRadioItem>
          </DropdownMenuRadioGroup>
        </DropdownMenuContent>
      </DropdownMenu>
      <input
        type="search"
        aria-label={suchfeld === 'Rechnungsnummer' ? 'Rechnungsnummer durchsuchen' : 'Titel und Inhalt durchsuchen'}
        placeholder="z. B. Toner, Rechnungsnr. oder Betrag"
        value={text}
        onChange={(e) => eingeben(e.target.value)}
        className="min-w-0 flex-1 border-0 bg-karte px-2.5 text-sm text-text outline-none"
      />
    </div>
  )
}

/** Mehrfachauswahl für Korrespondenten (mit Suche) und Kategorien. */
function MehrfachAuswahl({
  art,
  gewaehlt,
  onAendern,
  ausloeser,
  anfangsOffen = false,
}: {
  art: 'korrespondent' | 'kategorie'
  gewaehlt: string[]
  onAendern: (ids: string[]) => void
  ausloeser: ReactNode
  anfangsOffen?: boolean
}) {
  const listId = useId()
  const [suche, setSuche] = useState('')
  const korrespondenten = useKorrespondenten()
  const kategorien = useKategorien()
  const eintraege = (art === 'korrespondent' ? korrespondenten.data : kategorien.data) ?? []
  const begriff = suche.trim().toLocaleLowerCase('de')
  const treffer = eintraege.filter((e) => e.name.toLocaleLowerCase('de').includes(begriff))

  function umschalten(id: string) {
    onAendern(gewaehlt.includes(id) ? gewaehlt.filter((g) => g !== id) : [...gewaehlt, id])
  }

  const navigation = useListenNavigation(treffer.length, (i) => umschalten(treffer[i].id))

  return (
    <Popover defaultOpen={anfangsOffen} onOpenChange={(offen) => !offen && setSuche('')}>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
      <PopoverContent className="w-80 p-3">
        <Suchfeld
          wert={suche}
          onAendern={setSuche}
          platzhalter={art === 'korrespondent' ? 'Korrespondent suchen' : 'Kategorie suchen'}
          onKeyDown={navigation.onKeyDown}
          listId={listId}
          aktivId={navigation.aktiv >= 0 ? `${listId}-${navigation.aktiv}` : undefined}
        />
        <div
          id={listId}
          role="listbox"
          aria-label={art === 'korrespondent' ? 'Korrespondenten' : 'Kategorien'}
          aria-multiselectable="true"
          className="max-h-72 overflow-y-auto"
        >
          {treffer.map((e, i) => {
            const an = gewaehlt.includes(e.id)
            return (
              <Option
                key={e.id}
                id={`${listId}-${i}`}
                gewaehlt={an}
                aktiv={navigation.aktiv === i}
                onWaehlen={() => umschalten(e.id)}
              >
                <Icon name={an ? 'check_box' : 'check_box_outline_blank'} />
                <span className="truncate">{e.name}</span>
              </Option>
            )
          })}
          {treffer.length === 0 && <p className="m-0 px-2.5 py-2 text-sm text-gedaempft">Keine Treffer</p>}
        </div>
      </PopoverContent>
    </Popover>
  )
}
