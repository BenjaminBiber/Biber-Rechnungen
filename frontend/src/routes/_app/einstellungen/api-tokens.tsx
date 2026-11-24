import { createFileRoute } from '@tanstack/react-router'
import { ApiTokensBereich } from '@/features/einstellungen/betrieb/apiTokens'

export const Route = createFileRoute('/_app/einstellungen/api-tokens')({
  component: ApiTokensBereich,
})
