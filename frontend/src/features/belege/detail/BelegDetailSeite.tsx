import { Link } from '@tanstack/react-router'
import { useLayoutEffect, useState, type ReactNode } from 'react'
import { useBeleg } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegeAbfrage } from '@/api/typen'
import { useRolle } from '@/components/shell/useRolle'
import { Kopf } from './Kopf'
import { TabLeiste } from './TabLeiste'
import { Viewer } from './Viewer'

/**
 * Begrenzt ein Element auf die Fensterhöhe ab seiner Oberkante im Dokument (unter der Kopfleiste),
 * damit nur sein Inhalt scrollt und nicht die ganze Seite. Callback-Ref, weil das Element erst nach dem Laden erscheint.
 */
function useFensterhoehe() {
  const [element, setElement] = useState<HTMLElement | null>(null)
  const [hoehe, setHoehe] = useState<number>()
  useLayoutEffect(() => {
    if (!element) return
    const anpassen = () => {
      const oben = element.getBoundingClientRect().top + window.scrollY
      setHoehe(Math.max(320, window.innerHeight - oben))
    }
    anpassen()
    window.addEventListener('resize', anpassen)
    return () => window.removeEventListener('resize', anpassen)
  }, [element])
  return [setElement, hoehe] as const
}

/**
 * Rahmen des Beleg-Details: Kopf (mit Zurück und Blättern), darunter die Tabs mit dem Inhalt (`children`);
 * rechts das Original als Panel wie die Vorschau der Belegliste.
 */
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
  // Bleibt beim Blättern zum nächsten Beleg erhalten.
  const [originalOffen, setOriginalOffen] = useState(true)
  const [hoeheMessen, hoehe] = useFensterhoehe()

  if (isPending) return <main className="seite gedaempft">Beleg wird geladen …</main>
  if (!beleg) {
    const nichtGefunden = istApiFehler(error) && error.status === 404
    return (
      <main className="seite">
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
    // Höchstens so hoch wie das Fenster: Kopf und Tabs bleiben stehen, nur der Tab-Inhalt scrollt.
    <main ref={hoeheMessen} style={{ height: hoehe }} className="seite flex flex-col gap-3 pt-4! pb-4!">
      <Kopf
        beleg={beleg}
        aus={aus}
        darfBearbeiten={darfBearbeiten}
        onOriginalZeigen={originalOffen ? undefined : () => setOriginalOffen(true)}
      />
      <section className="karte flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <TabLeiste beleg={beleg} />
        <div className="flex min-h-0 flex-1 flex-col gap-[18px] overflow-y-auto p-5">{children}</div>
      </section>
      <Viewer key={beleg.id} beleg={beleg} offen={originalOffen} onSchliessen={() => setOriginalOffen(false)} />
    </main>
  )
}
