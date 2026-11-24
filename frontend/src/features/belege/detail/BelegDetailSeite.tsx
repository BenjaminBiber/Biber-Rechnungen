import { Link } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { useBeleg } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegeAbfrage } from '@/api/typen'
import { useRolle } from '@/components/shell/useRolle'
import { Brotkrumen } from './Brotkrumen'
import { Kopf } from './Kopf'
import { TabLeiste } from './TabLeiste'
import { Viewer } from './Viewer'

/** Rahmen des Beleg-Details: Brotkrumen, Kopf, links der Viewer, rechts Tabs mit dem Inhalt (`children`). */
export function BelegDetailSeite({
  belegId,
  aus,
  children,
}: {
  belegId: string
  aus?: BelegeAbfrage
  children: ReactNode
}) {
  const { data: beleg, error, isPending } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()

  if (isPending) return <main className="gedaempft px-4 py-5 lg:px-8">Beleg wird geladen …</main>
  if (!beleg) {
    const nichtGefunden = istApiFehler(error) && error.status === 404
    return (
      <main className="px-4 py-5 lg:px-8">
        <h1 className="m-0 text-[22px] font-semibold">
          {nichtGefunden ? 'Beleg nicht gefunden' : 'Beleg konnte nicht geladen werden'}
        </h1>
        <p className="gedaempft mt-2">
          <Link to="/belege">Zur Belegliste</Link>
        </p>
      </main>
    )
  }

  return (
    <main className="flex w-full flex-col gap-4 px-4 pt-5 pb-14 lg:px-8">
      <Brotkrumen beleg={beleg} aus={aus} />
      <Kopf beleg={beleg} darfBearbeiten={darfBearbeiten} />
      <div className="flex flex-col items-stretch gap-5 lg:flex-row lg:items-start">
        <Viewer key={beleg.id} beleg={beleg} />
        <section className="karte min-w-0 lg:flex-[1_1_480px]">
          <TabLeiste beleg={beleg} />
          <div className="flex flex-col gap-[18px] p-5">{children}</div>
        </section>
      </div>
    </main>
  )
}
