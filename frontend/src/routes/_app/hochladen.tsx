import { createFileRoute } from '@tanstack/react-router'
import { HochladenSeite } from '@/features/hochladen/HochladenSeite'

export const Route = createFileRoute('/_app/hochladen')({
  component: HochladenSeite,
})
