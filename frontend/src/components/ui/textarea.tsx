import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        'eingabe placeholder:text-gedaempft disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-rot-text',
        className,
      )}
      {...props}
    />
  )
}
