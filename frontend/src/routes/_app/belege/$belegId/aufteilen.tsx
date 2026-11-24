import { createFileRoute, Navigate } from '@tanstack/react-router'
import { useAuthStatus } from '@/api/auth'
import { useBeleg } from '@/api/belege'
import { useRolle } from '@/components/shell/useRolle'
import { AufteilenDialog } from '@/features/belege/aufteilen/AufteilenDialog'

/** Dialog „Beleg aufteilen“ über dem Beleg-Detail; Schließen führt zurück zum Beleg. */
export const Route = createFileRoute('/_app/belege/$belegId/aufteilen')({
  component: AufteilenRoute,
})

function AufteilenRoute() {
  const { belegId } = Route.useParams()
  const navigate = Route.useNavigate()
  const { data: beleg } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()
  // useRolle gilt bis zum geladenen Status als „NurLesen“ – erst danach weiterleiten.
  const { isSuccess: rolleBekannt } = useAuthStatus()
  if (!beleg || !rolleBekannt) return null
  if (!darfBearbeiten) return <Navigate to="/belege/$belegId" params={{ belegId }} replace />
  return (
    <AufteilenDialog
      beleg={beleg}
      onSchliessen={() => void navigate({ to: '/belege/$belegId', params: { belegId } })}
      onAufgeteilt={() => void navigate({ to: '/hochladen' })}
    />
  )
}
