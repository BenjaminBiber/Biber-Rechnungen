import { createFileRoute } from '@tanstack/react-router'
import { HistorieTab } from '@/features/belege/detail/tabs/HistorieTab'

/** Tab „Historie“ des Beleg-Details. */
export const Route = createFileRoute('/_app/belege/$belegId/historie')({
  component: HistorieRoute,
})

function HistorieRoute() {
  const { belegId } = Route.useParams()
  return <HistorieTab belegId={belegId} />
}
