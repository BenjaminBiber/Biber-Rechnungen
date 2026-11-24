import { hinzufuegen } from './toast-store'

const toast = {
  /** Erfolg; verschwindet nach 6 s. Mit `rueckgaengig` erscheint der Link „Rückgängig“. */
  erfolg(text: string, { rueckgaengig }: { rueckgaengig?: () => void } = {}): number {
    return hinzufuegen('erfolg', text, rueckgaengig && { text: 'Rückgängig', ausfuehren: rueckgaengig })
  },
  /** Hinweis; verschwindet nach 6 s. */
  info(text: string): number {
    return hinzufuegen('info', text)
  },
  /** Fehler; bleibt stehen, bis er geschlossen wird. Mit `erneut` erscheint der Link „Erneut“. */
  fehler(text: string, { erneut }: { erneut?: () => void } = {}): number {
    return hinzufuegen('fehler', text, erneut && { text: 'Erneut', ausfuehren: erneut })
  },
}

/** Meldungen unten rechts (`ToastBereich` im Layout). */
export function useToast(): typeof toast {
  return toast
}
