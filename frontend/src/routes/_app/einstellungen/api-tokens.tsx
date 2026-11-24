import { createFileRoute } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'

export const Route = createFileRoute('/_app/einstellungen/api-tokens')({
  component: () => <Platzhalter titel="API-Tokens (MCP)" />,
})
