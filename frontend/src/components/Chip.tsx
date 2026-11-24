import type { ReactNode } from 'react'
import { Icon } from '@/components/Icon'

export type ChipTon = 'blau' | 'orange' | 'gruen' | 'rot' | 'grau' | 'petrol' | 'lila'

/**
 * Status- oder Tag-Chip in einem der sieben Töne. Der Text ist Pflicht – Status nie nur über Farbe.
 * Mit `onEntfernen` erscheint ein Schließen-Knopf („… entfernen“).
 */
export function Chip({
  ton,
  icon,
  children,
  onEntfernen,
}: {
  ton: ChipTon
  icon?: string
  children: ReactNode
  onEntfernen?: () => void
}) {
  return (
    <span className={`chip chip-${ton}`}>
      {icon && <Icon name={icon} />}
      {children}
      {onEntfernen && (
        <button
          type="button"
          className="chip-entfernen"
          aria-label={typeof children === 'string' ? `${children} entfernen` : 'Entfernen'}
          onClick={onEntfernen}
        >
          <Icon name="close" />
        </button>
      )}
    </span>
  )
}
