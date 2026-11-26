import { useNavigate } from '@tanstack/react-router'
import { useEffect, useRef } from 'react'
import type { BelegListeneintrag } from '@/api/typen'
import { useBelegAuswahl } from './auswahl'

/** Was an einer Zeile geöffnet werden soll: das Zeilenmenü, die Tag- oder die Kategorie-Auswahl. */
export type ZeilenOeffnen = 'menue' | 'tags' | 'kategorie'

type Hoerer = (belegId: string, was: ZeilenOeffnen) => void
const hoerer = new Set<Hoerer>()

/** Bittet das Zeilenmenü des Belegs, etwas zu öffnen (Tastaturkürzel, Rechtsklick). */
export function zeileOeffnen(belegId: string, was: ZeilenOeffnen): void {
  hoerer.forEach((h) => h(belegId, was))
}

/** Meldet das Zeilenmenü für Öffnen-Anfragen an. */
export function aufZeileOeffnenHoeren(h: Hoerer): () => void {
  hoerer.add(h)
  return () => void hoerer.delete(h)
}

function istEingabe(ziel: EventTarget | null): boolean {
  if (!(ziel instanceof HTMLElement)) return false
  return ziel.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(ziel.tagName)
}

/** Bedienelemente und Zellen in der Zeile, deren Klick nicht den Beleg öffnet. */
const EIGENER_KLICK =
  'a, button, input, label, select, textarea, [role="button"], [role="checkbox"], [role="menuitem"], [data-kein-zeilenklick]'

/**
 * Tastaturkürzel und Maus für die Zeilen der Belegtabelle: Enter öffnet den Beleg, T die Tag-Auswahl,
 * K die Kategorie-Auswahl, Leertaste wählt aus; Rechtsklick öffnet das Zeilenmenü. Gilt nur für die
 * fokussierte Zeile selbst – nicht, solange ein Eingabefeld (z. B. die Suche) den Fokus hat.
 * Ein Klick auf die Zeile öffnet den Beleg (auch wenn die Titel-Spalte ausgeblendet ist) – außer auf
 * Bedienelementen, der Auswahl- und der Menüspalte oder wenn dabei Text markiert wurde.
 */
export function useListenKuerzel(eintraege: BelegListeneintrag[]) {
  const tabelle = useRef<HTMLTableSectionElement>(null)
  const navigate = useNavigate()
  const auswahl = useBelegAuswahl()
  const stand = useRef({ eintraege, auswahl, navigate })
  useEffect(() => {
    stand.current = { eintraege, auswahl, navigate }
  })

  useEffect(() => {
    const tbody = tabelle.current
    if (!tbody) return

    function zeileVon(ziel: EventTarget | null): BelegListeneintrag | undefined {
      if (!(ziel instanceof Element)) return undefined
      const tr = ziel.closest('tr')
      if (!tr || tr.parentElement !== tbody) return undefined
      return stand.current.eintraege[Array.prototype.indexOf.call(tbody!.children, tr)]
    }

    function taste(e: KeyboardEvent) {
      if (istEingabe(e.target) || e.altKey || e.ctrlKey || e.metaKey) return
      // Nur die Zeile selbst – Taste auf Checkbox, Link oder Knopf in der Zeile behält ihr Verhalten.
      if (!(e.target instanceof HTMLTableRowElement)) return
      const beleg = zeileVon(e.target)
      if (!beleg) return
      const { auswahl, navigate } = stand.current
      if (e.key === 'Enter') void navigate({ to: '/belege/$belegId', params: { belegId: beleg.id } })
      else if (e.key === ' ') auswahl.umschalten(beleg.id)
      else if (e.key === 't' || e.key === 'T') zeileOeffnen(beleg.id, 'tags')
      else if (e.key === 'k' || e.key === 'K') zeileOeffnen(beleg.id, 'kategorie')
      else return
      e.preventDefault()
    }

    function kontextmenue(e: MouseEvent) {
      const beleg = zeileVon(e.target)
      if (!beleg) return
      e.preventDefault()
      zeileOeffnen(beleg.id, 'menue')
    }

    function klick(e: MouseEvent) {
      if (e.button !== 0 || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return
      if (!(e.target instanceof Element) || e.target.closest(EIGENER_KLICK)) return
      // Wer Text markiert (z. B. die Rechnungsnummer kopieren will), soll nicht weggeleitet werden.
      if (window.getSelection()?.toString()) return
      const beleg = zeileVon(e.target)
      if (!beleg) return
      void stand.current.navigate({ to: '/belege/$belegId', params: { belegId: beleg.id } })
    }

    tbody.addEventListener('keydown', taste)
    tbody.addEventListener('contextmenu', kontextmenue)
    tbody.addEventListener('click', klick)
    return () => {
      tbody.removeEventListener('keydown', taste)
      tbody.removeEventListener('contextmenu', kontextmenue)
      tbody.removeEventListener('click', klick)
    }
  }, [])

  return tabelle
}
