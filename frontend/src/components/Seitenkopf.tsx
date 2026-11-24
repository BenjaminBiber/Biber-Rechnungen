import type { ReactNode } from 'react'

/** Kopfzeile jeder Seite: Titel (h1), optionaler Untertitel und Aktionen rechts. */
export function Seitenkopf({
  titel,
  untertitel,
  aktionen,
}: {
  titel: string
  untertitel?: ReactNode
  aktionen?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="m-0 text-[22px] font-semibold text-text">{titel}</h1>
        {untertitel && <p className="gedaempft m-0 mt-1 text-sm">{untertitel}</p>}
      </div>
      {aktionen && <div className="flex flex-wrap items-center gap-2">{aktionen}</div>}
    </div>
  )
}
