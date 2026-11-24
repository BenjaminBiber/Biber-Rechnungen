import { createFileRoute } from '@tanstack/react-router'
import { AllgemeinBereich } from '@/features/einstellungen/allgemein'

export const Route = createFileRoute('/_app/einstellungen/allgemein')({
  component: AllgemeinBereich,
})
