import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function Input({ className, type = 'text', ...props }: ComponentProps<'input'>) {
  return (
    <input
      data-slot="input"
      type={type}
      className={cn(
        'eingabe placeholder:text-gedaempft disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-rot-text',
        className,
      )}
      {...props}
    />
  )
}
