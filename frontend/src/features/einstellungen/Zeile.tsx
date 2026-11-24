import type { ReactNode } from 'react'

/** Einstellungszeile: Titel und Beschreibung links, Bedienelement rechts (`.row` in Einstellungen.dc.html). */
export function Zeile({
  titel,
  beschreibung,
  fehler,
  children,
}: {
  titel: string
  beschreibung?: ReactNode
  fehler?: string
  children: ReactNode
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-linie py-3.5">
      <span className="flex-[1_1_280px]">
        <strong className="text-sm">{titel}</strong>
        {beschreibung && (
          <>
            <br />
            <span className="gedaempft text-[13px]">{beschreibung}</span>
          </>
        )}
        {fehler && (
          <span role="alert" className="block text-[13px] text-gefahr">
            {fehler}
          </span>
        )}
      </span>
      {children}
    </div>
  )
}
