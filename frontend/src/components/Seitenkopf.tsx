import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Kopfzeile jeder Seite: Titel (h1), optionaler Untertitel und Aktionen rechts. `className` übersteuert den
 * Abstand nach unten, z. B. `mb-0` in Seiten, die ihre Abschnitte schon per `gap` trennen.
 */
export function Seitenkopf({
  titel,
  untertitel,
  aktionen,
  className,
}: {
  titel: string
  untertitel?: ReactNode
  aktionen?: ReactNode
  className?: string
}) {
  return (
    <div className={cn('mb-6 flex flex-wrap items-start justify-between gap-4', className)}>
      <div className="min-w-0">
        <h1 className="m-0 text-[22px] font-semibold text-text">{titel}</h1>
        {untertitel && <p className="gedaempft m-0 mt-1 text-sm">{untertitel}</p>}
      </div>
      {aktionen && <div className="flex flex-wrap items-center gap-2">{aktionen}</div>}
    </div>
  )
}
