import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useBenachrichtigungZaehler } from '@/api/benachrichtigungen'
import { Icon } from '@/components/Icon'
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet'
import { cn } from '@/lib/utils'
import { navEintraege } from './navigation'
import { useAbmeldenUndWeiter } from './useAbmeldenUndWeiter'

const elementKlasse =
  'group flex flex-1 items-center justify-center text-gedaempft-stark no-underline aria-[current=page]:text-akzent-text'
const iconKlasse = 'text-[26px] leading-none transition-[font-size] duration-150 group-aria-[current=page]:text-[32px]'

/** Mobile Bottom-Navigation unter 1024 px (MobilNav.dc.html). */
export function BottomNavigation() {
  const [mehrOffen, setMehrOffen] = useState(false)
  const { data: zaehler } = useBenachrichtigungZaehler()
  const abmelden = useAbmeldenUndWeiter()
  const ungelesen = zaehler?.ungelesen ?? 0
  const uebrige = navEintraege.filter((e) => !['/', '/belege', '/hochladen'].includes(e.to))

  return (
    <>
      <nav
        aria-label="Mobile Navigation"
        className="fixed inset-x-0 bottom-0 z-40 flex h-[84px] items-stretch border-t border-rand bg-karte px-2 pt-1.5 pb-[22px]"
      >
        <Link to="/" activeOptions={{ exact: true }} aria-label="Übersicht" title="Übersicht" className={elementKlasse}>
          <span className="flex h-9 w-14 items-center justify-center">
            <Icon name="space_dashboard" className={iconKlasse} />
          </span>
        </Link>
        <Link to="/belege" aria-label="Belege" title="Belege" className={elementKlasse}>
          <span className="flex h-9 w-14 items-center justify-center">
            <Icon name="receipt_long" className={iconKlasse} />
          </span>
        </Link>
        <Link to="/hochladen" aria-label="Hochladen" title="Hochladen" className={elementKlasse}>
          <span className="flex h-11 w-11 items-center justify-center rounded-full border-2 border-current">
            <Icon name="add" className={iconKlasse} />
          </span>
        </Link>
        <Link
          to="/benachrichtigungen"
          aria-label={ungelesen > 0 ? `Meldungen, ${ungelesen} neu` : 'Meldungen'}
          title="Meldungen"
          className={elementKlasse}
        >
          <span className="relative flex h-9 w-14 items-center justify-center">
            <Icon name="notifications" className={iconKlasse} />
            {ungelesen > 0 && (
              <span className="absolute -top-0.5 right-2 flex h-4 min-w-4 items-center justify-center rounded-lg border-2 border-karte bg-rot-text text-[10px] font-bold text-white">
                {ungelesen}
              </span>
            )}
          </span>
        </Link>
        <button
          type="button"
          aria-label="Mehr"
          title="Mehr"
          aria-expanded={mehrOffen}
          onClick={() => setMehrOffen(true)}
          className={cn(elementKlasse, 'cursor-pointer border-0 bg-transparent')}
        >
          <span className="flex h-9 w-14 items-center justify-center">
            <Icon name="menu" className={iconKlasse} />
          </span>
        </button>
      </nav>
      <Sheet open={mehrOffen} onOpenChange={setMehrOffen}>
        <SheetContent seite="unten" aria-describedby={undefined} className="pb-8">
          <SheetTitle className="m-0 text-base font-semibold">Mehr</SheetTitle>
          <nav aria-label="Weitere Bereiche" className="flex flex-col gap-0.5">
            {uebrige.map((e) => (
              <Link
                key={e.id}
                to={e.to}
                onClick={() => setMehrOffen(false)}
                className="flex h-11 items-center gap-3 rounded-lg px-3 text-[15px] text-text no-underline hover:bg-hover aria-[current=page]:bg-akzent-zart aria-[current=page]:text-akzent-text"
              >
                <Icon name={e.icon} className="text-[22px]" />
                {e.label}
              </Link>
            ))}
            <button
              type="button"
              onClick={() => {
                setMehrOffen(false)
                void abmelden()
              }}
              className="mt-2 flex h-11 cursor-pointer items-center gap-3 rounded-lg border-0 border-t border-linie bg-transparent px-3 text-[15px] text-rot-text"
            >
              <Icon name="logout" className="text-[22px]" />
              Abmelden
            </button>
          </nav>
        </SheetContent>
      </Sheet>
    </>
  )
}
