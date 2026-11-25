import {
  createContext,
  useContext,
  useEffect,
  useEffectEvent,
  useId,
  useLayoutEffect,
  useRef,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'
import { Icon } from '@/components/Icon'
import { cn } from '@/lib/utils'

/**
 * Stelle rechts unter der Kopfleiste (neben dem Seiteninhalt), an der ein Seitenpanel einfährt.
 * Der App-Rahmen stellt das Element bereit; ohne Rahmen bleibt das Panel unsichtbar.
 */
export const SeitenpanelZiel = createContext<HTMLElement | null>(null)

const breite = 'w-[clamp(380px,40vw,720px)]'
const weich = 'ease-[cubic-bezier(0.65,0,0.35,1)] motion-reduce:transition-none'

/**
 * Nicht-modales Panel, das auf dem Desktop unter der Kopfleiste von rechts einfährt.
 * Es liegt im Fluss neben dem Seiteninhalt: Dieser wird schmaler, statt verdeckt zu werden.
 * Das Panel reicht immer bis zum unteren Fensterrand; ist die Kopfleiste weggescrollt, bis ganz nach oben.
 * Escape und der Schließen-Knopf schließen es; `onGeschlossen` meldet das Ende der Ausfahr-Animation.
 *
 * `spalte`: fester Teil einer Seite (z. B. das Original im Beleg-Detail) statt eines Dialogs –
 * ohne Fokussprung beim Öffnen und ohne Escape, damit Eingaben im Seiteninhalt es nicht schließen.
 */
export function Seitenpanel({
  offen,
  titel,
  beschreibung,
  aktionen,
  onSchliessen,
  onGeschlossen,
  spalte = false,
  children,
}: {
  offen: boolean
  titel: ReactNode
  beschreibung?: ReactNode
  /** Zusätzliche Knöpfe im Kopf vor „Schließen“. */
  aktionen?: ReactNode
  onSchliessen: () => void
  onGeschlossen?: () => void
  spalte?: boolean
  children: ReactNode
}) {
  const ziel = useContext(SeitenpanelZiel)
  const titelId = useId()
  const beschreibungId = useId()
  const aussenRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLElement>(null)
  const schliessen = useEffectEvent(onSchliessen)

  // Höhe: vom sichtbaren Anfang der Panel-Spalte bis zum Fensterrand (ohne Neu-Rendern beim Scrollen).
  useLayoutEffect(() => {
    const aussen = aussenRef.current
    if (!ziel || !aussen) return
    const anpassen = () => {
      const oben = Math.max(0, ziel.getBoundingClientRect().top)
      aussen.style.height = `${Math.max(0, window.innerHeight - oben)}px`
    }
    anpassen()
    window.addEventListener('scroll', anpassen, { passive: true })
    window.addEventListener('resize', anpassen)
    return () => {
      window.removeEventListener('scroll', anpassen)
      window.removeEventListener('resize', anpassen)
    }
  }, [ziel])

  // Escape schließt – außer eine andere Ebene (Dialog, Menü) hat die Taste schon verbraucht.
  useEffect(() => {
    if (!offen || spalte) return
    const beiTaste = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !e.defaultPrevented) schliessen()
    }
    window.addEventListener('keydown', beiTaste)
    return () => window.removeEventListener('keydown', beiTaste)
  }, [offen, spalte])

  // Beim Öffnen den Fokus ins Panel, beim Schließen zurück zum Auslöser.
  useEffect(() => {
    const panel = panelRef.current
    if (!offen || spalte || !panel) return
    const vorher = document.activeElement instanceof HTMLElement ? document.activeElement : null
    panel.focus({ preventScroll: true })
    return () => {
      if (panel.contains(document.activeElement) && vorher?.isConnected) vorher.focus({ preventScroll: true })
    }
  }, [offen, spalte])

  if (!ziel) return null

  return createPortal(
    <div
      ref={aussenRef}
      className={cn(
        'sticky top-0 flex-none overflow-hidden transition-[width] duration-[280ms]',
        weich,
        offen ? breite : 'w-0',
      )}
      onTransitionEnd={(e) => {
        if (e.target === e.currentTarget && !offen) onGeschlossen?.()
      }}
    >
      <section
        ref={panelRef}
        role={spalte ? 'complementary' : 'dialog'}
        aria-modal={spalte ? undefined : false}
        aria-labelledby={titelId}
        aria-describedby={beschreibung ? beschreibungId : undefined}
        aria-hidden={!offen || undefined}
        inert={!offen}
        tabIndex={-1}
        className={cn('flex h-full flex-col gap-3 border-l border-rand bg-karte p-4 text-text outline-none', breite)}
      >
        <header className="flex items-center gap-2">
          <div className="flex min-w-0 flex-1 flex-col gap-1">
            <h2 id={titelId} className="m-0 text-[17px] font-semibold text-text">
              {titel}
            </h2>
            {beschreibung && (
              <p id={beschreibungId} className="m-0 truncate text-sm text-gedaempft">
                {beschreibung}
              </p>
            )}
          </div>
          {aktionen}
          <button
            type="button"
            onClick={onSchliessen}
            className="btn btn-klein w-[30px] flex-none border-transparent px-0"
          >
            <Icon name="close" label="Schließen" />
          </button>
        </header>
        {children}
      </section>
    </div>,
    ziel,
  )
}
