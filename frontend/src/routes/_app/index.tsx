import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/')({
  component: () => <Platzhalter titel="Dashboard" />,
})
