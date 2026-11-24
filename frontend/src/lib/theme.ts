import { useCallback, useEffect, useSyncExternalStore } from 'react'

/** Wahl im Benutzermenü: Hell, Dunkel oder wie das System. */
export type Darstellung = 'hell' | 'dunkel' | 'system'

export const DARSTELLUNG_SCHLUESSEL = 'darstellung'
const DUNKEL_ABFRAGE = '(prefers-color-scheme: dark)'

const zuhoerer = new Set<() => void>()
// Ersatz, falls der localStorage nicht verfügbar ist (z. B. privater Modus)
let sitzungsWert: Darstellung = 'system'

function istDarstellung(wert: unknown): wert is Darstellung {
  return wert === 'hell' || wert === 'dunkel' || wert === 'system'
}

/** Gespeicherte Darstellung aus dem localStorage, sonst „system“. */
export function leseDarstellung(): Darstellung {
  try {
    const wert = window.localStorage.getItem(DARSTELLUNG_SCHLUESSEL)
    return istDarstellung(wert) ? wert : 'system'
  } catch {
    return sitzungsWert
  }
}

function systemIstDunkel(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia(DUNKEL_ABFRAGE).matches
}

/** Setzt `data-theme` („light“ oder „dark“) am `<html>`-Element. */
export function wendeDarstellungAn(darstellung: Darstellung = leseDarstellung()): void {
  const dunkel = darstellung === 'dunkel' || (darstellung === 'system' && systemIstDunkel())
  document.documentElement.dataset.theme = dunkel ? 'dark' : 'light'
}

function abonnieren(zuhoeren: () => void): () => void {
  zuhoerer.add(zuhoeren)
  return () => zuhoerer.delete(zuhoeren)
}

/**
 * Aktuelle Darstellung und Setter. Speichert im localStorage unter `darstellung`
 * und hält `data-theme` am `<html>` aktuell, bei „system“ auch bei Systemwechsel.
 */
export function useDarstellung(): [Darstellung, (d: Darstellung) => void] {
  const darstellung = useSyncExternalStore(abonnieren, leseDarstellung, () => 'system' as const)

  useEffect(() => {
    wendeDarstellungAn(darstellung)
    if (darstellung !== 'system' || typeof window.matchMedia !== 'function') return
    const abfrage = window.matchMedia(DUNKEL_ABFRAGE)
    const beiWechsel = () => wendeDarstellungAn('system')
    abfrage.addEventListener('change', beiWechsel)
    return () => abfrage.removeEventListener('change', beiWechsel)
  }, [darstellung])

  const setzen = useCallback((neu: Darstellung) => {
    sitzungsWert = neu
    try {
      window.localStorage.setItem(DARSTELLUNG_SCHLUESSEL, neu)
    } catch {
      // localStorage nicht verfügbar: gilt nur für diese Sitzung
    }
    wendeDarstellungAn(neu)
    zuhoerer.forEach((zuhoeren) => zuhoeren())
  }, [])

  return [darstellung, setzen]
}
