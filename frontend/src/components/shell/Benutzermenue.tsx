import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useAuthStatus } from '@/api/auth'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { type Darstellung, useDarstellung } from '@/lib/theme'
import { cn } from '@/lib/utils'
import { useAbmeldenUndWeiter } from './useAbmeldenUndWeiter'

const eintraege = [
  { icon: 'person', label: 'Mein Konto', to: '/einstellungen/konto' },
  { icon: 'phonelink_lock', label: 'Zwei-Faktor-Anmeldung', to: '/einstellungen/konto' },
  { icon: 'key', label: 'API-Tokens (MCP)', to: '/einstellungen/api-tokens' },
  { icon: 'description', label: 'Verfahrensdokumentation', to: '/einstellungen/verfahrensdoku' },
] as const

const darstellungen: Array<[Darstellung, string]> = [
  ['hell', 'Hell'],
  ['dunkel', 'Dunkel'],
  ['system', 'System'],
]

/** „M. Keller“ aus „Martin Keller“. */
function kurzname(name: string): string {
  const teile = name.trim().split(/\s+/)
  return teile.length < 2 ? name : `${teile[0][0]}. ${teile[teile.length - 1]}`
}

/**
 * Schlichter Auslöser wie bei paperless-ngx: Personen-Icon und Kurzname (mobil nur das Icon);
 * Popover mit Konto-Links, Darstellung und Abmelden (Menues.dc.html).
 */
export function Benutzermenue({ kompakt = false }: { kompakt?: boolean }) {
  const [offen, setOffen] = useState(false)
  const { data: status } = useAuthStatus()
  const [darstellung, setzeDarstellung] = useDarstellung()
  const abmelden = useAbmeldenUndWeiter()
  const benutzer = status?.benutzer
  const name = benutzer?.name ?? ''

  return (
    <Popover open={offen} onOpenChange={setOffen}>
      <PopoverTrigger
        aria-label="Benutzermenü"
        className={cn(
          'flex h-10 cursor-pointer items-center gap-1.5 rounded-lg border-0 font-semibold text-text hover:bg-hover',
          kompakt ? 'w-10 justify-center px-0' : 'px-2',
          offen ? 'bg-hover' : 'bg-transparent',
        )}
      >
        <Icon name="account_circle" className="text-[24px] [vertical-align:0]" />
        {!kompakt && <span className="text-sm">{kurzname(name)}</span>}
      </PopoverTrigger>
      <PopoverContent align="end" sideOffset={8} aria-label="Benutzermenü" className="popover-animiert w-[280px] p-1.5">
        <div className="mb-1.5 border-b border-linie px-3 pt-2.5 pb-3">
          <div className="text-sm font-semibold">{name}</div>
          <div className="text-xs text-gedaempft">
            Rolle: {benutzer?.rolle} · 2FA {benutzer?.totpAktiv ? 'aktiv' : 'inaktiv'}
          </div>
        </div>
        {eintraege.map((e) => (
          <Link
            key={e.label}
            to={e.to}
            onClick={() => setOffen(false)}
            className="flex h-[38px] items-center gap-2.5 rounded-md px-2.5 text-sm text-text no-underline hover:bg-hover"
          >
            <Icon name={e.icon} className="text-[20px]" />
            {e.label}
          </Link>
        ))}
        <div className="mt-1.5 border-t border-linie px-2.5 pt-2 pb-1.5">
          <div className="mb-1.5 text-xs text-gedaempft" id="darstellung-label">
            Darstellung
          </div>
          <div role="group" aria-labelledby="darstellung-label" className="flex gap-1 rounded-lg bg-hover p-[3px]">
            {darstellungen.map(([wert, text]) => (
              <button
                key={wert}
                type="button"
                aria-pressed={darstellung === wert}
                onClick={() => setzeDarstellung(wert)}
                className={cn(
                  'h-[30px] flex-1 cursor-pointer rounded-md border-0 text-[13px] font-medium',
                  darstellung === wert ? 'bg-karte text-text shadow-segment' : 'bg-transparent text-gedaempft-stark',
                )}
              >
                {text}
              </button>
            ))}
          </div>
        </div>
        <button
          type="button"
          onClick={() => {
            setOffen(false)
            void abmelden()
          }}
          className="mt-1 flex h-[38px] w-full cursor-pointer items-center gap-2.5 border-0 border-t border-linie bg-transparent px-2.5 text-sm text-rot-text"
        >
          <Icon name="logout" className="text-[20px]" />
          Abmelden
        </button>
      </PopoverContent>
    </Popover>
  )
}
