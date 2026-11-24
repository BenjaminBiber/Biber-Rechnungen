import { useEffect, useRef } from 'react'
import type { Befehl } from './befehle'

/** Zeit, in der nach „G“ der zweite Buchstabe kommen muss. */
export const FOLGE_MS = 1000

/** Eingabefeld, `contenteditable` oder offener Dialog hat den Fokus – dann wirken Einzeltasten nicht. */
function fokusBlockiert(ziel: EventTarget | null): boolean {
  const el = ziel instanceof HTMLElement ? ziel : document.activeElement
  if (!(el instanceof HTMLElement)) return false
  if (el.isContentEditable || el.closest('input, textarea, select, [contenteditable="true"]')) return true
  return !!el.closest('[role="dialog"], [role="alertdialog"]') || !!document.querySelector('[role="dialog"]')
}

/**
 * Globale Tastaturkürzel: Strg K / Cmd K schaltet die Palette (auch aus Eingabefeldern),
 * Einzeltasten und Folgen wie „G P“ führen den Befehl mit diesem Kürzel aus.
 */
export function useGlobaleKuerzel(optionen: {
  umschalten: () => void
  befehle: Befehl[]
  ausfuehren: (befehl: Befehl) => void
}): void {
  const aktuell = useRef(optionen)
  useEffect(() => {
    aktuell.current = optionen
  })
  const folge = useRef<{ taste: string; zeit: number } | null>(null)

  useEffect(() => {
    const beiTaste = (e: KeyboardEvent) => {
      const taste = e.key.toUpperCase()
      if ((e.ctrlKey || e.metaKey) && taste === 'K') {
        e.preventDefault()
        // Das Layout öffnet bei Strg K nur; hier wird umgeschaltet, deshalb nicht weiterreichen.
        e.stopPropagation()
        aktuell.current.umschalten()
        return
      }
      if (e.ctrlKey || e.metaKey || e.altKey || e.repeat || taste.length !== 1) return
      if (fokusBlockiert(e.target)) return

      const jetzt = Date.now()
      const vorher = folge.current
      folge.current = null
      const kuerzel = vorher && jetzt - vorher.zeit <= FOLGE_MS ? `${vorher.taste} ${taste}` : taste
      const befehl = aktuell.current.befehle.find((b) => b.kuerzel === kuerzel)
      if (befehl) {
        e.preventDefault()
        aktuell.current.ausfuehren(befehl)
      } else if (taste === 'G') {
        folge.current = { taste, zeit: jetzt }
      }
    }
    document.addEventListener('keydown', beiTaste)
    return () => document.removeEventListener('keydown', beiTaste)
  }, [])
}
