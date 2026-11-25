import { createFileRoute } from '@tanstack/react-router'
import { KontoBereich, type GoogleRueckkehr } from '@/features/einstellungen/zugriff/KontoBereich'

type Suche = { google?: GoogleRueckkehr }

const rueckkehr: readonly unknown[] = ['verknuepft', 'vergeben', 'fehlgeschlagen'] satisfies GoogleRueckkehr[]

export const Route = createFileRoute('/_app/einstellungen/konto')({
  validateSearch: (suche: Record<string, unknown>): Suche =>
    rueckkehr.includes(suche.google) ? { google: suche.google as GoogleRueckkehr } : {},
  component: function KontoSeite() {
    const { google } = Route.useSearch()
    return <KontoBereich google={google} />
  },
})
