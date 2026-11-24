import { createFileRoute, Outlet } from '@tanstack/react-router'
import type { BelegeAbfrage } from '@/api/typen'
import { BelegDetailSeite } from '@/features/belege/detail/BelegDetailSeite'
import type { BelegDetailSuche } from '@/features/belege/detail/gemeinsam'

/** Layout des Beleg-Details: Kopf, Viewer und Tab-Leiste; die Tabs sind verschachtelte Routen. */
export const Route = createFileRoute('/_app/belege/$belegId')({
  validateSearch: (suche: Record<string, unknown>): BelegDetailSuche =>
    typeof suche.aus === 'object' && suche.aus !== null ? { aus: suche.aus as BelegeAbfrage } : {},
  component: BelegDetailRoute,
})

function BelegDetailRoute() {
  const { belegId } = Route.useParams()
  const { aus } = Route.useSearch()
  return (
    <BelegDetailSeite belegId={belegId} aus={aus}>
      <Outlet />
    </BelegDetailSeite>
  )
}
