import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/belege/')({
  component: () => <Platzhalter titel="Belege" />,
})
