import { createContext, useContext, useState, type ReactNode } from 'react'

export type BelegAuswahl = {
  ids: string[]
  istGewaehlt: (id: string) => boolean
  umschalten: (id: string) => void
  alleSetzen: (ids: string[]) => void
  leeren: () => void
}

const AuswahlKontext = createContext<BelegAuswahl | null>(null)

/**
 * Auswahl der Belegliste. `schluessel` beschreibt Filter und Seite: Ändert er sich, ist die Auswahl leer
 * (ein Wechsel der Darstellung behält sie).
 */
export function BelegAuswahlProvider({ schluessel, children }: { schluessel: string; children: ReactNode }) {
  const [stand, setStand] = useState<{ schluessel: string; ids: string[] }>({ schluessel, ids: [] })
  const ids = stand.schluessel === schluessel ? stand.ids : []

  function aendern(neu: (alt: string[]) => string[]) {
    setStand((s) => ({ schluessel, ids: neu(s.schluessel === schluessel ? s.ids : []) }))
  }

  const auswahl: BelegAuswahl = {
    ids,
    istGewaehlt: (id) => ids.includes(id),
    umschalten: (id) => aendern((alt) => (alt.includes(id) ? alt.filter((x) => x !== id) : [...alt, id])),
    alleSetzen: (neu) => aendern(() => [...new Set(neu)]),
    leeren: () => aendern(() => []),
  }
  return <AuswahlKontext.Provider value={auswahl}>{children}</AuswahlKontext.Provider>
}

export function useBelegAuswahl(): BelegAuswahl {
  const auswahl = useContext(AuswahlKontext)
  if (!auswahl) throw new Error('useBelegAuswahl braucht einen BelegAuswahlProvider')
  return auswahl
}
