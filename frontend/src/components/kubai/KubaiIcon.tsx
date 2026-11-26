import { useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'
import { binde } from './anbinden'
import { NAV_ICONS } from './navIcons.js'
import './kubai.css'

export type KubaiIconName =
  'Dashboard' | 'Belege' | 'Hochladen' | 'Korrespondenten' | 'Artikel' | 'Tags' | 'Export' | 'Einstellungen'

/**
 * Animiertes, einfarbiges Linien-Icon (Farbe = `currentColor`, wie die Material Symbols). Es reagiert auf das nächste umgebende Link- oder Button-Element:
 * Mauszeiger darüber oder Tastaturfokus spielt die Hover-Bewegung ab, Verlassen spielt sie zurück.
 * Bei „Bewegung reduzieren“ springt es ohne Animation. Immer dekorativ (`aria-hidden`).
 */
export function KubaiIcon({ name, className }: { name: KubaiIconName; className?: string }) {
  const host = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    const el = host.current
    const def = NAV_ICONS.find((i) => i.name === name)
    if (!el || !def) return
    return binde(el, def, 'hover', false)
  }, [name])

  return <span ref={host} aria-hidden="true" className={cn('kubai-icon', className)} />
}
