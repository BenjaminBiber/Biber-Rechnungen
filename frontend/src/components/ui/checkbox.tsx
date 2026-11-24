import * as CheckboxPrimitive from '@radix-ui/react-checkbox'
import type { ComponentProps } from 'react'
import { Icon } from '@/components/Icon'
import { cn } from '@/lib/utils'

export function Checkbox({ className, ...props }: ComponentProps<typeof CheckboxPrimitive.Root>) {
  return (
    <CheckboxPrimitive.Root
      data-slot="checkbox"
      className={cn(
        'peer inline-flex size-[18px] shrink-0 items-center justify-center rounded-[5px] border border-eingabe-rand bg-karte text-auf-akzent',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-akzent',
        'data-[state=checked]:border-akzent data-[state=checked]:bg-akzent data-[state=indeterminate]:border-akzent data-[state=indeterminate]:bg-akzent',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator className="flex items-center justify-center">
        <Icon
          name={props.checked === 'indeterminate' ? 'remove' : 'check'}
          className="text-[16px] [vertical-align:0]"
        />
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  )
}
