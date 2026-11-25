import { createFileRoute, Outlet } from '@tanstack/react-router'
import { Seitenkopf } from '@/components/Seitenkopf'
import { EinstellungenAuswahl } from '@/features/einstellungen/EinstellungenNavigation'

/**
 * Layout der Einstellungen (Einstellungen.dc.html): der gewählte Bereich als Karte.
 * Ab 1024 px steht die Bereichsnavigation samt Überschrift als eigene Spalte im App-Rahmen (`_app.tsx`),
 * darunter gibt es Überschrift und Auswahlliste hier über der Karte.
 */
export const Route = createFileRoute('/_app/einstellungen')({
  component: EinstellungenLayout,
})

function EinstellungenLayout() {
  return (
    <main className="seite">
      <div className="mb-6 lg:hidden">
        <Seitenkopf titel="Einstellungen" />
        <EinstellungenAuswahl />
      </div>
      <section className="karte flex min-w-0 flex-col gap-[18px] p-6">
        <Outlet />
      </section>
    </main>
  )
}
