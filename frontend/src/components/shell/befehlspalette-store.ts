import { useSyncExternalStore } from 'react'

let offen = false
const zuhoerer = new Set<() => void>()

function setzen(wert: boolean): void {
  if (offen === wert) return
  offen = wert
  zuhoerer.forEach((zuhoeren) => zuhoeren())
}

function abonnieren(zuhoeren: () => void): () => void {
  zuhoerer.add(zuhoeren)
  return () => zuhoerer.delete(zuhoeren)
}

const oeffnen = () => setzen(true)
const schliessen = () => setzen(false)

/** Zustand der Befehlspalette (Strg K): geöffnet über die Suche in der Kopfleiste oder das Tastenkürzel. */
export function useBefehlspalette(): { offen: boolean; oeffnen: () => void; schliessen: () => void } {
  const istOffen = useSyncExternalStore(
    abonnieren,
    () => offen,
    () => false,
  )
  return { offen: istOffen, oeffnen, schliessen }
}
