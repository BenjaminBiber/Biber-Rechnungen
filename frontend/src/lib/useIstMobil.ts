import { useSyncExternalStore } from 'react'

const MOBIL_ABFRAGE = '(max-width: 1023px)'

function abfrage(): MediaQueryList | null {
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    ? window.matchMedia(MOBIL_ABFRAGE)
    : null
}

function abonnieren(zuhoeren: () => void): () => void {
  const liste = abfrage()
  liste?.addEventListener('change', zuhoeren)
  return () => liste?.removeEventListener('change', zuhoeren)
}

/** `true` unterhalb von 1024 px Breite (mobile Kopfzeile und Bottom-Navigation statt Seitenleiste). */
export function useIstMobil(): boolean {
  return useSyncExternalStore(
    abonnieren,
    () => abfrage()?.matches ?? false,
    () => false,
  )
}
