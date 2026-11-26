import { Link } from '@tanstack/react-router'
import { useLayoutEffect, useState, type ReactNode } from 'react'
import { useBeleg } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegeAbfrage } from '@/api/typen'
import { useRolle } from '@/components/shell/useRolle'
import { LadeBereich, Skelett, SkelettFormular } from '@/components/ui/skelett'
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

  if (isPending) return <BelegDetailSkelett />
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
      {/* `relative`: Der Auto-Save-Status im Tab „Daten“ sitzt fest in der unteren rechten Ecke der Karte. */}
      <section className="karte relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <TabLeiste beleg={beleg} />
        <div className="flex min-h-0 flex-1 flex-col gap-[18px] overflow-y-auto p-5">{children}</div>
      </section>
      <Viewer key={beleg.id} beleg={beleg} offen={originalOffen} onSchliessen={() => setOriginalOffen(false)} />
    </main>
  )
}

/** Platzhalter in der Form des Details: Kopf mit Logo und Aktionen, Tab-Leiste, Formularfelder. */
function BelegDetailSkelett() {
  return (
    <LadeBereich als="main" label="Beleg wird geladen" className="seite flex flex-col gap-3 pt-4! pb-4!">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <div className="flex min-w-0 flex-[1_1_320px] items-center gap-3">
          <Skelett breite={36} hoehe={36} className="rounded-[var(--radius-button)]" />
          <Skelett rund breite={36} hoehe={36} />
          <Skelett hoehe={22} breite={260} className="max-w-[50vw]" />
        </div>
        <div className="ml-auto flex gap-2">
          <Skelett hoehe={36} breite={96} className="rounded-[var(--radius-button)]" />
          <Skelett hoehe={36} breite={120} className="rounded-[var(--radius-button)]" />
        </div>
      </div>
      <section className="karte flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <div className="flex h-11 items-center gap-6 border-b border-rand px-5">
          {[64, 84, 72, 92, 70].map((b, i) => (
            <Skelett key={i} hoehe={12} breite={b} />
          ))}
        </div>
        <div className="p-5">
          <SkelettFormular felder={10} label={null} />
        </div>
      </section>
    </LadeBereich>
  )
}
