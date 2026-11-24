import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/einstellungen/benutzer')({
  component: () => <Platzhalter titel="Benutzer & Freigaben" />,
})
