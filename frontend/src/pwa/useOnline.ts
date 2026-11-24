import { useSyncExternalStore } from 'react'

function abonnieren(zuhoeren: () => void): () => void {
  window.addEventListener('online', zuhoeren)
  window.addEventListener('offline', zuhoeren)
  return () => {
    window.removeEventListener('online', zuhoeren)
    window.removeEventListener('offline', zuhoeren)
  }
}

/** `navigator.onLine`, aktualisiert über die Ereignisse `online` und `offline`. */
export function useOnline(): boolean {
  return useSyncExternalStore(
    abonnieren,
    () => navigator.onLine,
    () => true,
  )
}
