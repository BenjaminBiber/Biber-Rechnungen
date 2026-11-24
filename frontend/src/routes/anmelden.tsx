import { createFileRoute, redirect } from '@tanstack/react-router'
import { authKeys } from '@/api/auth'
import { api, daten } from '@/api/client'
import { AnmeldeRahmen } from '@/features/anmeldung/AnmeldeRahmen'
import { AnmeldenFormular } from '@/features/anmeldung/AnmeldenFormular'
import { Ersteinrichtung } from '@/features/anmeldung/Ersteinrichtung'
import { FreigabeHinweis } from '@/features/anmeldung/FreigabeHinweis'
import { anmeldeFehler, schritte, type AnmeldeFehler, type Schritt } from '@/features/anmeldung/gemeinsam'
import { RegistrierenFormular } from '@/features/anmeldung/RegistrierenFormular'
import { TotpFormular } from '@/features/anmeldung/TotpFormular'

type AnmeldenSuche = { schritt?: Schritt; fehler?: AnmeldeFehler; email?: string }

function auswahl<T extends string>(werte: readonly T[], wert: unknown): T | undefined {
  return werte.find((w) => w === wert)
}

/** Anmeldeseite in fünf Schritten (Login.dc.html). */
export const Route = createFileRoute('/anmelden')({
  validateSearch: (suche: Record<string, unknown>): AnmeldenSuche => ({
    schritt: auswahl(schritte, suche.schritt),
    fehler: auswahl(anmeldeFehler, suche.fehler),
    email: typeof suche.email === 'string' ? suche.email : undefined,
  }),
  beforeLoad: async ({ context, search }) => {
    // Gleicher Schlüssel wie `useAuthStatus`, damit die Formulare den Status aus dem Cache lesen.
    const status = await context.queryClient.fetchQuery({
      queryKey: authKeys.status(),
      queryFn: ({ signal }) => daten(api.GET('/api/auth/status', { signal })),
    })
    if (!status.eingerichtet) {
      if (search.schritt !== 'ersteinrichtung')
        throw redirect({ to: '/anmelden', search: { schritt: 'ersteinrichtung' } })
      return
    }
    if (status.angemeldet) throw redirect({ to: '/' })
    if (search.schritt === 'ersteinrichtung') throw redirect({ to: '/anmelden', search: {} })
  },
  component: AnmeldenSeite,
})

function AnmeldenSeite() {
  const { schritt = 'anmelden', fehler, email } = Route.useSearch()
  return (
    <AnmeldeRahmen>
      {schritt === 'anmelden' && <AnmeldenFormular fehler={fehler} />}
      {schritt === 'totp' && <TotpFormular />}
      {schritt === 'registrieren' && <RegistrierenFormular />}
      {schritt === 'freigabe' && <FreigabeHinweis email={email} />}
      {schritt === 'ersteinrichtung' && <Ersteinrichtung />}
    </AnmeldeRahmen>
  )
}
