import { createFileRoute } from '@tanstack/react-router'
import { MailBereich } from '@/features/einstellungen/eingang/mail'

export const Route = createFileRoute('/_app/einstellungen/mail')({
  component: MailBereich,
})
