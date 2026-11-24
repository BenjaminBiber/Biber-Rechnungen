import { createFileRoute } from '@tanstack/react-router'
import { BenutzerBereich } from '@/features/einstellungen/zugriff/BenutzerBereich'

export const Route = createFileRoute('/_app/einstellungen/benutzer')({
  component: BenutzerBereich,
})
