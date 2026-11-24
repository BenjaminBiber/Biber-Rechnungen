import { createFileRoute } from '@tanstack/react-router'
import { VerfahrensdokuBereich } from '@/features/einstellungen/betrieb/verfahrensdoku'

export const Route = createFileRoute('/_app/einstellungen/verfahrensdoku')({
  component: VerfahrensdokuBereich,
})
