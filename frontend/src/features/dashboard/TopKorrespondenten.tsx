import { Link } from '@tanstack/react-router'
import type { TopKorrespondent } from '@/api/typen'
import { formatEuro } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

const toene = ['petrol', 'blau', 'grau', 'orange', 'lila']

/** Fünf größte Korrespondenten des Jahres mit Balken relativ zum größten. */
export function TopKorrespondenten({ eintraege, jahr }: { eintraege: TopKorrespondent[]; jahr: number }) {
  const maximum = Math.max(0, ...eintraege.map((e) => e.summe))
  return (
    <DashboardKarte
      titel={`Top-Korrespondenten ${jahr}`}
      zusatz={<Link to="/korrespondenten">Alle</Link>}
      className="flex-[1_1_320px]"
    >
      {eintraege.length === 0 && <p className="gedaempft m-0 text-sm">Noch keine Kosten in {jahr}.</p>}
      {eintraege.slice(0, 5).map((e, i) => {
        const ton = toene[i % toene.length]
        const anteil = maximum > 0 ? Math.round((e.summe / maximum) * 100) : 0
        return (
          <Link
            key={e.korrespondent.id}
            to="/korrespondenten/$korrespondentId"
            params={{ korrespondentId: e.korrespondent.id }}
            className="flex flex-col gap-1.5 text-text no-underline hover:text-text"
          >
            <span className="flex items-center gap-2.5">
              <span
                className="flex size-[30px] flex-none items-center justify-center rounded-[7px] text-xs font-bold"
                style={{ background: `var(--color-${ton}-flaeche)`, color: `var(--color-${ton}-text)` }}
                aria-hidden="true"
              >
                {e.logo.initialen}
              </span>
              <span className="min-w-0 flex-1 truncate text-sm">{e.korrespondent.name}</span>
              <span className="num text-sm font-semibold">{formatEuro(e.summe)}</span>
            </span>
            <span className="ml-10 h-1.5 rounded-[3px] bg-[var(--color-linie)]">
              <span className="block h-1.5 rounded-[3px] bg-[var(--color-akzent)]" style={{ width: `${anteil}%` }} />
            </span>
          </Link>
        )
      })}
    </DashboardKarte>
  )
}
