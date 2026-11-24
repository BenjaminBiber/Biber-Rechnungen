import { createFileRoute } from '@tanstack/react-router'
import { KontoBereich } from '@/features/einstellungen/zugriff/KontoBereich'

type Suche = { google?: 'verknuepft' | 'vergeben' }

export const Route = createFileRoute('/_app/einstellungen/konto')({
  validateSearch: (suche: Record<string, unknown>): Suche =>
    suche.google === 'verknuepft' || suche.google === 'vergeben' ? { google: suche.google } : {},
  component: function KontoSeite() {
    const { google } = Route.useSearch()
    return <KontoBereich google={google} />
  },
})
