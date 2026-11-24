import { createFileRoute } from '@tanstack/react-router'
import { DocumentIntelligenceBereich } from '@/features/einstellungen/eingang/documentIntelligence'

export const Route = createFileRoute('/_app/einstellungen/document-intelligence')({
  component: DocumentIntelligenceBereich,
})
