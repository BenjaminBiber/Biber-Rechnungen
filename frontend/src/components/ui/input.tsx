import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export type InputProps = ComponentProps<'input'> & {
  /** Wert wird gerade verarbeitet (z. B. Suche läuft): Drehkreis rechts im Feld, das Feld bleibt bedienbar. */
  laedt?: boolean
}

export function Input({ className, type = 'text', laedt = false, ...props }: InputProps) {
  return (
    <input
      data-slot="input"
      type={type}
      aria-busy={laedt || undefined}
      className={cn(
        'eingabe placeholder:text-gedaempft disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-rot-text',
        className,
      )}
      {...props}
    />
  )
}
