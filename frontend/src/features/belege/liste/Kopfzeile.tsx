import type { BelegSortierung } from '@/api/typen'
import { Icon } from '@/components/Icon'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { gruppe, MenueKnopf } from './bausteine'
import { Suchzeile } from './Filterleiste'
import { Spalten } from './Spalten'
import type { SpaltenSchluessel } from './spaltenDefinitionen'
import { ansichtTitel, sortierungen, sortierungText, type Darstellung, type ListenZustand } from './suche'

const darstellungKnoepfe: { wert: Darstellung; icon: string; label: string }[] = [
  { wert: 'tabelle', icon: 'table_rows', label: 'Tabelle' },
  { wert: 'klein', icon: 'grid_view', label: 'Kleine Karten' },
  { wert: 'gross', icon: 'view_agenda', label: 'Große Karten' },
]

/**
 * Kopfzeile der Belegliste: Titel der Ansicht mit Suchfeld, Darstellung, Sortierung (nur Kartenansichten)
 * und Spaltenwahl (nur Tabelle). „Seite/Alle auswählen“ sitzt in der Massenaktionen-Leiste.
 */
export function Kopfzeile({
  zustand,
  spalten,
  onSpaltenAendern,
}: {
  zustand: ListenZustand
  spalten: SpaltenSchluessel[]
  onSpaltenAendern: (spalten: SpaltenSchluessel[]) => void
}) {
  const { suche, setze } = zustand
  const darstellung = suche.darstellung ?? 'tabelle'

  return (
    <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
      <div className="flex min-w-0 flex-[1_1_520px] flex-wrap items-center gap-x-5 gap-y-3">
        <h1 className="m-0 flex-none text-[26px] font-medium text-text">{ansichtTitel[suche.ansicht ?? 'Alle']}</h1>
        <Suchzeile zustand={zustand} />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <div className={gruppe} role="radiogroup" aria-label="Darstellung">
          {darstellungKnoepfe.map((k) => {
            const an = darstellung === k.wert
            return (
              <button
                key={k.wert}
                type="button"
                role="radio"
                aria-checked={an}
                aria-label={k.label}
                title={k.label}
                onClick={() => setze({ darstellung: k.wert === 'tabelle' ? undefined : k.wert, seite: suche.seite })}
                className={cn('!w-[38px] !px-0', an && '!bg-akzent !text-auf-akzent')}
              >
                <Icon name={k.icon} className="[vertical-align:0]" />
              </button>
            )
          })}
        </div>
        {/* In der Tabelle sortieren die Spaltenköpfe. */}
        {darstellung !== 'tabelle' && <SortierMenue zustand={zustand} />}
        {/* Spalten gibt es nur in der Tabelle. */}
        {darstellung === 'tabelle' && <Spalten spalten={spalten} onAendern={onSpaltenAendern} />}
      </div>
    </div>
  )
}

function SortierMenue({ zustand: { suche, setze } }: { zustand: ListenZustand }) {
  const sortierung = suche.sortierung ?? 'Rechnungsdatum'
  const absteigend = suche.absteigend ?? true
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <MenueKnopf
          icon="swap_vert"
          label={sortierungText[sortierung]}
          aria-label={`Sortierung: ${sortierungText[sortierung]}, ${absteigend ? 'absteigend' : 'aufsteigend'}`}
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>Sortieren nach</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={sortierung}
          onValueChange={(wert) =>
            setze({ sortierung: wert === 'Rechnungsdatum' ? undefined : (wert as BelegSortierung) })
          }
        >
          {sortierungen.map((s) => (
            <DropdownMenuRadioItem key={s} value={s}>
              {sortierungText[s]}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuRadioGroup
          value={absteigend ? 'ab' : 'auf'}
          onValueChange={(wert) => setze({ absteigend: wert === 'ab' ? undefined : false })}
        >
          <DropdownMenuRadioItem value="auf">Aufsteigend</DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="ab">Absteigend</DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
