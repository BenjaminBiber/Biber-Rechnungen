import { createFileRoute } from '@tanstack/react-router'
import { PruefungenBereich } from '@/features/einstellungen/pruefungen'

export const Route = createFileRoute('/_app/einstellungen/pruefungen')({
  component: PruefungenBereich,
})
