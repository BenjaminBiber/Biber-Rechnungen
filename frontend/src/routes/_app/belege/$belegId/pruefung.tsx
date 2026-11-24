import { createFileRoute } from '@tanstack/react-router'
import { useBeleg } from '@/api/belege'
import { PruefungTab } from '@/features/belege/detail/tabs/PruefungTab'

/** Tab „Prüfung“ des Beleg-Details. */
export const Route = createFileRoute('/_app/belege/$belegId/pruefung')({
  component: PruefungRoute,
})

function PruefungRoute() {
  const { belegId } = Route.useParams()
  const { data: beleg } = useBeleg(belegId)
  if (!beleg) return null
  return <PruefungTab beleg={beleg} />
}
