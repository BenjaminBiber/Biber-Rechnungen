import { useEffect, useRef, useState } from 'react'
import type { PreisaenderungFilter } from '@/api/typen'
import { KorrespondentAuswahl } from '@/components/auswahl/KorrespondentAuswahl'
import { Icon } from '@/components/Icon'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { MenueKnopf } from '@/features/belege/liste/bausteine'
import type { ArtikelSuche } from './suche'

export const ARTIKEL_SUCHE_ENTPRELLT_MS = 300

/** Filterzeile: Suche (entprellt), Korrespondent, Preisänderung und „Ohne Stammartikel“. */
export function ArtikelFilter({ suche, setze }: { suche: ArtikelSuche; setze: (teil: Partial<ArtikelSuche>) => void }) {
  return (
    <div role="search" aria-label="Filter" className="karte flex flex-wrap items-center gap-2 p-3">
      <Suchfeld suche={suche} setze={setze} />
      <span className="flex-1" />
      <KorrespondentAuswahl
        wert={suche.korrespondentId ?? null}
        onAendern={(id) => setze({ korrespondentId: id === suche.korrespondentId ? undefined : id })}
        ausloeser={
          <MenueKnopf
            icon="storefront"
            label="Korrespondent"
            aktiv={!!suche.korrespondentId}
            anzahl={suche.korrespondentId ? 1 : 0}
          />
        }
      />
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <MenueKnopf
            icon="trending_up"
            label="Preisänderung"
            aktiv={!!suche.preisaenderung}
            anzahl={suche.preisaenderung ? 1 : 0}
          />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          <DropdownMenuRadioGroup
            value={suche.preisaenderung ?? ''}
            onValueChange={(w) => setze({ preisaenderung: (w || undefined) as PreisaenderungFilter | undefined })}
          >
            <DropdownMenuRadioItem value="Gestiegen">Gestiegen</DropdownMenuRadioItem>
            <DropdownMenuRadioItem value="Gefallen">Gefallen</DropdownMenuRadioItem>
            <DropdownMenuSeparator />
            <DropdownMenuRadioItem value="">Alle</DropdownMenuRadioItem>
          </DropdownMenuRadioGroup>
        </DropdownMenuContent>
      </DropdownMenu>
      <label className="inline-flex cursor-pointer items-center gap-2 text-sm font-medium">
        <input
          type="checkbox"
          role="switch"
          checked={!!suche.ohneStammartikel}
          onChange={(e) => setze({ ohneStammartikel: e.target.checked || undefined })}
          className="size-4 cursor-pointer accent-[var(--color-akzent)]"
        />
        Ohne Stammartikel
      </label>
      {(suche.korrespondentId || suche.preisaenderung || suche.ohneStammartikel || suche.suche) && (
        <button
          type="button"
          onClick={() =>
            setze({
              suche: undefined,
              korrespondentId: undefined,
              preisaenderung: undefined,
              ohneStammartikel: undefined,
            })
          }
          className="inline-flex h-8 cursor-pointer items-center gap-1 rounded-[7px] border border-eingabe-rand bg-karte px-2.5 text-[13px] font-medium text-gedaempft hover:bg-hover"
        >
          <Icon name="close" className="text-[16px]" />
          Filter zurücksetzen
        </button>
      )}
    </div>
  )
}

/** Schreibt den Suchbegriff erst nach 300 ms Ruhe in die URL. */
function Suchfeld({ suche, setze }: { suche: ArtikelSuche; setze: (teil: Partial<ArtikelSuche>) => void }) {
  const [text, setText] = useState(suche.suche ?? '')
  const [stand, setStand] = useState(suche.suche)
  const zeitgeber = useRef<ReturnType<typeof setTimeout>>(undefined)

  if (stand !== suche.suche) {
    setStand(suche.suche)
    setText(suche.suche ?? '')
  }

  useEffect(() => () => clearTimeout(zeitgeber.current), [])

  function eingeben(wert: string) {
    setText(wert)
    clearTimeout(zeitgeber.current)
    zeitgeber.current = setTimeout(() => setze({ suche: wert.trim() || undefined }), ARTIKEL_SUCHE_ENTPRELLT_MS)
  }

  return (
    <div className="flex h-[34px] max-w-[360px] flex-[1_1_260px] items-center gap-1.5 rounded-[7px] border border-akzent bg-karte px-2.5">
      <Icon name="search" className="text-[18px] text-gedaempft" />
      <input
        type="search"
        aria-label="Artikel suchen"
        placeholder="Artikel suchen"
        value={text}
        onChange={(e) => eingeben(e.target.value)}
        className="min-w-0 flex-1 border-0 bg-transparent text-sm text-text outline-none"
      />
    </div>
  )
}
