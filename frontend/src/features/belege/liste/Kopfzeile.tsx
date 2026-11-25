import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { belegeKeys, useAnsichten, useAnsichtLoeschen, useAnsichtSpeichern, useBelegZaehler } from '@/api/belege'
import { api, daten } from '@/api/client'
import type { BelegAnsicht, BelegeAbfrage, BelegSortierung, BelegZaehler, GespeicherteAnsicht } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { useBelegAuswahl } from './auswahl'
import { gruppe, MenueKnopf } from './bausteine'
import { Spalten } from './Spalten'
import type { SpaltenSchluessel } from './spaltenDefinitionen'
import {
  ansichten,
  ansichtTitel,
  ausFilter,
  sortierungen,
  sortierungText,
  zuFilter,
  type Darstellung,
  type ListenZustand,
} from './suche'

const darstellungKnoepfe: { wert: Darstellung; icon: string; label: string }[] = [
  { wert: 'tabelle', icon: 'table_rows', label: 'Tabelle' },
  { wert: 'klein', icon: 'grid_view', label: 'Kleine Karten' },
  { wert: 'gross', icon: 'view_agenda', label: 'Große Karten' },
]

const ansichtIcon: Record<BelegAnsicht, string> = {
  Alle: 'receipt_long',
  Inbox: 'inbox',
  ZuPruefen: 'report',
  Offen: 'schedule',
  Ueberfaellig: 'event_busy',
}

const zaehlerFeld: Record<BelegAnsicht, keyof BelegZaehler> = {
  Alle: 'alle',
  Inbox: 'inbox',
  ZuPruefen: 'zuPruefen',
  Offen: 'offen',
  Ueberfaellig: 'ueberfaellig',
}

/**
 * Kopfzeile der Belegliste: Titel der Ansicht, „Auswählen: Seite | Alle“, Spaltenwahl, Darstellung,
 * Sortierung und das Menü „Ansicht“ (Standardansichten, gespeicherte Ansichten, speichern, löschen).
 */
export function Kopfzeile({
  zustand,
  abfrage,
  seitenIds,
  gesamt,
  spalten,
  onSpaltenAendern,
}: {
  zustand: ListenZustand
  abfrage: BelegeAbfrage
  seitenIds: string[]
  gesamt: number
  spalten: SpaltenSchluessel[]
  onSpaltenAendern: (spalten: SpaltenSchluessel[]) => void
}) {
  const { suche, setze } = zustand
  const auswahl = useBelegAuswahl()
  const queryClient = useQueryClient()
  const darstellung = suche.darstellung ?? 'tabelle'

  async function alleWaehlen() {
    if (gesamt === 0) return
    const alleAbfrage = { ...abfrage, seite: 1, seitengroesse: gesamt }
    const liste = await queryClient.fetchQuery({
      queryKey: belegeKeys.liste(alleAbfrage),
      queryFn: ({ signal }) => daten(api.GET('/api/belege', { params: { query: alleAbfrage }, signal })),
    })
    auswahl.alleSetzen(liste.eintraege.map((b) => b.id))
  }

  return (
    <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
      <h1 className="m-0 text-[26px] font-medium text-text">{ansichtTitel[suche.ansicht ?? 'Alle']}</h1>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-gedaempft-stark">Auswählen:</span>
        <div className={gruppe} role="group" aria-label="Auswählen">
          <button
            type="button"
            onClick={() => auswahl.alleSetzen(seitenIds)}
            aria-label="Alle Belege dieser Seite auswählen"
          >
            <Icon name="description" className="text-[18px]" />
            Seite
          </button>
          <button type="button" onClick={() => void alleWaehlen()} aria-label="Alle Belege der Liste auswählen">
            <Icon name="done_all" className="text-[18px]" />
            Alle
          </button>
        </div>
        <Spalten spalten={spalten} onAendern={onSpaltenAendern} />
        <div className={gruppe} role="radiogroup" aria-label="Darstellung">
          {darstellungKnoepfe.map((k) => {
            const an = darstellung === k.wert
            return (
              <button
                key={k.wert}
                type="button"
                role="radio"
                aria-checked={an}
                aria-label={k.label}
                title={k.label}
                onClick={() => setze({ darstellung: k.wert === 'tabelle' ? undefined : k.wert, seite: suche.seite })}
                className={cn('!w-[38px] !px-0', an && '!bg-akzent !text-auf-akzent')}
              >
                <Icon name={k.icon} className="[vertical-align:0]" />
              </button>
            )
          })}
        </div>
        <SortierMenue zustand={zustand} />
        <AnsichtMenue zustand={zustand} spalten={spalten} onSpaltenAendern={onSpaltenAendern} />
      </div>
    </div>
  )
}

