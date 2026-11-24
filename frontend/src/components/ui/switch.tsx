import * as SwitchPrimitive from '@radix-ui/react-switch'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

/** Schalter wie im Design: 40 × 24 px, an = Akzent, aus = Grau. */
export function Switch({ className, ...props }: ComponentProps<typeof SwitchPrimitive.Root>) {
  return (
    <SwitchPrimitive.Root
      data-slot="switch"
      className={cn(
        'relative inline-flex h-6 w-10 shrink-0 cursor-pointer items-center rounded-full border-0 p-0',
        'bg-schalter-aus data-[state=checked]:bg-akzent',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-akzent',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb
        data-slot="switch-thumb"
        className="block size-[18px] translate-x-[3px] rounded-full bg-white shadow-[0_1px_2px_rgba(0,0,0,0.2)] transition-transform data-[state=checked]:translate-x-[19px]"
      />
    </SwitchPrimitive.Root>
  )
}
