import type { ReactNode } from 'react'
import { useEinstellungen } from '@/api/einstellungen'
import type { Einstellungen } from '@/api/typen'

/** Überschrift eines Bereichs; der Inhalt erscheint, sobald die Einstellungen geladen sind. */
export function Bereich({ titel, children }: { titel: string; children: (e: Einstellungen) => ReactNode }) {
  const { data, isError, error } = useEinstellungen()
  return (
    <div className="flex flex-col gap-[18px]">
      <h2 className="m-0 text-lg font-semibold text-text">{titel}</h2>
      {data ? (
        children(data)
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : (
        <p className="gedaempft m-0 text-sm">Lädt …</p>
      )}
    </div>
  )
}
