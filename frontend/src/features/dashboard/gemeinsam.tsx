import type { ReactNode } from 'react'

export type DashboardZeitraum = 'Monat' | 'Quartal' | 'Jahr'

export const monatsNamen = ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez']

export const monatsNamenLang = [
  'Januar',
  'Februar',
  'März',
  'April',
  'Mai',
  'Juni',
  'Juli',
  'August',
  'September',
  'Oktober',
  'November',
  'Dezember',
]

/** Monat „JJJJ-MM“ → Index 0–11. */
export function monatsIndex(monat: string): number {
  return Number(monat.slice(5, 7)) - 1
}

/** Karte des Dashboards mit Überschrift (h2) und optionalem Zusatz rechts. */
export function DashboardKarte({
  titel,
  zusatz,
  children,
  className = '',
}: {
  titel: ReactNode
  zusatz?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`karte flex min-w-0 flex-col gap-3 p-5 ${className}`}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="m-0 text-base font-semibold text-text">{titel}</h2>
        {zusatz && <span className="text-[13px]">{zusatz}</span>}
      </div>
      {children}
    </section>
  )
}
