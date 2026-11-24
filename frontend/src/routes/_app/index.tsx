import { createFileRoute } from '@tanstack/react-router'
import { DashboardSeite } from '@/features/dashboard/DashboardSeite'
import type { DashboardZeitraum } from '@/features/dashboard/gemeinsam'

type DashboardSuche = { zeitraum?: DashboardZeitraum; jahr?: number }

const zeitraeume: DashboardZeitraum[] = ['Monat', 'Quartal', 'Jahr']

export const Route = createFileRoute('/_app/')({
  validateSearch: (suche: Record<string, unknown>): DashboardSuche => {
    const zeitraum = zeitraeume.find((z) => z === suche.zeitraum)
    const jahr = Number(suche.jahr)
    return {
      ...(zeitraum ? { zeitraum } : {}),
      ...(Number.isInteger(jahr) && jahr > 1900 ? { jahr } : {}),
    }
  },
  component: DashboardRoute,
})

function DashboardRoute() {
  const suche = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <DashboardSeite
      zeitraum={suche.zeitraum ?? 'Jahr'}
      jahr={suche.jahr ?? new Date().getFullYear()}
      onWahl={(wahl) => void navigate({ search: (alt) => ({ ...alt, ...wahl }) })}
    />
  )
}
