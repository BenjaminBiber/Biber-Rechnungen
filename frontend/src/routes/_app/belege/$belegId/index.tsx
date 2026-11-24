import { createFileRoute } from '@tanstack/react-router'
import { useCallback } from 'react'
import { useBeleg } from '@/api/belege'
import { useRolle } from '@/components/shell/useRolle'
import { DatenFormular } from '@/features/belege/detail/DatenFormular'

type DatenSuche = { fokus?: 'belegart' }

/** Tab „Daten“ des Beleg-Details. `?fokus=belegart` setzt den Fokus auf das Feld (Mehr-Menü „Belegart ändern“). */
export const Route = createFileRoute('/_app/belege/$belegId/')({
  validateSearch: (suche: Record<string, unknown>): DatenSuche =>
    suche.fokus === 'belegart' ? { fokus: 'belegart' } : {},
  component: DatenRoute,
})

function DatenRoute() {
  const { belegId } = Route.useParams()
  const { fokus } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { data: beleg } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()
  const fokusErledigt = useCallback(
    () => void navigate({ search: (alt) => ({ ...alt, fokus: undefined }), replace: true }),
    [navigate],
  )
  if (!beleg) return null
  return (
    <DatenFormular
      key={beleg.id}
      beleg={beleg}
      darfBearbeiten={darfBearbeiten}
      fokus={fokus}
      onFokusErledigt={fokusErledigt}
    />
  )
}
