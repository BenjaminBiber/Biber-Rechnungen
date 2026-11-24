import * as TabsPrimitive from '@radix-ui/react-tabs'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export const Tabs = TabsPrimitive.Root

/**
 * Reiterleiste. Standard: Unterstrich-Tabs wie im Beleg-Detail.
 * Mit `variante="segment"` als Segment-Umschalter (Klasse `segment`).
 */
export function TabsList({
  className,
  variante = 'linie',
  ...props
}: ComponentProps<typeof TabsPrimitive.List> & { variante?: 'linie' | 'segment' }) {
  return (
    <TabsPrimitive.List
      data-slot="tabs-list"
      data-variante={variante}
      className={cn(variante === 'segment' ? 'segment' : 'flex gap-1 border-b border-rand', className)}
      {...props}
    />
  )
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof TabsPrimitive.Trigger>) {
  return (
    <TabsPrimitive.Trigger
      data-slot="tabs-trigger"
      className={cn(
        'inline-flex cursor-pointer items-center gap-1.5 whitespace-nowrap',
        'in-data-[variante=linie]:-mb-px in-data-[variante=linie]:h-10 in-data-[variante=linie]:border-0 in-data-[variante=linie]:border-b-2 in-data-[variante=linie]:border-transparent in-data-[variante=linie]:bg-transparent in-data-[variante=linie]:px-3 in-data-[variante=linie]:text-sm in-data-[variante=linie]:font-medium in-data-[variante=linie]:text-gedaempft-stark',
        'in-data-[variante=linie]:data-[state=active]:border-akzent in-data-[variante=linie]:data-[state=active]:text-akzent-text',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-akzent disabled:opacity-50',
        className,
      )}
      {...props}
    />
  )
}

export function TabsContent({ className, ...props }: ComponentProps<typeof TabsPrimitive.Content>) {
  return <TabsPrimitive.Content data-slot="tabs-content" className={cn('outline-none', className)} {...props} />
}
