import * as SheetPrimitive from '@radix-ui/react-dialog'
import type { ComponentProps } from 'react'
import { Icon } from '@/components/Icon'
import { cn } from '@/lib/utils'

export const Sheet = SheetPrimitive.Root
export const SheetTrigger = SheetPrimitive.Trigger
export const SheetClose = SheetPrimitive.Close

const seiten = {
  rechts: 'inset-y-0 right-0 h-full w-[min(420px,100vw)] border-l',
  links: 'inset-y-0 left-0 h-full w-[min(320px,100vw)] border-r',
  unten: 'inset-x-0 bottom-0 max-h-[85vh] rounded-t-karte border-t shadow-leiste-unten',
  oben: 'inset-x-0 top-0 max-h-[85vh] border-b',
} as const

/** Seitlich oder unten einfahrendes Panel (mobil: Filter, Menüs). */
export function SheetContent({
  className,
  children,
  seite = 'rechts',
  ...props
}: ComponentProps<typeof SheetPrimitive.Content> & { seite?: keyof typeof seiten }) {
  return (
    <SheetPrimitive.Portal>
      <SheetPrimitive.Overlay data-slot="sheet-overlay" className="fixed inset-0 z-50 bg-[rgba(21,25,34,0.45)]" />
      <SheetPrimitive.Content
        data-slot="sheet-content"
        className={cn(
          'fixed z-50 flex flex-col gap-4 overflow-y-auto border-rand bg-karte p-5 text-text shadow-dialog',
          seiten[seite],
          className,
        )}
        {...props}
      >
        {children}
        <SheetPrimitive.Close className="btn btn-klein absolute top-4 right-4 w-[30px] border-transparent px-0">
          <Icon name="close" label="Schließen" />
        </SheetPrimitive.Close>
      </SheetPrimitive.Content>
    </SheetPrimitive.Portal>
  )
}

export function SheetHeader({ className, ...props }: ComponentProps<'div'>) {
  return <div data-slot="sheet-header" className={cn('flex flex-col gap-1.5 pr-8', className)} {...props} />
}

export function SheetFooter({ className, ...props }: ComponentProps<'div'>) {
  return <div data-slot="sheet-footer" className={cn('mt-auto flex flex-col gap-2', className)} {...props} />
}

export function SheetTitle({ className, ...props }: ComponentProps<typeof SheetPrimitive.Title>) {
  return (
    <SheetPrimitive.Title
      data-slot="sheet-title"
      className={cn('m-0 text-[18px] font-semibold text-text', className)}
      {...props}
    />
  )
}

export function SheetDescription({ className, ...props }: ComponentProps<typeof SheetPrimitive.Description>) {
  return (
    <SheetPrimitive.Description
      data-slot="sheet-description"
      className={cn('m-0 text-sm text-gedaempft', className)}
      {...props}
    />
  )
}
