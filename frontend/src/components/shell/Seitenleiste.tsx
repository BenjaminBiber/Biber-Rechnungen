import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useBelegZaehler } from '@/api/belege'
import { Icon } from '@/components/Icon'
import { Logo } from '@/components/Logo'
import { cn } from '@/lib/utils'
import { navGruppen } from './navigation'

const SPEICHER_SCHLUESSEL = 'seitenleiste-eingeklappt'

function gespeichert(): boolean {
  try {
    return localStorage.getItem(SPEICHER_SCHLUESSEL) === 'true'
  } catch {
    return false
  }
}

/** Eingeklappt-Zustand der Seitenleiste, im Browser gemerkt. */
function useEingeklappt(): [boolean, () => void] {
  const [eingeklappt, setEingeklappt] = useState(gespeichert)
  const umschalten = () =>
    setEingeklappt((alt) => {
      try {
        localStorage.setItem(SPEICHER_SCHLUESSEL, String(!alt))
      } catch {
        // Ohne Speicher gilt der Zustand nur bis zum Neuladen.
      }
      return !alt
    })
  return [eingeklappt, umschalten]
}

/** Weiches Ein-/Ausgleiten: ease-in-out (easeInOutCubic) für Breite, Pfeil und Badge. */
const weich = 'ease-[cubic-bezier(0.65,0,0.35,1)] motion-reduce:transition-none'

/**
 * Seitenleiste ab 1024 px (Sidebar.dc.html): 240 px breit oder eingeklappt 72 px nur mit Icons.
 * Der aktive Eintrag kommt vom Router; eingeklappt zeigen Tooltips die Namen.
 */
export function Seitenleiste() {
  const { data: zaehler } = useBelegZaehler()
  const inbox = zaehler?.inbox ?? 0
  const [eingeklappt, umschalten] = useEingeklappt()

  /** Beschriftungen blenden beim Einklappen sofort aus und beim Ausklappen leicht verzögert ein. */
  const text = cn(
    'transition-[opacity,transform] ease-in-out motion-reduce:transition-none',
    weich,
    eingeklappt
      ? '-translate-x-1 opacity-0 duration-150 delay-0'
      : 'translate-x-0 opacity-100 duration-200 delay-[140ms]',
  )

  return (
    <div className="flex flex-none bg-seitenleiste">
      <nav
        aria-label="Hauptnavigation"
        className={cn(
          'sticky top-0 flex h-screen flex-col gap-2.5 overflow-x-hidden overflow-y-auto bg-seitenleiste px-3 py-5 text-white transition-[width] duration-[280ms] will-change-[width]',
          weich,
          eingeklappt ? 'w-[72px]' : 'w-[240px]',
        )}
      >
        <Link
          to="/"
          aria-label="Biber-Rechnungen, zum Dashboard"
          title={eingeklappt ? 'Biber-Rechnungen' : undefined}
          className="mb-1.5 flex h-11 flex-none items-center gap-3 overflow-hidden rounded-[9px] px-0.5 outline-none focus-visible:shadow-[inset_0_0_0_2px_rgba(255,255,255,0.85)] whitespace-nowrap text-white no-underline hover:text-white"
        >
          <Logo className="h-[26px] w-11 text-white" />
          <span className={cn('text-[15px] font-semibold', text)}>Biber-Rechnungen</span>
        </Link>

        {navGruppen.map((gruppe) => (
          <div key={gruppe.label} role="group" aria-label={gruppe.label} className="flex flex-col gap-0.5">
            <div aria-hidden="true" className="relative h-[26px] whitespace-nowrap">
              <span
                className={cn(
                  'absolute bottom-1.5 left-[13px] text-[11px] font-semibold tracking-[0.08em] text-seitenleiste-gedaempft uppercase',
                  text,
                )}
              >
                {gruppe.label}
              </span>
              <span
                className={cn(
                  'absolute top-3 left-1 h-px w-10 bg-seitenleiste-linie transition-opacity duration-200 motion-reduce:transition-none',
                  eingeklappt ? 'opacity-100' : 'opacity-0',
                )}
              />
            </div>
            {gruppe.eintraege.map((e) => {
              const badge = e.id === 'belege' && inbox > 0 ? inbox : undefined
              return (
                <Link
                  key={e.id}
                  to={e.to}
                  activeOptions={{ exact: e.to === '/' }}
                  aria-label={badge ? `${e.label}, ${badge} in der Inbox` : undefined}
                  title={eingeklappt ? e.label : undefined}
                  className="relative flex h-[42px] flex-none items-center overflow-hidden outline-none focus-visible:shadow-[inset_0_0_0_2px_rgba(255,255,255,0.85)] gap-3.5 rounded-[9px] pl-[13px] text-sm font-medium whitespace-nowrap text-white no-underline transition-colors duration-150 hover:bg-seitenleiste-hover hover:text-white data-[status=active]:bg-seitenleiste-aktiv"
                >
                  <Icon name={e.icon} className="w-[22px] flex-none text-[22px]" />
                  <span className={text}>{e.label}</span>
                  {badge && (
                    <span
                      aria-hidden="true"
                      className={cn(
                        'absolute inline-flex h-5 min-w-[22px] origin-top-right items-center justify-center rounded-[10px] bg-seitenleiste-linie px-1.5 text-xs font-semibold text-white transition-transform duration-[280ms] top-[11px] right-2.5',
                        weich,
                        eingeklappt ? 'translate-x-[9px] -translate-y-2 scale-[0.82]' : 'translate-0 scale-100',
                      )}
                    >
                      {badge}
                    </span>
                  )}
                </Link>
              )
            })}
          </div>
        ))}

        <span className="flex-1" />
        <button
          type="button"
          onClick={umschalten}
          aria-expanded={!eingeklappt}
          aria-label={eingeklappt ? 'Seitenleiste ausklappen' : 'Seitenleiste einklappen'}
          title={eingeklappt ? 'Ausklappen' : undefined}
          className="flex h-[42px] flex-none cursor-pointer items-center overflow-hidden outline-none focus-visible:shadow-[inset_0_0_0_2px_rgba(255,255,255,0.85)] gap-3.5 rounded-[9px] border-0 bg-transparent pl-[13px] text-left text-sm font-medium whitespace-nowrap text-seitenleiste-gedaempft transition-colors duration-150 hover:bg-seitenleiste-hover"
        >
          <Icon
            name="keyboard_double_arrow_left"
            className={cn(
              'w-[22px] flex-none text-[22px] transition-transform duration-[280ms]',
              weich,
              eingeklappt && 'rotate-180',
            )}
          />
          <span className={text}>Einklappen</span>
        </button>
      </nav>
    </div>
  )
}
