import { createFileRoute } from '@tanstack/react-router'
import { EingangsordnerBereich } from '@/features/einstellungen/eingang/eingangsordner'

export const Route = createFileRoute('/_app/einstellungen/eingangsordner')({
  component: EingangsordnerBereich,
})
