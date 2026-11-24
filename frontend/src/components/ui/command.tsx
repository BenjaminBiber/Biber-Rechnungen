import { Command as CommandPrimitive } from 'cmdk'
import type { ComponentProps } from 'react'
import { Icon } from '@/components/Icon'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import { cn } from '@/lib/utils'

export function Command({ className, ...props }: ComponentProps<typeof CommandPrimitive>) {
  return (
    <CommandPrimitive
      data-slot="command"
      className={cn('flex h-full w-full flex-col overflow-hidden bg-karte text-text', className)}
      {...props}
    />
  )
}

/** Befehlspalette im Dialog (Strg+K). */
export function CommandDialog({
  titel = 'Befehlspalette',
  beschreibung = 'Suchen oder Befehl ausführen',
  children,
  ...props
}: ComponentProps<typeof Dialog> & { titel?: string; beschreibung?: string }) {
  return (
    <Dialog {...props}>
      <DialogContent ohneSchliessen className="top-[15vh] max-w-[640px] translate-y-0 gap-0 overflow-hidden p-0">
        <DialogTitle className="sr-only">{titel}</DialogTitle>
        <DialogDescription className="sr-only">{beschreibung}</DialogDescription>
        <Command>{children}</Command>
      </DialogContent>
    </Dialog>
  )
}

export function CommandInput({ className, ...props }: ComponentProps<typeof CommandPrimitive.Input>) {
  return (
    <div data-slot="command-input-wrapper" className="flex h-12 items-center gap-2.5 border-b border-linie px-4">
      <Icon name="search" className="text-gedaempft" />
      <CommandPrimitive.Input
        data-slot="command-input"
        className={cn(
          'h-full flex-1 border-0 bg-transparent text-[15px] text-text outline-none placeholder:text-gedaempft',
          className,
        )}
        {...props}
      />
    </div>
  )
}

export function CommandList({ className, ...props }: ComponentProps<typeof CommandPrimitive.List>) {
  return (
    <CommandPrimitive.List
      data-slot="command-list"
      className={cn('max-h-[400px] overflow-x-hidden overflow-y-auto p-1.5', className)}
      {...props}
    />
  )
}

export function CommandEmpty(props: ComponentProps<typeof CommandPrimitive.Empty>) {
  return (
    <CommandPrimitive.Empty data-slot="command-empty" className="py-6 text-center text-sm text-gedaempft" {...props} />
  )
}

export function CommandGroup({ className, ...props }: ComponentProps<typeof CommandPrimitive.Group>) {
  return (
    <CommandPrimitive.Group
      data-slot="command-group"
      className={cn(
        '[&_[cmdk-group-heading]]:px-2.5 [&_[cmdk-group-heading]]:pt-2 [&_[cmdk-group-heading]]:pb-1 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:text-gedaempft',
        className,
      )}
      {...props}
    />
  )
}

export function CommandSeparator({ className, ...props }: ComponentProps<typeof CommandPrimitive.Separator>) {
  return (
    <CommandPrimitive.Separator
      data-slot="command-separator"
      className={cn('my-1 h-px bg-linie', className)}
      {...props}
    />
  )
}

export function CommandItem({ className, ...props }: ComponentProps<typeof CommandPrimitive.Item>) {
  return (
    <CommandPrimitive.Item
      data-slot="command-item"
      className={cn(
        'flex h-9 cursor-pointer items-center gap-2.5 rounded-md px-2.5 text-sm outline-none select-none',
        'data-[selected=true]:bg-hover data-[disabled=true]:pointer-events-none data-[disabled=true]:opacity-50',
        '[&_.material-symbols-outlined]:text-[18px] [&_.material-symbols-outlined]:text-gedaempft [&_.material-symbols-outlined]:[vertical-align:0]',
        className,
      )}
      {...props}
    />
  )
}

export function CommandShortcut({ className, ...props }: ComponentProps<'span'>) {
  return (
    <span data-slot="command-shortcut" className={cn('mono ml-auto text-xs text-gedaempft', className)} {...props} />
  )
}
