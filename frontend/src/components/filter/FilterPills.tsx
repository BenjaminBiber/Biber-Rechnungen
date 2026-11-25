import { useRef, useState, type ComponentProps, type ReactElement, type ReactNode } from 'react'
import { Icon } from '@/components/Icon'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'

/** Ein Filter der Pill-Leiste. */
export type PillFilter = {
  schluessel: string
  titel: string
  icon: string
  /** Kurztext des gesetzten Werts („Hostwerk Süd GmbH“, „2 gewählt“ …), leer wenn nicht gesetzt. */
  wert: string
  /** Auswahl (Popover/Menü) um den gegebenen Auslöser; `anfangsOffen` öffnet sie beim Hinzufügen sofort. */
  auswahl: (ausloeser: ReactElement, anfangsOffen: boolean) => ReactNode
  /** Löscht den Filterwert (× der Pill). */
  entfernen: () => void
}

/** Auslöser einer Filter-Pill: „Titel: Wert“ bzw. „Titel wählen …“, solange nichts gesetzt ist. */
function PillKnopf({
  titel,
  icon,
  wert,
  className,
  ...props
}: ComponentProps<'button'> & { titel: string; icon: string; wert: string }) {
  return (
    <button
      type="button"
      className={cn(
        'inline-flex h-full min-w-0 cursor-pointer items-center gap-1.5 pr-1 pl-2.5 hover:bg-akzent-zart data-[state=open]:bg-akzent-zart [&_.material-symbols-outlined]:text-[17px]',
        className,
      )}
      aria-label={wert ? `${titel}: ${wert}` : `${titel} wählen`}
      {...props}
    >
      <Icon name={icon} />
      <span className="whitespace-nowrap">{wert ? `${titel}:` : titel}</span>
      {wert ? (
        <span className="max-w-[220px] truncate font-semibold">{wert}</span>
      ) : (
        <span className="font-normal whitespace-nowrap text-gedaempft">wählen …</span>
      )}
      <Icon name="arrow_drop_down" />
    </button>
  )
}

/**
 * Pill-Filterleiste (Belege, Artikel, Stammartikel): Anfangs nur „Filter hinzufügen“. Jeder gewählte Filter
 * erscheint als Pill, deren Auswahl sich sofort öffnet; das × entfernt ihn wieder. Gesetzte Filter (URL,
 * gespeicherte Ansicht) erscheinen von selbst als Pill. „Filter zurücksetzen“ erscheint, sobald eine Pill steht
 * oder `weitereAktiv` gilt (z. B. ein Suchbegriff).
 */
export function FilterPills({
  filter,
  onZuruecksetzen,
  weitereAktiv = false,
  className,
}: {
  /** Filter in der Reihenfolge des Menüs „Filter hinzufügen“. */
  filter: PillFilter[]
  onZuruecksetzen: () => void
  weitereAktiv?: boolean
  className?: string
}) {
  // Hinzugefügte Pills (auch noch ohne Wert) in Reihenfolge; `neu` öffnet seine Auswahl beim Erscheinen.
  const [hinzugefuegt, setHinzugefuegt] = useState<string[]>([])
  const [neu, setNeu] = useState<string | null>(null)
  const gewaehlt = useRef(false)

  const nachSchluessel = new Map(filter.map((f) => [f.schluessel, f]))
  const sichtbar = [
    ...hinzugefuegt.filter((s) => nachSchluessel.has(s)),
    ...filter.filter((f) => f.wert && !hinzugefuegt.includes(f.schluessel)).map((f) => f.schluessel),
  ].map((s) => nachSchluessel.get(s)!)
  const verfuegbar = filter.filter((f) => !sichtbar.includes(f))

  function hinzufuegen(schluessel: string) {
    gewaehlt.current = true
    setNeu(schluessel)
    setHinzugefuegt((alt) => [...alt, schluessel])
  }

  function entfernen(f: PillFilter) {
    f.entfernen()
    setHinzugefuegt((alt) => alt.filter((s) => s !== f.schluessel))
    if (neu === f.schluessel) setNeu(null)
  }

  return (
    <div role="search" aria-label="Filter" className={cn('flex flex-wrap items-center gap-2', className)}>
      {sichtbar.map((f) => (
        <span
          key={f.schluessel}
          role="group"
          aria-label={`Filter ${f.titel}`}
          className={cn(
            'inline-flex h-8 max-w-full items-stretch overflow-hidden rounded-full border border-akzent text-[13px] font-medium text-akzent-text',
            f.wert ? 'bg-blau-flaeche' : 'border-dashed bg-karte',
          )}
        >
          {f.auswahl(<PillKnopf titel={f.titel} icon={f.icon} wert={f.wert} />, neu === f.schluessel)}
          <button
            type="button"
            aria-label={`Filter ${f.titel} entfernen`}
            title="Filter entfernen"
            onClick={() => entfernen(f)}
            className="inline-flex w-7 flex-none cursor-pointer items-center justify-center hover:bg-akzent-zart"
          >
            <Icon name="close" className="text-[16px]" />
          </button>
        </span>
      ))}
      {verfuegbar.length > 0 && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className="inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-full border border-dashed border-akzent bg-karte pr-3 pl-2.5 text-[13px] font-medium text-akzent-text hover:bg-akzent-zart data-[state=open]:bg-akzent-zart"
            >
              <Icon name="add" className="text-[18px]" />
              Filter hinzufügen
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            align="start"
            // Die neue Pill öffnet ihre Auswahl selbst – der Fokus soll nicht zurück auf „Filter hinzufügen“.
            onCloseAutoFocus={(e) => {
              if (gewaehlt.current) e.preventDefault()
              gewaehlt.current = false
            }}
          >
            {verfuegbar.map((f) => (
              <DropdownMenuItem key={f.schluessel} onSelect={() => hinzufuegen(f.schluessel)}>
                <Icon name={f.icon} />
                {f.titel}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
      {(sichtbar.length > 0 || weitereAktiv) && (
        <button
          type="button"
          onClick={() => {
            onZuruecksetzen()
            setHinzugefuegt([])
            setNeu(null)
          }}
          className="inline-flex h-8 cursor-pointer items-center gap-1 rounded-[7px] border-0 bg-transparent px-2 text-[13px] font-medium text-gedaempft hover:bg-hover hover:text-text"
        >
          <Icon name="close" className="text-[16px]" />
          Filter zurücksetzen
        </button>
      )}
    </div>
  )
}
