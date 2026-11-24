import { createFileRoute } from '@tanstack/react-router'
import { AufbewahrungBereich } from '@/features/einstellungen/aufbewahrung'

export const Route = createFileRoute('/_app/einstellungen/aufbewahrung')({
  component: AufbewahrungBereich,
})
