import { Link } from '@tanstack/react-router'
import { useAehnlicheBelege } from '@/api/belege'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { SkelettListe } from '@/components/ui/skelett'
import { formatDatum, formatEuro } from '@/lib/format'

/** Knopf „Ähnliche“ der großen Karte mit Popover der ähnlichen Belege. */
export function AehnlichePopover({ beleg }: { beleg: BelegListeneintrag }) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button type="button">
          <Icon name="account_tree" />
          Ähnliche
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-80 p-1.5" aria-label="Ähnliche Belege">
        <AehnlicheListe id={beleg.id} />
      </PopoverContent>
    </Popover>
  )
}

function AehnlicheListe({ id }: { id: string }) {
  const { data, isPending, isError } = useAehnlicheBelege(id)
  if (isPending)
    return <SkelettListe zeilen={3} symbol={false} label="Ähnliche Belege werden geladen" className="px-2.5" />
  if (isError)
    return <p className="m-0 px-2.5 py-2 text-sm text-rot-text">Ähnliche Belege konnten nicht geladen werden.</p>
  if (data.length === 0) return <p className="m-0 px-2.5 py-2 text-sm text-gedaempft">Keine ähnlichen Belege</p>
  return (
    <ul className="m-0 list-none p-0">
      {data.map((b) => (
        <li key={b.id}>
          <Link
            to="/belege/$belegId"
            params={{ belegId: b.id }}
            className="flex items-center gap-2 rounded-md px-2.5 py-2 text-sm text-text no-underline hover:bg-hover"
          >
            <span className="min-w-0 flex-1">
              <span className="block truncate font-medium">{b.titel}</span>
              <span className="block text-xs text-gedaempft">
                {[b.rechnungsnummer, b.rechnungsdatum && formatDatum(b.rechnungsdatum)].filter(Boolean).join(' · ')}
              </span>
            </span>
            {b.brutto !== null && b.brutto !== undefined && (
              <span className="num font-semibold">{formatEuro(b.brutto, b.waehrung)}</span>
            )}
          </Link>
        </li>
      ))}
    </ul>
  )
}
