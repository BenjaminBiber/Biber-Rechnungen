import { useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'
import { go, mountIcon, runtime } from './engine.js'
import { NAV_ICONS } from './navIcons.js'
import './kubai.css'

export type KubaiIconName =
  'Dashboard' | 'Belege' | 'Hochladen' | 'Korrespondenten' | 'Artikel' | 'Tags' | 'Export' | 'Einstellungen'

// Die Nav-Icons sind einfarbig (currentColor); die Engine braucht trotzdem eine Palette.
runtime.palette = { name: 'Schrift', colors: NAV_ICONS.map(() => '#2E3036') }

let naechsteId = 0

/**
 * Animiertes, einfarbiges Linien-Icon (Farbe = `currentColor`, wie die Material Symbols). Es reagiert auf das nächste umgebende Link- oder Button-Element:
 * Mauszeiger darüber oder Tastaturfokus spielt die Hover-Bewegung ab, Verlassen spielt sie zurück.
 * Bei „Bewegung reduzieren“ springt es ohne Animation. Immer dekorativ (`aria-hidden`).
 */
export function KubaiIcon({ name, className }: { name: KubaiIconName; className?: string }) {
  const host = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    const el = host.current
    const index = NAV_ICONS.findIndex((i) => i.name === name)
    if (!el || index < 0) return
    const inst = mountIcon(NAV_ICONS[index], index, el, `kubai${naechsteId++}`)
    const ausloeser = el.closest<HTMLElement>('a, button') ?? el
    let zeiger = false
    let fokus = false
    const setzen = () => {
      const hover = Number(zeiger) + Number(fokus)
      if (hover === inst.hover) return
      inst.hover = hover
      go(inst)
    }
    const zeigerAn = (e: PointerEvent) => {
      if (e.pointerType !== 'mouse') return
      zeiger = true
      setzen()
    }
    const zeigerAus = (e: PointerEvent) => {
      if (e.pointerType !== 'mouse') return
      zeiger = false
      setzen()
    }
    // Nur Tastaturfokus zählt; ein Mausklick fokussiert den Link auch, soll das Icon aber nicht festhalten.
    const fokusAn = () => {
      fokus = sichtbarerFokus(ausloeser)
      setzen()
    }
    const fokusAus = () => {
      fokus = false
      setzen()
    }
    ausloeser.addEventListener('pointerenter', zeigerAn)
    ausloeser.addEventListener('pointerleave', zeigerAus)
    ausloeser.addEventListener('focus', fokusAn)
    ausloeser.addEventListener('blur', fokusAus)
    return () => {
      cancelAnimationFrame(inst.raf)
      ausloeser.removeEventListener('pointerenter', zeigerAn)
      ausloeser.removeEventListener('pointerleave', zeigerAus)
      ausloeser.removeEventListener('focus', fokusAn)
      ausloeser.removeEventListener('blur', fokusAus)
      el.replaceChildren()
    }
  }, [name])

  return <span ref={host} aria-hidden="true" className={cn('kubai-icon', className)} />
}

function sichtbarerFokus(el: HTMLElement): boolean {
  try {
    return el.matches(':focus-visible')
  } catch {
    return true
  }
}
