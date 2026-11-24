import { createFileRoute } from '@tanstack/react-router'
import { UmsatzsteuerBereich } from '@/features/einstellungen/umsatzsteuer'

export const Route = createFileRoute('/_app/einstellungen/umsatzsteuer')({
  component: UmsatzsteuerBereich,
})
