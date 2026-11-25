import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { useState, type ComponentProps, type MouseEvent } from 'react'
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
    /**
     * Aktion läuft (z. B. `mutation.isPending`): Knopf gesperrt, nach 250 ms Drehkreis statt Inhalt.
     * Gibt `onClick` ein Promise zurück, gilt der Knopf bis zu dessen Ende ebenfalls als ladend.
     */
    laedt?: boolean
  }

export function Button({
  className,
  variant,
  size,
  asChild = false,
  type,
  laedt = false,
  disabled,
  onClick,
  ...props
}: ButtonProps) {
  const Comp = asChild ? Slot : 'button'
  const [klickLaeuft, setKlickLaeuft] = useState(false)
  const beschaeftigt = laedt || klickLaeuft

  function klick(e: MouseEvent<HTMLButtonElement>) {
    const ergebnis: unknown = onClick?.(e)
    if (ergebnis instanceof Promise) {
      setKlickLaeuft(true)
      void ergebnis.finally(() => setKlickLaeuft(false))
    }
  }

  return (
    <Comp
      data-slot="button"
      type={asChild ? type : (type ?? 'button')}
      className={cn(buttonVariants({ variant, size }), className)}
      disabled={disabled || beschaeftigt}
      aria-busy={beschaeftigt || undefined}
      onClick={onClick ? klick : undefined}
      {...props}
    />
  )
}
