import { createFileRoute } from '@tanstack/react-router'
import { useCallback } from 'react'
import { useBeleg } from '@/api/belege'
import { useRolle } from '@/components/shell/useRolle'
import { DublettenTab } from '@/features/belege/detail/tabs/DublettenTab'

type DublettenSuche = { markieren?: 1 }

/** Tab „Dubletten“ des Beleg-Details. `?markieren=1` öffnet den Markieren-Dialog (Mehr-Menü). */
export const Route = createFileRoute('/_app/belege/$belegId/dubletten')({
  validateSearch: (suche: Record<string, unknown>): DublettenSuche =>
    suche.markieren === 1 || suche.markieren === '1' ? { markieren: 1 } : {},
  component: DublettenRoute,
})

function DublettenRoute() {
  const { belegId } = Route.useParams()
  const { markieren } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { data: beleg } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()
  const markierenSchliessen = useCallback(
    () => void navigate({ search: (alt) => ({ ...alt, markieren: undefined }), replace: true }),
    [navigate],
  )
  if (!beleg) return null
  return (
    <DublettenTab
      key={beleg.id}
      beleg={beleg}
      darfBearbeiten={darfBearbeiten}
      markieren={markieren === 1}
      onMarkierenSchliessen={markierenSchliessen}
    />
  )
}
