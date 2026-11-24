import { createFileRoute } from '@tanstack/react-router'
import { IntegritaetBereich } from '@/features/einstellungen/betrieb/integritaet'

export const Route = createFileRoute('/_app/einstellungen/integritaet')({
  component: IntegritaetBereich,
})
