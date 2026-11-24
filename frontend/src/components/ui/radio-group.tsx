import * as RadioGroupPrimitive from '@radix-ui/react-radio-group'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function RadioGroup({ className, ...props }: ComponentProps<typeof RadioGroupPrimitive.Root>) {
  return <RadioGroupPrimitive.Root data-slot="radio-group" className={cn('grid gap-2', className)} {...props} />
}

export function RadioGroupItem({ className, ...props }: ComponentProps<typeof RadioGroupPrimitive.Item>) {
  return (
    <RadioGroupPrimitive.Item
      data-slot="radio-group-item"
      className={cn(
        'inline-flex size-[18px] shrink-0 items-center justify-center rounded-full border border-eingabe-rand bg-karte',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-akzent',
        'data-[state=checked]:border-akzent disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <RadioGroupPrimitive.Indicator className="block size-2.5 rounded-full bg-akzent" />
    </RadioGroupPrimitive.Item>
  )
}
