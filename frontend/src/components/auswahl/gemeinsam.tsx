import { useEffect, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Icon } from '@/components/Icon'

/** Liefert `wert` erst, wenn er sich `ms` Millisekunden nicht mehr geändert hat. */
export function useEntprellt<T>(wert: T, ms = 250): T {
  const [entprellt, setEntprellt] = useState(wert)
  useEffect(() => {
    const zeitgeber = setTimeout(() => setEntprellt(wert), ms)
    return () => clearTimeout(zeitgeber)
  }, [wert, ms])
  return entprellt
}

/**
 * Tastaturbedienung einer Listbox über das Suchfeld: Pfeiltasten bewegen die Markierung, Enter wählt.
 * `waehlbar(i)` überspringt deaktivierte Einträge.
 */
export function useListenNavigation(
  anzahl: number,
  onWaehlen: (index: number) => void,
  waehlbar = (_: number) => true,
) {
  const [aktiv, setAktiv] = useState(-1)
  const aktivSicher = aktiv < anzahl ? aktiv : -1

  function bewegen(schritt: 1 | -1) {
    // Ohne Markierung beginnt ↓ beim ersten, ↑ beim letzten Eintrag.
    const start = aktivSicher >= 0 ? aktivSicher : schritt === 1 ? -1 : anzahl
    for (let i = 1; i <= anzahl; i++) {
      const kandidat = (((start + schritt * i) % anzahl) + anzahl) % anzahl
      if (waehlbar(kandidat)) return setAktiv(kandidat)
    }
  }

  function onKeyDown(e: KeyboardEvent) {
    if (anzahl === 0) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      bewegen(1)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      bewegen(-1)
    } else if (e.key === 'Enter' && aktivSicher >= 0) {
      e.preventDefault()
      onWaehlen(aktivSicher)
    }
  }

  return { aktiv: aktivSicher, setAktiv, onKeyDown }
}

/** Suchzeile oben im Popover. */
export function Suchfeld({
  wert,
  onAendern,
  platzhalter,
  onKeyDown,
  aktivId,
  listId,
}: {
  wert: string
  onAendern: (text: string) => void
  platzhalter: string
  onKeyDown: (e: KeyboardEvent) => void
  aktivId?: string
  listId: string
}) {
  return (
    <div className="-mx-3 -mt-3 mb-2 flex h-10 items-center gap-2 border-b border-rand px-3">
      <Icon name="search" className="gedaempft" />
      <input
        className="min-w-0 flex-1 bg-transparent outline-none"
        value={wert}
        placeholder={platzhalter}
        aria-label={platzhalter}
        role="combobox"
        aria-expanded="true"
        aria-controls={listId}
        aria-activedescendant={aktivId}
        onChange={(e) => onAendern(e.target.value)}
        onKeyDown={onKeyDown}
      />
    </div>
  )
}

/** Eintrag einer Listbox (Optik `.mi` des Designs). */
export function Option({
  id,
  gewaehlt,
  aktiv,
  deaktiviert,
  onWaehlen,
  children,
}: {
  id: string
  gewaehlt: boolean
  aktiv: boolean
  deaktiviert?: boolean
  onWaehlen: () => void
  children: ReactNode
}) {
  return (
    <div
      id={id}
      role="option"
      aria-selected={gewaehlt}
      aria-disabled={deaktiviert || undefined}
      className={`flex h-[38px] cursor-pointer items-center gap-2.5 rounded-md px-2.5 text-sm ${
        aktiv ? 'bg-hover' : ''
      } ${deaktiviert ? 'cursor-default text-gedaempft' : ''}`}
      // Tastatur läuft über das Suchfeld (aria-activedescendant); die Option ist nur per Klick fokussierbar.
      tabIndex={-1}
      onClick={deaktiviert ? undefined : onWaehlen}
      onKeyDown={(e) => {
        if (!deaktiviert && (e.key === 'Enter' || e.key === ' ')) {
          e.preventDefault()
          onWaehlen()
        }
      }}
    >
      {children}
    </div>
  )
}
