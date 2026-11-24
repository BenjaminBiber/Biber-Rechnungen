import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

/** Tabelle im Design-Stil (Klasse `tabelle`); Beträge in Zellen mit `className="num"`. */
export function Table({ className, ...props }: ComponentProps<'table'>) {
  return (
    <div data-slot="table-container" className="relative w-full overflow-x-auto">
      <table data-slot="table" className={cn('tabelle', className)} {...props} />
    </div>
  )
}

export function TableHeader(props: ComponentProps<'thead'>) {
  return <thead data-slot="table-header" {...props} />
}

export function TableBody(props: ComponentProps<'tbody'>) {
  return <tbody data-slot="table-body" {...props} />
}

export function TableFooter({ className, ...props }: ComponentProps<'tfoot'>) {
  return <tfoot data-slot="table-footer" className={cn('font-semibold', className)} {...props} />
}

export function TableRow({ className, ...props }: ComponentProps<'tr'>) {
  return (
    <tr
      data-slot="table-row"
      className={cn('hover:bg-tabellenkopf data-[state=selected]:bg-akzent-zart', className)}
      {...props}
    />
  )
}

export function TableHead(props: ComponentProps<'th'>) {
  return <th data-slot="table-head" {...props} />
}

export function TableCell(props: ComponentProps<'td'>) {
  return <td data-slot="table-cell" {...props} />
}

export function TableCaption({ className, ...props }: ComponentProps<'caption'>) {
  return <caption data-slot="table-caption" className={cn('mt-3 text-sm text-gedaempft', className)} {...props} />
}
