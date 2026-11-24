import { cn } from '@/lib/utils'

/**
 * Material Symbols Outlined über den Ligaturnamen, z. B. `<Icon name="receipt_long" />`.
 * Ohne `label` ist das Icon dekorativ (`aria-hidden`), mit `label` wird es als Bild vorgelesen.
 */
export function Icon({ name, className, label }: { name: string; className?: string; label?: string }) {
  const barrierefrei = label ? { role: 'img', 'aria-label': label } : { 'aria-hidden': true }
  return (
    <span className={cn('material-symbols-outlined', className)} translate="no" {...barrierefrei}>
      {name}
    </span>
  )
}
