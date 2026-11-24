import { createFileRoute } from '@tanstack/react-router'
import { useBeleg } from '@/api/belege'
import { DateienTab } from '@/features/belege/detail/tabs/DateienTab'

/** Tab „Dateien“ des Beleg-Details. */
export const Route = createFileRoute('/_app/belege/$belegId/dateien')({
  component: DateienRoute,
})

function DateienRoute() {
  const { belegId } = Route.useParams()
  const { data: beleg } = useBeleg(belegId)
  if (!beleg) return null
  return <DateienTab beleg={beleg} />
}
