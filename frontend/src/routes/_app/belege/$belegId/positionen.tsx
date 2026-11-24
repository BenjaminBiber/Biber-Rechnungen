import { createFileRoute } from '@tanstack/react-router'
import { useBeleg } from '@/api/belege'
import { useRolle } from '@/components/shell/useRolle'
import { PositionenTab } from '@/features/belege/detail/tabs/PositionenTab'

/** Tab „Positionen“ des Beleg-Details. */
export const Route = createFileRoute('/_app/belege/$belegId/positionen')({
  component: PositionenRoute,
})

function PositionenRoute() {
  const { belegId } = Route.useParams()
  const { data: beleg } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()
  if (!beleg) return null
  return <PositionenTab key={beleg.id} beleg={beleg} darfBearbeiten={darfBearbeiten} />
}
