import { useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'
import { binde, type IconAusloeser } from './kubai/anbinden'
import { SYMBOLE } from './kubai/symbole.js'
import './kubai/kubai.css'

/**
 * Icon über den Namen des Material-Symbols, z. B. `<Icon name="receipt_long" />`.
 *
 * Gezeichnet wird das animierte Linien-Icon aus `kubai/symbole.js` (gleicher Stil wie die Hauptnavigation); die
 * Größe folgt wie bisher der Schriftgröße, die Farbe der Textfarbe. Wann es sich bewegt, regelt `ausloeser`:
 * Standard ist `auto` – Hover in Buttons und Links mit Text, Klick bei reinen Icon-Buttons, sonst ruhig. Wechselt
 * `name` (z. B. Kopieren → Haken), spielt das neue Icon einmal.
 *
 * Für einen Namen ohne eigenes Icon bleibt es beim Schriftzeichen der Material Symbols Outlined.
 * Ohne `label` ist das Icon dekorativ (`aria-hidden`), mit `label` wird es als Bild vorgelesen.
 */
export function Icon({
  name,
  className,
  label,
  ausloeser = 'auto',
}: {
  name: string
  className?: string
  label?: string
  ausloeser?: IconAusloeser
}) {
  const host = useRef<HTMLSpanElement>(null)
  const gezeigt = useRef<string | null>(null)
  const def = Object.hasOwn(SYMBOLE, name) ? SYMBOLE[name] : undefined
  const barrierefrei = label ? { role: 'img', 'aria-label': label } : { 'aria-hidden': true }

  useEffect(() => {
    const el = host.current
    if (!el || !def) return
    const auftritt = gezeigt.current !== null && gezeigt.current !== def.name
    gezeigt.current = def.name
    return binde(el, def, ausloeser, auftritt)
  }, [def, ausloeser])

  if (!def) {
    return (
      <span
        key="schrift"
        className={cn('material-symbols-outlined', className)}
        translate="no"
        data-icon={name}
        {...barrierefrei}
      >
        {name}
      </span>
    )
  }
  return (
    <span
      key="kubai"
      ref={host}
      className={cn('material-symbols-outlined symbol', className)}
      data-icon={name}
      {...barrierefrei}
    />
  )
}
