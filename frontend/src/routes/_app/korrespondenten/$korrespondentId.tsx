import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/korrespondenten/$korrespondentId')({
  component: () => <Platzhalter titel="Korrespondent" />,
})
