import { useNavigate } from '@tanstack/react-router'
import { useEffect, useMemo, useState } from 'react'
import { useSuche } from '@/api/suche'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import { befehle, befehleFiltern, type Befehl } from '@/features/befehlspalette/befehle'
import { useBefehlAusfuehren } from '@/features/befehlspalette/useBefehlAusfuehren'
import { useGlobaleKuerzel } from '@/features/befehlspalette/useGlobaleKuerzel'
import { formatEuro } from '@/lib/format'
import { useBefehlspalette } from './befehlspalette-store'
import { useRolle } from './useRolle'

/** Ab so vielen Zeichen wird gesucht. */
const MIN_ZEICHEN = 2
const ENTPRELLUNG_MS = 200

function useEntprellt(wert: string, ms: number): string {
  const [entprellt, setEntprellt] = useState(wert)
  useEffect(() => {
    const t = setTimeout(() => setEntprellt(wert), ms)
    return () => clearTimeout(t)
  }, [wert, ms])
  return entprellt
}

function Kbd({ children }: { children: string }) {
  return (
    <kbd className="ml-auto flex-none rounded border border-eingabe-rand bg-karte px-[5px] py-0.5 font-mono text-[11px] font-medium text-gedaempft-stark">
      {children}
    </kbd>
  )
}

/** Befehlspalette (Strg K, Menues.dc.html): Volltextsuche über Belege, Korrespondenten, Artikel plus Befehle. */
export function Befehlspalette() {
  const { offen, oeffnen, schliessen } = useBefehlspalette()
  const { rolle } = useRolle()
  const navigate = useNavigate()
  const ausfuehren = useBefehlAusfuehren()
  const [eingabe, setEingabe] = useState('')
  const alleBefehle = useMemo(() => befehle({ rolle, heute: new Date() }), [rolle])

  useGlobaleKuerzel({ umschalten: () => (offen ? schliessen() : oeffnen()), befehle: alleBefehle, ausfuehren })

  const entprellt = useEntprellt(eingabe.trim(), ENTPRELLUNG_MS)
  const suchen = eingabe.trim().length >= MIN_ZEICHEN && entprellt.length >= MIN_ZEICHEN
  const { data } = useSuche(suchen ? entprellt : '')
  const treffer = suchen ? data : undefined
  const keineTreffer =
    !!treffer &&
    treffer.belege.length + treffer.korrespondenten.length + treffer.artikel.length + treffer.stammartikel.length === 0
  const sichtbareBefehle = befehleFiltern(alleBefehle, eingabe)

  const beenden = () => {
    schliessen()
    setEingabe('')
  }
  const oeffneRoute: typeof navigate = (ziel) => {
    beenden()
    return navigate(ziel)
  }
  const befehlWaehlen = (befehl: Befehl) => {
    beenden()
    ausfuehren(befehl)
  }

  return (
    <Dialog open={offen} onOpenChange={(o) => (o ? oeffnen() : beenden())}>
      <DialogContent
        ohneSchliessen
        aria-label="Befehlspalette"
        className="top-[15vh] max-w-[720px] translate-y-0 gap-0 overflow-hidden p-0"
      >
        <DialogTitle className="sr-only">Befehlspalette</DialogTitle>
        <DialogDescription className="sr-only">Suchen oder Befehl ausführen</DialogDescription>
        <Command shouldFilter={false} label="Befehlspalette">
          <div className="flex items-center pr-4 [&>div]:flex-1 [&>div]:border-0">
            <CommandInput
              value={eingabe}
              onValueChange={setEingabe}
              placeholder="Belege, Korrespondenten, Artikel durchsuchen …"
              aria-label="Suchen oder Befehl ausführen"
            />
            <Kbd>Esc</Kbd>
          </div>
          <CommandList className="border-t border-linie">
            {keineTreffer && (
              <>
                <p className="px-2.5 pt-2 pb-1 text-sm text-gedaempft">Nichts gefunden für ‚{entprellt}‘</p>
                <CommandItem
                  value="volltext"
                  onSelect={() => oeffneRoute({ to: '/belege', search: { suche: entprellt } })}
                >
                  <Icon name="search" />
                  In Belegen suchen: ‚{entprellt}‘
                </CommandItem>
              </>
            )}
            {treffer && treffer.belege.length > 0 && (
              <CommandGroup heading="Belege">
                {treffer.belege.map((b) => (
                  <CommandItem
                    key={b.id}
                    value={`beleg-${b.id}`}
                    onSelect={() => oeffneRoute({ to: '/belege/$belegId', params: { belegId: b.id } })}
                  >
                    <Icon name="receipt_long" />
                    <span className="min-w-0 flex-1 truncate">
                      {b.korrespondent?.name ?? b.titel}
                      {b.rechnungsnummer && (
                        <>
                          {' · '}
                          <span className="font-mono text-[13px]">{b.rechnungsnummer}</span>
                        </>
                      )}
                      {b.auszug && ` · „${b.auszug}“`}
                    </span>
                    {b.brutto != null && <Kbd>{formatEuro(b.brutto, b.waehrung)}</Kbd>}
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
            {treffer && treffer.korrespondenten.length > 0 && (
              <CommandGroup heading="Korrespondenten">
                {treffer.korrespondenten.map((k) => (
                  <CommandItem
                    key={k.id}
                    value={`korrespondent-${k.id}`}
                    onSelect={() =>
                      oeffneRoute({ to: '/korrespondenten/$korrespondentId', params: { korrespondentId: k.id } })
                    }
                  >
                    <KorrespondentLogo logo={k.logo} groesse={20} ton="akzent" />
                    {k.name}
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
            {treffer && treffer.artikel.length + treffer.stammartikel.length > 0 && (
              <CommandGroup heading="Artikel">
                {treffer.stammartikel.map((s) => (
                  <CommandItem
                    key={s.id}
                    value={`stammartikel-${s.id}`}
                    onSelect={() =>
                      oeffneRoute({ to: '/artikel/stammartikel/$stammartikelId', params: { stammartikelId: s.id } })
                    }
                  >
                    <Icon name="inventory_2" />
                    {s.name}
                    <Chip ton="grau">{`Stammartikel · ${s.korrespondenten.length} Händler`}</Chip>
                  </CommandItem>
                ))}
                {treffer.artikel.map((a) => (
                  <CommandItem
                    key={a.id}
                    value={`artikel-${a.id}`}
                    onSelect={() => oeffneRoute({ to: '/artikel/$artikelId', params: { artikelId: a.id } })}
                  >
                    <Icon name="inventory_2" />
                    <span className="min-w-0 flex-1 truncate">
                      {a.name} <span className="text-gedaempft">· {a.korrespondent.name}</span>
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
            {sichtbareBefehle.length > 0 && (
              <CommandGroup heading="Befehle">
                {sichtbareBefehle.map((b) => (
                  <CommandItem key={b.id} value={`befehl-${b.id}`} onSelect={() => befehlWaehlen(b)}>
                    <Icon name={b.icon} />
                    {b.text}
                    {b.kuerzel && <Kbd>{b.kuerzel}</Kbd>}
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </DialogContent>
    </Dialog>
  )
}
