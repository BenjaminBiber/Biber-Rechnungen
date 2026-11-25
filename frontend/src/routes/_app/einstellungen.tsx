import { createFileRoute, Outlet } from '@tanstack/react-router'
import { Seitenkopf } from '@/components/Seitenkopf'
import { EinstellungenNavigation } from '@/features/einstellungen/EinstellungenNavigation'

/** Layout der Einstellungen: Bereichsnavigation links, der gewählte Bereich als Karte rechts. */
export const Route = createFileRoute('/_app/einstellungen')({
  component: EinstellungenLayout,
})

function EinstellungenLayout() {
  return (
    <main className="seite">
      <Seitenkopf titel="Einstellungen" />
      <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
        <EinstellungenNavigation />
        <section className="karte flex min-w-0 flex-1 flex-col gap-[18px] p-6">
          <Outlet />
        </section>
      </div>
    </main>
  )
}
