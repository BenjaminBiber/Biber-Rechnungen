import { Link, useRouterState } from '@tanstack/react-router'
import { useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import type { BelegDetail } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { fehlgeschlageneBefunde } from './gemeinsam'

const tabKlasse =
  'inline-flex h-[46px] flex-none items-center gap-1.5 whitespace-nowrap border-0 border-b-2 border-transparent px-2.5 text-sm font-medium text-gedaempft-stark no-underline hover:text-text'
const aktivKlasse = 'border-akzent text-text'
/** Abstand zwischen den Tabs (gap-0.5). */
const ABSTAND = 2
/** Platz für den Knopf „Mehr ▾“ (bzw. den Namen des aktiven Tabs darin). */
const MEHR_BREITE = 120

type Tab = {
  to:
    | '/belege/$belegId'
    | '/belege/$belegId/positionen'
    | '/belege/$belegId/pruefung'
    | '/belege/$belegId/historie'
    | '/belege/$belegId/dateien'
    | '/belege/$belegId/dubletten'
  label: string
  zaehler?: number
  orange?: boolean
}

function Inhalt({ tab }: { tab: Tab }): ReactNode {
  return (
    <>
      {tab.label}
      {tab.zaehler !== undefined && (
        <Chip ton={tab.orange ? 'orange' : 'grau'}>
          {tab.zaehler}
          {tab.orange && <span className="sr-only"> fehlgeschlagen</span>}
        </Chip>
      )}
    </>
  )
}

/**
 * Wie viele Tabs passen in die Leiste? Gemessen an einer unsichtbaren Kopie aller Tabs; was nicht passt,
 * wandert ins Menü „Mehr“. Ohne Layout (jsdom) passen alle.
 */
function useSichtbareAnzahl(anzahl: number) {
  const leisteRef = useRef<HTMLElement>(null)
  const messRef = useRef<HTMLDivElement>(null)
  const [sichtbar, setSichtbar] = useState(anzahl)

  useLayoutEffect(() => {
    const leiste = leisteRef.current
    const mess = messRef.current
    if (!leiste || !mess) return
    const berechnen = () => {
      const stil = getComputedStyle(leiste)
      const frei = leiste.clientWidth - parseFloat(stil.paddingLeft) - parseFloat(stil.paddingRight)
      const breiten = Array.from(mess.children, (k) => (k as HTMLElement).offsetWidth)
      const gesamt = breiten.reduce((s, b) => s + b, 0) + ABSTAND * (breiten.length - 1)
      if (frei <= 0 || gesamt <= frei) return setSichtbar(breiten.length)
      let summe = 0
      let passend = 0
      for (const b of breiten) {
        if (summe + b + ABSTAND + MEHR_BREITE > frei) break
        summe += b + ABSTAND
        passend++
      }
      setSichtbar(Math.max(1, passend))
    }
    berechnen()
    if (typeof ResizeObserver === 'undefined') return
    const beobachter = new ResizeObserver(berechnen)
    beobachter.observe(leiste)
    beobachter.observe(mess)
    return () => beobachter.disconnect()
  }, [anzahl])

  return { leisteRef, messRef, sichtbar }
}

/**
 * Tab-Leiste des Beleg-Details; jeder Tab ist eine verschachtelte Route.
 * Auf schmalen Breiten stehen die Tabs, die nicht mehr passen, im Menü „Mehr“ statt quer zu scrollen.
 */
export function TabLeiste({ beleg }: { beleg: BelegDetail }) {
  const befunde = fehlgeschlageneBefunde(beleg).length
  const tabs: Tab[] = [
    { to: '/belege/$belegId', label: 'Daten' },
    { to: '/belege/$belegId/positionen', label: 'Positionen', zaehler: beleg.positionen.length },
    { to: '/belege/$belegId/pruefung', label: 'Prüfung', zaehler: befunde || undefined, orange: true },
    { to: '/belege/$belegId/historie', label: 'Historie' },
    { to: '/belege/$belegId/dateien', label: 'Dateien', zaehler: beleg.dateien.length },
    { to: '/belege/$belegId/dubletten', label: 'Dubletten' },
  ]
  const pfad = useRouterState({ select: (s) => s.location.pathname })
  const { leisteRef, messRef, sichtbar } = useSichtbareAnzahl(tabs.length)
  const ueberlauf = tabs.slice(sichtbar)
  const aktivImMenue = ueberlauf.find((t) => t.to.replace('$belegId', beleg.id) === pfad)

  const link = (tab: Tab) => ({
    to: tab.to,
    params: { belegId: beleg.id },
    search: (alt: Record<string, unknown>) => ({ aus: alt.aus }) as never,
  })

  return (
    <nav aria-label="Belegdaten" ref={leisteRef} className="relative flex gap-0.5 border-b border-rand px-3">
      {/* Unsichtbare Kopie aller Tabs nur zum Messen ihrer Breiten; der Rahmen ohne Größe verbreitert die Leiste nicht. */}
      <div aria-hidden="true" className="pointer-events-none invisible absolute top-0 left-0 size-0 overflow-hidden">
        <div ref={messRef} className="flex w-max gap-0.5 whitespace-nowrap">
          {tabs.map((tab) => (
            <span key={tab.to} className={tabKlasse}>
              <Inhalt tab={tab} />
            </span>
          ))}
        </div>
      </div>

      {tabs.slice(0, sichtbar).map((tab) => (
        <Link
          key={tab.to}
          {...link(tab)}
          activeOptions={{ exact: true, includeSearch: false }}
          className={tabKlasse}
          activeProps={{ className: aktivKlasse, 'aria-current': 'page' }}
        >
          <Inhalt tab={tab} />
        </Link>
      ))}

      {ueberlauf.length > 0 && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className={cn(tabKlasse, 'cursor-pointer bg-transparent', aktivImMenue && aktivKlasse)}
              aria-label={aktivImMenue ? `Weitere Tabs, aktiv: ${aktivImMenue.label}` : 'Weitere Tabs'}
            >
              {aktivImMenue ? <Inhalt tab={aktivImMenue} /> : 'Mehr'}
              <Icon name="expand_more" className="text-[18px]" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-[180px]">
            {ueberlauf.map((tab) => (
              <DropdownMenuItem key={tab.to} asChild>
                <Link
                  {...link(tab)}
                  activeOptions={{ exact: true, includeSearch: false }}
                  activeProps={{ 'aria-current': 'page', className: 'font-semibold' }}
                  className="flex items-center justify-between gap-2 text-text no-underline hover:text-text"
                >
                  <Inhalt tab={tab} />
                </Link>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </nav>
  )
}
