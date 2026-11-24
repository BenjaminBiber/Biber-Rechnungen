import { createFileRoute } from '@tanstack/react-router'
import { useMemo } from 'react'
import { ExportSeite } from '@/features/export/ExportSeite'
import { zeitraumVorgabe } from '@/lib/zeitraum'

type ExportSuche = {
  von?: string
  bis?: string
  kategorien?: string[]
  korrespondenten?: string[]
  tags?: string[]
}

const DATUM = /^\d{4}-\d{2}-\d{2}$/

/** Liste aus Array oder kommagetrennter Zeichenkette. */
function liste(wert: unknown): string[] | undefined {
  const roh = Array.isArray(wert) ? wert.map(String) : typeof wert === 'string' ? wert.split(',') : []
  const ids = roh.map((s) => s.trim()).filter(Boolean)
  return ids.length > 0 ? ids : undefined
}

export const Route = createFileRoute('/_app/export')({
  validateSearch: (suche: Record<string, unknown>): ExportSuche => {
    const datum = (wert: unknown) => (typeof wert === 'string' && DATUM.test(wert) ? wert : undefined)
    const ergebnis: ExportSuche = {
      von: datum(suche.von),
      bis: datum(suche.bis),
      kategorien: liste(suche.kategorien),
      korrespondenten: liste(suche.korrespondenten),
      tags: liste(suche.tags),
    }
    return Object.fromEntries(Object.entries(ergebnis).filter(([, w]) => w !== undefined))
  },
  component: ExportRoute,
})

function ExportRoute() {
  const suche = Route.useSearch()
  const heute = useMemo(() => new Date(), [])
  const vorgabe = zeitraumVorgabe('letztesQuartal', heute)
  return (
    <ExportSeite
      heute={heute}
      vorbelegung={{
        von: suche.von ?? vorgabe.von,
        bis: suche.bis ?? vorgabe.bis,
        kategorien: suche.kategorien ?? [],
        korrespondenten: suche.korrespondenten ?? [],
        tags: suche.tags ?? [],
      }}
    />
  )
}
