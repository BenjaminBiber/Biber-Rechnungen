import * as DialogPrimitive from '@radix-ui/react-dialog'
import { useState } from 'react'
import { useBelege, useBelegZaehler } from '@/api/belege'
import type { BelegAnsicht, BelegZaehler } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { ansichten, ansichtTitel, zuAbfrage } from '@/features/belege/liste/suche'
import { cn } from '@/lib/utils'

/** Die beiden Sortierungen des Sheets; „Rechnungsdatum“ ist die Vorgabe. */
export type MobilSortierung = 'Rechnungsdatum' | 'Brutto'

export type FilterAuswahl = { ansicht: BelegAnsicht; sortierung: MobilSortierung }

const zaehlerFeld: Record<BelegAnsicht, keyof BelegZaehler> = {
  Alle: 'alle',
  Inbox: 'inbox',
  ZuPruefen: 'zuPruefen',
  Offen: 'offen',
  Ueberfaellig: 'ueberfaellig',
}

const sortierungTexte: Record<MobilSortierung, string> = {
  Rechnungsdatum: 'Rechnungsdatum, neueste zuerst',
  Brutto: 'Betrag, höchster zuerst',
}

const option =
  'flex min-h-12 w-full cursor-pointer items-center gap-3 border-0 border-b border-linie bg-transparent px-1 text-left text-[15px] text-text'

/**
 * Filter-Sheet der mobilen Belegliste. Die Auswahl gilt erst mit „n Belege anzeigen“;
 * Schließen (Knopf, Hintergrund, Esc) verwirft sie.
 */
export function FilterSheet({
  offen,
  aktuell,
  suche,
  onUebernehmen,
  onSchliessen,
}: {
  offen: boolean
  aktuell: FilterAuswahl
  suche?: string
  onUebernehmen: (auswahl: FilterAuswahl) => void
  onSchliessen: () => void
}) {
  return (
    <DialogPrimitive.Root open={offen} onOpenChange={(o) => !o && onSchliessen()}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-[rgba(21,25,34,0.45)]" />
        {offen && <SheetInhalt aktuell={aktuell} suche={suche} onUebernehmen={onUebernehmen} />}
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  )
}

function SheetInhalt({
  aktuell,
  suche,
  onUebernehmen,
}: {
  aktuell: FilterAuswahl
  suche?: string
  onUebernehmen: (auswahl: FilterAuswahl) => void
}) {
  const [auswahl, setAuswahl] = useState<FilterAuswahl>(aktuell)
  const { data: zaehler } = useBelegZaehler()
  const { data: vorschau } = useBelege({
    ...zuAbfrage({
      ansicht: auswahl.ansicht === 'Alle' ? undefined : auswahl.ansicht,
      suche,
    }),
    seitengroesse: 1,
  })
  const anzahl = vorschau?.gesamt

  return (
    <DialogPrimitive.Content
      aria-describedby={undefined}
      className="fixed inset-x-0 bottom-0 z-50 flex max-h-[90vh] flex-col rounded-t-[18px] bg-karte text-text shadow-dialog"
    >
      <span aria-hidden="true" className="mx-auto mt-2 h-1 w-10 rounded-full bg-rand" />
      <div className="flex items-center gap-2 px-4 pt-2">
        <DialogPrimitive.Title className="m-0 flex-1 text-[17px] font-semibold">Filtern</DialogPrimitive.Title>
        <button
          type="button"
          className="min-h-11 cursor-pointer border-0 bg-transparent px-2 text-sm font-medium text-akzent-text"
          onClick={() => setAuswahl({ ansicht: 'Alle', sortierung: 'Rechnungsdatum' })}
        >
          Zurücksetzen
        </button>
        <DialogPrimitive.Close
          aria-label="Schließen"
          className="flex size-11 cursor-pointer items-center justify-center rounded-full border-0 bg-transparent text-gedaempft-stark"
        >
          <Icon name="close" />
        </DialogPrimitive.Close>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-2">
        <h3
          id="mobil-filter-status"
          className="gedaempft m-0 px-1 pt-4 pb-1 text-xs font-semibold tracking-[0.06em] uppercase"
        >
          Status
        </h3>
        <div role="radiogroup" aria-labelledby="mobil-filter-status">
          {ansichten.map((a) => (
            <Radio
              key={a}
              an={auswahl.ansicht === a}
              text={ansichtTitel[a]}
              zahl={zaehler?.[zaehlerFeld[a]]}
              onWahl={() => setAuswahl({ ...auswahl, ansicht: a })}
            />
          ))}
        </div>
        <h3
          id="mobil-filter-sortierung"
          className="gedaempft m-0 px-1 pt-4 pb-1 text-xs font-semibold tracking-[0.06em] uppercase"
        >
          Sortieren nach
        </h3>
        <div role="radiogroup" aria-labelledby="mobil-filter-sortierung">
          {(Object.keys(sortierungTexte) as MobilSortierung[]).map((s) => (
            <Radio
              key={s}
              an={auswahl.sortierung === s}
              text={sortierungTexte[s]}
              onWahl={() => setAuswahl({ ...auswahl, sortierung: s })}
            />
          ))}
        </div>
      </div>
      <div className="border-t border-linie p-4">
        <button
          type="button"
          className="btn btn-primaer h-12 w-full justify-center"
          onClick={() => onUebernehmen(auswahl)}
        >
          {anzahl === undefined ? 'Belege anzeigen' : anzahl === 1 ? '1 Beleg anzeigen' : `${anzahl} Belege anzeigen`}
        </button>
      </div>
    </DialogPrimitive.Content>
  )
}

function Radio({ an, text, zahl, onWahl }: { an: boolean; text: string; zahl?: number; onWahl: () => void }) {
  return (
    <button type="button" role="radio" aria-checked={an} className={option} onClick={onWahl}>
      <span className="flex-1">{text}</span>
      {zahl !== undefined && <span className="num gedaempft text-sm">{zahl}</span>}
      <span className={cn(an ? 'text-akzent-text' : 'text-gedaempft')}>
        <Icon name={an ? 'radio_button_checked' : 'radio_button_unchecked'} />
      </span>
    </button>
  )
}
