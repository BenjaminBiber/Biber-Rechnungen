import { useEffect, useRef, useState } from 'react'
import { useKorrespondenten } from '@/api/korrespondenten'
import type { PreisaenderungFilter } from '@/api/typen'
import { KorrespondentAuswahl } from '@/components/auswahl/KorrespondentAuswahl'
import { FilterPills, type PillFilter } from '@/components/filter/FilterPills'
import { Icon } from '@/components/Icon'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type { ArtikelSuche } from './suche'

export const ARTIKEL_SUCHE_ENTPRELLT_MS = 300

const preisaenderungText: Record<PreisaenderungFilter, string> = { Gestiegen: 'Gestiegen', Gefallen: 'Gefallen' }

/**
 * Filterzeile der Reiter „Artikel“ und „Stammartikel“: Suche (entprellt) und Filter-Pills für Korrespondent und
 * Preisänderung; im Reiter „Artikel“ zusätzlich „Zuordnung: ohne Stammartikel“.
 */
export function ArtikelFilter({
  suche,
  setze,
  reiter = 'artikel',
}: {
  suche: ArtikelSuche
  setze: (teil: Partial<ArtikelSuche>) => void
  reiter?: 'artikel' | 'stammartikel'
}) {
  const korrespondenten = useKorrespondenten()
  const korrespondentName = suche.korrespondentId
    ? (korrespondenten.data?.find((k) => k.id === suche.korrespondentId)?.name ?? '1 gewählt')
    : ''

  const filter: PillFilter[] = [
    {
      schluessel: 'korrespondent',
      titel: 'Korrespondent',
      icon: 'storefront',
      wert: korrespondentName,
      entfernen: () => setze({ korrespondentId: undefined }),
      auswahl: (ausloeser, anfangsOffen) => (
        <KorrespondentAuswahl
          anfangsOffen={anfangsOffen}
          wert={suche.korrespondentId ?? null}
          onAendern={(id) => setze({ korrespondentId: id === suche.korrespondentId ? undefined : id })}
          ausloeser={ausloeser}
        />
      ),
    },
    {
      schluessel: 'preisaenderung',
      titel: 'Preisänderung',
      icon: 'trending_up',
      wert: suche.preisaenderung ? preisaenderungText[suche.preisaenderung] : '',
      entfernen: () => setze({ preisaenderung: undefined }),
      auswahl: (ausloeser, anfangsOffen) => (
        <DropdownMenu defaultOpen={anfangsOffen}>
          <DropdownMenuTrigger asChild>{ausloeser}</DropdownMenuTrigger>
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
      ),
    },
  ]
  if (reiter === 'artikel')
    filter.push({
      schluessel: 'zuordnung',
      titel: 'Zuordnung',
      icon: 'category',
      wert: suche.ohneStammartikel ? 'ohne Stammartikel' : '',
      entfernen: () => setze({ ohneStammartikel: undefined }),
      auswahl: (ausloeser, anfangsOffen) => (
        <DropdownMenu defaultOpen={anfangsOffen}>
          <DropdownMenuTrigger asChild>{ausloeser}</DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            <DropdownMenuRadioGroup
              value={suche.ohneStammartikel ? 'ohne' : ''}
              onValueChange={(w) => setze({ ohneStammartikel: w === 'ohne' || undefined })}
            >
              <DropdownMenuRadioItem value="ohne">Ohne Stammartikel</DropdownMenuRadioItem>
              <DropdownMenuSeparator />
              <DropdownMenuRadioItem value="">Alle</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    })

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Suchfeld suche={suche} setze={setze} label={reiter === 'artikel' ? 'Artikel suchen' : 'Stammartikel suchen'} />
      <FilterPills
        filter={filter}
        weitereAktiv={!!suche.suche}
        onZuruecksetzen={() =>
          setze({
            suche: undefined,
            korrespondentId: undefined,
            preisaenderung: undefined,
            ohneStammartikel: undefined,
          })
        }
      />
    </div>
  )
}

/** Schreibt den Suchbegriff erst nach 300 ms Ruhe in die URL. */
function Suchfeld({
  suche,
  setze,
  label,
}: {
  suche: ArtikelSuche
  setze: (teil: Partial<ArtikelSuche>) => void
  label: string
}) {
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
    <div className="flex h-[34px] max-w-[360px] min-w-0 flex-[1_1_260px] items-center gap-1.5 rounded-[7px] border border-akzent bg-karte px-2.5">
      <Icon name="search" className="text-[18px] text-gedaempft" />
      <input
        type="search"
        aria-label={label}
        placeholder={label}
        value={text}
        onChange={(e) => eingeben(e.target.value)}
        className="min-w-0 flex-1 border-0 bg-transparent text-sm text-text outline-none"
      />
    </div>
  )
}
