import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export const buttonVariants = cva('btn', {
  variants: {
    variant: {
      standard: '',
      primaer: 'btn-primaer',
      gefaehrlich: 'btn-gefaehrlich',
      geist: 'border-transparent bg-transparent hover:bg-hover',
      link: 'h-auto border-0 bg-transparent p-0 text-akzent-text underline-offset-4 hover:bg-transparent hover:underline',
    },
    size: {
      standard: '',
      klein: 'btn-klein',
      icon: 'w-9 px-0',
      'icon-klein': 'btn-klein w-[30px] px-0',
    },
  },
  defaultVariants: { variant: 'standard', size: 'standard' },
})

export type ButtonProps = ComponentProps<'button'> &
  VariantProps<typeof buttonVariants> & {
    /** Rendert das Kind (z. B. einen Link) mit Button-Optik. */
    asChild?: boolean
  }

export function Button({ className, variant, size, asChild = false, type, ...props }: ButtonProps) {
  const Comp = asChild ? Slot : 'button'
  return (
    <Comp
      data-slot="button"
      type={asChild ? type : (type ?? 'button')}
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  )
}
