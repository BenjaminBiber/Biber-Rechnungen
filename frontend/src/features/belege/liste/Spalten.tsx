import { useState } from 'react'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { MenueKnopf } from './bausteine'
import { spaltenDefinitionen, type SpaltenSchluessel } from './spaltenDefinitionen'

/**
 * Popover „Spalten anpassen“: Checkbox je Spalte (Korrespondent fest), Reihenfolge per Pfeiltasten am
 * Griff oder per Ziehen. Angezeigt werden erst die sichtbaren Spalten in ihrer Reihenfolge, dann die übrigen.
 */
export function Spalten({
  spalten,
  onAendern,
}: {
  spalten: SpaltenSchluessel[]
  onAendern: (spalten: SpaltenSchluessel[]) => void
}) {
  const [gezogen, setGezogen] = useState<SpaltenSchluessel | null>(null)
  const reihenfolge = [...spalten, ...spaltenDefinitionen.map((d) => d.schluessel).filter((s) => !spalten.includes(s))]

  function sichtbarNach(liste: SpaltenSchluessel[], sichtbar: SpaltenSchluessel[]) {
    onAendern(liste.filter((s) => sichtbar.includes(s)))
  }

  function umschalten(schluessel: SpaltenSchluessel) {
    if (spalten.includes(schluessel)) onAendern(spalten.filter((s) => s !== schluessel))
    else onAendern([...spalten, schluessel])
  }

  function verschieben(schluessel: SpaltenSchluessel, ziel: number) {
    const von = reihenfolge.indexOf(schluessel)
    if (ziel < 0 || ziel >= reihenfolge.length || ziel === von) return
    const neu = reihenfolge.filter((s) => s !== schluessel)
    neu.splice(ziel, 0, schluessel)
    sichtbarNach(neu, spalten)
  }

  return (
    <Popover>
      <PopoverTrigger asChild>
        <MenueKnopf icon="view_column" label="Anzeigen" aria-haspopup="dialog" />
      </PopoverTrigger>
      <PopoverContent role="dialog" aria-label="Spalten" className="w-[260px] p-1.5">
        <ul className="m-0 list-none p-0">
          {reihenfolge.map((schluessel, index) => {
            const definition = spaltenDefinitionen.find((d) => d.schluessel === schluessel)!
            const sichtbar = spalten.includes(schluessel)
            return (
              <li
                key={schluessel}
                draggable
                onDragStart={() => setGezogen(schluessel)}
                onDragOver={(e) => e.preventDefault()}
                onDrop={() => {
                  if (gezogen) verschieben(gezogen, index)
                  setGezogen(null)
                }}
                className="flex h-[38px] items-center gap-2.5 rounded-md px-2.5 text-sm hover:bg-hover"
              >
                <button
                  type="button"
                  aria-label={`${definition.label} verschieben (Pfeiltasten)`}
                  className="flex cursor-grab items-center border-0 bg-transparent p-0 text-[#9AA4B5]"
                  disabled={!sichtbar}
                  onKeyDown={(e) => {
                    if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
                      e.preventDefault()
                      verschieben(schluessel, index + (e.key === 'ArrowUp' ? -1 : 1))
                    }
                  }}
                >
                  <Icon name="drag_indicator" />
                </button>
                <label className="flex flex-1 cursor-pointer items-center gap-2.5">
                  <input
                    type="checkbox"
                    className="h-4 w-4"
                    checked={sichtbar}
                    disabled={definition.fest}
                    onChange={() => umschalten(schluessel)}
                  />
                  {definition.label}
                </label>
              </li>
            )
          })}
        </ul>
      </PopoverContent>
    </Popover>
  )
}
