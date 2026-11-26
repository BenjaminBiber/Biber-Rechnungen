import { createFileRoute } from '@tanstack/react-router'
import { GeloeschteBelegeBereich } from '@/features/einstellungen/geloeschteBelege'

export const Route = createFileRoute('/_app/einstellungen/geloeschte-belege')({
  component: GeloeschteBelegeBereich,
})