function SortierMenue({ zustand: { suche, setze } }: { zustand: ListenZustand }) {
  const sortierung = suche.sortierung ?? 'Rechnungsdatum'
  const absteigend = suche.absteigend ?? true
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <MenueKnopf
          icon="swap_vert"
          label={sortierungText[sortierung]}
          aria-label={`Sortierung: ${sortierungText[sortierung]}, ${absteigend ? 'absteigend' : 'aufsteigend'}`}
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>Sortieren nach</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={sortierung}
          onValueChange={(wert) =>
            setze({ sortierung: wert === 'Rechnungsdatum' ? undefined : (wert as BelegSortierung) })
          }
        >
          {sortierungen.map((s) => (
            <DropdownMenuRadioItem key={s} value={s}>
              {sortierungText[s]}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuRadioGroup
          value={absteigend ? 'ab' : 'auf'}
          onValueChange={(wert) => setze({ absteigend: wert === 'ab' ? undefined : false })}
        >
          <DropdownMenuRadioItem value="auf">Aufsteigend</DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="ab">Absteigend</DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function AnsichtMenue({
  zustand,
  spalten,
  onSpaltenAendern,
}: {
  zustand: ListenZustand
  spalten: SpaltenSchluessel[]
  onSpaltenAendern: (spalten: SpaltenSchluessel[]) => void
}) {
  const { suche, setze, ersetze } = zustand
  const { data: zaehler } = useBelegZaehler()
  const { data: gespeichert = [] } = useAnsichten()
  const loeschen = useAnsichtLoeschen()
  const toast = useToast()
  const [speichernOffen, setSpeichernOffen] = useState(false)
  const aktiv = suche.ansicht ?? 'Alle'

  function anwenden(ansicht: GespeicherteAnsicht) {
    ersetze(ausFilter(ansicht.filter, ansicht.darstellung))
    if (ansicht.spalten.length) onSpaltenAendern(ansicht.spalten as SpaltenSchluessel[])
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <MenueKnopf icon="bookmark" label="Ansicht" />
        </DropdownMenuTrigger>
        <DropdownMenuContent className="min-w-[260px]">
          <DropdownMenuRadioGroup
            value={aktiv}
            onValueChange={(wert) => setze({ ansicht: wert === 'Alle' ? undefined : (wert as BelegAnsicht) })}
          >
            {ansichten.map((a) => (
              <DropdownMenuRadioItem key={a} value={a}>
                <Icon name={ansichtIcon[a]} />
                <span className="flex-1">{ansichtTitel[a]}</span>
                {zaehler && <span className="text-xs text-gedaempft tabular-nums">{zaehler[zaehlerFeld[a]]}</span>}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
          {gespeichert.length > 0 && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuLabel>Gespeicherte Ansichten</DropdownMenuLabel>
              {gespeichert.map((a) => (
                <div key={a.id} className="flex items-center">
                  <DropdownMenuItem className="flex-1" onSelect={() => anwenden(a)}>
                    <Icon name="bookmark" />
                    {a.name}
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    aria-label={`Ansicht „${a.name}“ löschen`}
                    onSelect={() =>
                      loeschen.mutate(a.id, {
                        onSuccess: () => toast.erfolg(`Ansicht „${a.name}“ gelöscht`),
                      })
                    }
                  >
                    <Icon name="delete" />
                  </DropdownMenuItem>
                </div>
              ))}
            </>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => setSpeichernOffen(true)}>
            <Icon name="bookmark_add" />
            Aktuelle Ansicht speichern …
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <AnsichtSpeichernDialog
        offen={speichernOffen}
        onOffenAendern={setSpeichernOffen}
        zustand={zustand}
        spalten={spalten}
      />
    </>
  )
}

function AnsichtSpeichernDialog({
  offen,
  onOffenAendern,
  zustand,
  spalten,
}: {
  offen: boolean
  onOffenAendern: (offen: boolean) => void
  zustand: ListenZustand
  spalten: SpaltenSchluessel[]
}) {
  const [name, setName] = useState('')
  const speichern = useAnsichtSpeichern()
  const toast = useToast()

  function absenden(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    speichern.mutate(
      {
        name: name.trim(),
        filter: zuFilter(zustand.suche),
        spalten,
        darstellung: zustand.suche.darstellung ?? 'tabelle',
      },
      {
        onSuccess: (ansicht) => {
          toast.erfolg(`Ansicht „${ansicht.name}“ gespeichert`)
          setName('')
          onOffenAendern(false)
        },
      },
    )
  }

  return (
    <Dialog open={offen} onOpenChange={onOffenAendern}>
      <DialogContent>
        <form onSubmit={absenden} className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Ansicht speichern</DialogTitle>
          </DialogHeader>
          <div>
            <label className="label" htmlFor="ansicht-name">
              Name
            </label>
            <input
              id="ansicht-name"
              className="eingabe"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="z. B. Offen 2026"
            />
          </div>
          {speichern.isError && (
            <p className="m-0 text-sm text-rot-text">Die Ansicht konnte nicht gespeichert werden.</p>
          )}
          <DialogFooter>
            <button type="button" className="btn" onClick={() => onOffenAendern(false)}>
              Abbrechen
            </button>
            <button
              type="submit"
              className="btn btn-primaer"
              disabled={!name.trim() || speichern.isPending}
              aria-busy={speichern.isPending}
            >
              Speichern
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
