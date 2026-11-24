import { useEffect, useId, useRef, useState } from 'react'
import { useKorrespondenten } from '@/api/korrespondenten'
import { useKategorien } from '@/api/tags'
import type { ImportQuelle, Zahlstatus } from '@/api/typen'
import { BetragAuswahl } from '@/components/auswahl/BetragAuswahl'
import { Option, Suchfeld, useListenNavigation } from '@/components/auswahl/gemeinsam'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { ZeitraumAuswahl } from '@/components/auswahl/ZeitraumAuswahl'
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
import { MenueKnopf } from './bausteine'
import {
  filterZurueckgesetzt,
  quelleIcon,
  quellenWerte,
  quelleText,
  zahlstatusText,
  zahlstatusWerte,
  type ListenZustand,
} from './suche'

export const SUCHE_ENTPRELLT_MS = 300

/** Anzahl gesetzter Filter je Knopf (für Plakette und Hervorhebung). */
function anzahl(liste?: unknown[]): number {
  return liste?.length ?? 0
}

/**
 * Filterleiste: Suchfeld mit Umschalter „Titel & Inhalt“ / „Rechnungsnr.“ (entprellt), sieben Filter
 * (Tags, Korrespondent, Kategorie, Rechnungsdatum, Betrag, Zahlstatus, Herkunft) und „Filter zurücksetzen“.
 */
export function Filterleiste({ zustand }: { zustand: ListenZustand }) {
  const { suche, setze, ersetze } = zustand
  const mitBetrag = suche.betragVon !== undefined || suche.betragBis !== undefined

  return (
    <div className="karte flex flex-wrap items-center gap-2 p-3">
      <Suchzeile zustand={zustand} />
      <span className="flex-1" />
      <TagAuswahl
        gewaehlt={suche.tags ?? []}
        onAendern={(ids) => setze({ tags: ids.length ? ids : undefined })}
        ausloeser={<MenueKnopf icon="sell" label="Tags" aktiv={!!suche.tags} anzahl={anzahl(suche.tags)} />}
      />
      <MehrfachAuswahl
        art="korrespondent"
        gewaehlt={suche.korrespondenten ?? []}
        onAendern={(ids) => setze({ korrespondenten: ids.length ? ids : undefined })}
      />
      <MehrfachAuswahl
        art="kategorie"
        gewaehlt={suche.kategorien ?? []}
        onAendern={(ids) => setze({ kategorien: ids.length ? ids : undefined })}
      />
      <ZeitraumAuswahl
        wert={{ von: suche.von ?? null, bis: suche.bis ?? null }}
        onAendern={(z) => setze({ von: z.von ?? undefined, bis: z.bis ?? undefined })}
        ausloeser={
          <MenueKnopf
            icon="calendar_month"
            label="Rechnungsdatum"
            aktiv={!!(suche.von || suche.bis)}
            anzahl={suche.von || suche.bis ? 1 : 0}
          />
        }
      />
      <BetragAuswahl
        wert={{ von: suche.betragVon ?? null, bis: suche.betragBis ?? null, art: suche.betragArt ?? 'Brutto' }}
        onAendern={(b) =>
          setze({
            betragVon: b.von ?? undefined,
            betragBis: b.bis ?? undefined,
            betragArt: b.art === 'Brutto' || (b.von === null && b.bis === null) ? undefined : b.art,
          })
        }
        ausloeser={<MenueKnopf icon="euro" label="Betrag" aktiv={mitBetrag} anzahl={mitBetrag ? 1 : 0} />}
      />
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <MenueKnopf icon="paid" label="Zahlstatus" aktiv={!!suche.zahlstatus} anzahl={suche.zahlstatus ? 1 : 0} />
        </DropdownMenuTrigger>
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
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <MenueKnopf icon="input" label="Herkunft" aktiv={!!suche.quellen} anzahl={anzahl(suche.quellen)} />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          {quellenWerte.map((q) => (
            <DropdownMenuCheckboxItem
              key={q}
              checked={suche.quellen?.includes(q) ?? false}
              onSelect={(e) => e.preventDefault()}
              onCheckedChange={(an) => {
                const alt = suche.quellen ?? []
                const neu: ImportQuelle[] = an ? [...alt, q] : alt.filter((x) => x !== q)
                setze({ quellen: neu.length ? neu : undefined })
              }}
            >
              <Icon name={quelleIcon[q]} />
              {quelleText[q]}
            </DropdownMenuCheckboxItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
      <button
        type="button"
        onClick={() => ersetze(filterZurueckgesetzt(suche))}
        className="inline-flex h-8 cursor-pointer items-center gap-1 rounded-[7px] border border-eingabe-rand bg-karte px-2.5 text-[13px] font-medium text-gedaempft hover:bg-hover"
      >
        <Icon name="close" className="text-[16px]" />
        Filter zurücksetzen
      </button>
    </div>
  )
}

/** Suchfeld mit Feldwahl; schreibt den Begriff erst nach 300 ms Ruhe in die URL. */
function Suchzeile({ zustand: { suche, setze } }: { zustand: ListenZustand }) {
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
    <div className="flex h-[34px] max-w-[460px] flex-[1_1_340px] overflow-hidden rounded-[7px] border border-akzent">
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
}: {
  art: 'korrespondent' | 'kategorie'
  gewaehlt: string[]
  onAendern: (ids: string[]) => void
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
  const label = art === 'korrespondent' ? 'Korrespondent' : 'Kategorie'

  return (
    <Popover onOpenChange={(offen) => !offen && setSuche('')}>
      <PopoverTrigger asChild>
        <MenueKnopf
          icon={art === 'korrespondent' ? 'storefront' : 'category'}
          label={label}
          aktiv={gewaehlt.length > 0}
          anzahl={gewaehlt.length}
        />
      </PopoverTrigger>
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
