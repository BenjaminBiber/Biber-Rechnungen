import { Link, useNavigate } from '@tanstack/react-router'
import { useState } from 'react'
import {
  useAlleBenachrichtigungenGelesen,
  useBenachrichtigungen,
  useBenachrichtigungGelesen,
  useBenachrichtigungZaehler,
} from '@/api/benachrichtigungen'
import type { Benachrichtigung, Schweregrad } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { formatDatumZeit } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Icon und Farbe je Schweregrad (Warnung orange, Fehler rot, Ok grün, Info blau). */
export const schweregradDarstellung: Record<Schweregrad, { icon: string; klasse: string; text: string }> = {
  Warnung: { icon: 'warning', klasse: 'bg-orange-flaeche text-orange-text', text: 'Warnung' },
  Fehler: { icon: 'error', klasse: 'bg-rot-flaeche text-rot-text', text: 'Fehler' },
  Ok: { icon: 'check_circle', klasse: 'bg-gruen-flaeche text-gruen-text', text: 'In Ordnung' },
  Info: { icon: 'info', klasse: 'bg-blau-flaeche text-blau-text', text: 'Hinweis' },
}

/** Eine Benachrichtigung als Zeile; ungelesene sind hinterlegt. Für Glocke und Benachrichtigungsseite. */
export function BenachrichtigungZeile({
  eintrag,
  beiKlick,
}: {
  eintrag: Benachrichtigung
  beiKlick: (eintrag: Benachrichtigung) => void
}) {
  const d = schweregradDarstellung[eintrag.schweregrad]
  return (
    <button
      type="button"
      onClick={() => beiKlick(eintrag)}
      className={cn(
        'flex w-full cursor-pointer gap-3 border-0 border-b border-linie px-4 py-3 text-left text-text',
        eintrag.gelesen ? 'bg-karte' : 'bg-[#f7f7fc] dark:bg-akzent-zart',
      )}
    >
      <span className={cn('flex h-8 w-8 flex-none items-center justify-center rounded-full', d.klasse)}>
        <Icon name={d.icon} className="text-[18px]" label={d.text} />
      </span>
      <span className="flex flex-col gap-0.5 text-[13px] leading-[1.4]">
        <span className="font-semibold">
          {eintrag.titel}
          {!eintrag.gelesen && <span className="sr-only"> (ungelesen)</span>}
        </span>
        <span className="text-gedaempft-stark">{eintrag.text}</span>
        <span className="text-xs text-gedaempft">{formatDatumZeit(eintrag.erstelltAm)}</span>
      </span>
    </button>
  )
}

/** Markiert eine Benachrichtigung als gelesen und folgt ihrem Link. */
export function useBenachrichtigungOeffnen(nachher?: () => void) {
  const navigate = useNavigate()
  const gelesen = useBenachrichtigungGelesen()
  return (eintrag: Benachrichtigung) => {
    if (!eintrag.gelesen) gelesen.mutate(eintrag.id)
    nachher?.()
    if (eintrag.link) void navigate({ to: eintrag.link })
  }
}

/** Glocke in der Kopfleiste mit Zähler und Popover der neuesten fünf Benachrichtigungen. */
export function Glocke() {
  const [offen, setOffen] = useState(false)
  const { data: zaehler } = useBenachrichtigungZaehler()
  const { data: liste } = useBenachrichtigungen()
  const alleGelesen = useAlleBenachrichtigungenGelesen()
  const oeffnen = useBenachrichtigungOeffnen(() => setOffen(false))
  const ungelesen = zaehler?.ungelesen ?? 0
  const neueste = (liste?.eintraege ?? []).slice(0, 5)

  return (
    <Popover open={offen} onOpenChange={setOffen}>
      <PopoverTrigger
        aria-label={`Benachrichtigungen, ${ungelesen} ungelesen`}
        className={cn(
          'relative flex h-8 w-8 cursor-pointer items-center justify-center rounded-full border-0 text-text',
          offen ? 'bg-linie' : 'bg-transparent hover:bg-hover',
        )}
      >
        <Icon name="notifications" className="text-[20px]" />
        {ungelesen > 0 && (
          <span
            data-testid="glocke-zaehler"
            className="absolute -top-1 -right-1 flex h-4 min-w-4 items-center justify-center rounded-full border-2 border-karte bg-rot-text text-[10px] font-bold text-white"
          >
            {ungelesen}
          </span>
        )}
      </PopoverTrigger>
      <PopoverContent
        align="end"
        sideOffset={8}
        role="dialog"
        aria-label="Benachrichtigungen"
        className="w-[400px] max-w-[calc(100vw-32px)] overflow-hidden p-0"
      >
        <div className="flex items-center justify-between border-b border-linie px-4 py-3.5">
          <span className="text-[15px] font-semibold">Benachrichtigungen</span>
          <button
            type="button"
            className="cursor-pointer border-0 bg-transparent text-[13px] font-medium text-akzent-text"
            onClick={() => alleGelesen.mutate()}
          >
            Alle gelesen
          </button>
        </div>
        {neueste.length === 0 && (
          <p className="gedaempft m-0 px-4 py-6 text-center text-sm">Keine Benachrichtigungen</p>
        )}
        {neueste.map((n) => (
          <BenachrichtigungZeile key={n.id} eintrag={n} beiKlick={oeffnen} />
        ))}
        <Link
          to="/benachrichtigungen"
          onClick={() => setOffen(false)}
          className="block px-4 py-3 text-center text-[13px] font-medium text-akzent-text no-underline"
        >
          Alle Benachrichtigungen
        </Link>
      </PopoverContent>
    </Popover>
  )
}
