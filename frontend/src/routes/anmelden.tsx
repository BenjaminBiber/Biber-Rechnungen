import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/anmelden')({
  component: () => <Platzhalter titel="Anmelden" />,
})
