import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/einstellungen/document-intelligence')({
  component: () => <Platzhalter titel="Document Intelligence" />,
})
