import { Link } from '@tanstack/react-router'
import type { TopKorrespondent } from '@/api/typen'
import { formatEuro } from '@/lib/format'
import { KorrespondentLogo, type LogoTon } from '@/components/KorrespondentLogo'
import { DashboardKarte } from './gemeinsam'

const toene: LogoTon[] = ['petrol', 'blau', 'grau', 'orange', 'lila']

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
              <KorrespondentLogo logo={e.logo} groesse={30} ton={ton} />
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
