import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/artikel/stammartikel/$stammartikelId')({
  component: () => <Platzhalter titel="Stammartikel" />,
})
