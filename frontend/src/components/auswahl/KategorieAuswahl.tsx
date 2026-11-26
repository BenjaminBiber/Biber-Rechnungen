import { useId, useState, type ReactNode } from 'react'
import { useKategorien } from '@/api/tags'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { SkelettListe } from '@/components/ui/skelett'
import { kategorieBaum } from '@/lib/kategorien'
import { Option, Suchfeld, useListenNavigation } from './gemeinsam'

/**
 * „Kategorie setzen“ (Massenaktion, Beleg-Detail): Einfachauswahl; erneutes Wählen leert.
 * Unterkategorien stehen eingerückt unter ihrer Hauptkategorie; die Suche findet sie auch über deren Namen.
 */
export function KategorieAuswahl({
  wert,
  onAendern,
  ausloeser,
  leerText = 'Leer lassen = Default des Korrespondenten',
}: {
  wert: string | null
  onAendern: (id: string | null) => void
  ausloeser: ReactNode
  leerText?: string
}) {
  const listId = useId()
  const [offen, setOffen] = useState(false)
  const [suche, setSuche] = useState('')
  const { data } = useKategorien()
  const kategorien = data ?? []
  const laedt = data === undefined

  const begriff = suche.trim().toLocaleLowerCase('de')
  const passt = (name?: string) => name !== undefined && name.toLocaleLowerCase('de').includes(begriff)
  const treffer = kategorieBaum(kategorien).filter(
    ({ kategorie }) => passt(kategorie.name) || passt(kategorien.find((e) => e.id === kategorie.elternId)?.name),
  )

  function waehlen(id: string) {
    onAendern(id === wert ? null : id)
    setOffen(false)
  }

  const navigation = useListenNavigation(treffer.length, (i) => waehlen(treffer[i].kategorie.id))

  return (
    <Popover open={offen} onOpenChange={setOffen}>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
      <PopoverContent className="p-3">
        <Suchfeld
          wert={suche}
          onAendern={setSuche}
          platzhalter="Kategorie suchen"
          onKeyDown={navigation.onKeyDown}
          listId={listId}
          aktivId={navigation.aktiv >= 0 ? `${listId}-${navigation.aktiv}` : undefined}
        />
        {laedt && <SkelettListe zeilen={4} kompakt label="Kategorien werden geladen" />}
        <div id={listId} role="listbox" aria-label="Kategorien" aria-busy={laedt || undefined}>
          {treffer.map(({ kategorie, ebene }, i) => (
            <Option
              key={kategorie.id}
              id={`${listId}-${i}`}
              gewaehlt={kategorie.id === wert}
              aktiv={navigation.aktiv === i}
              onWaehlen={() => waehlen(kategorie.id)}
            >
              {ebene > 0 && <span aria-hidden className="w-3 shrink-0" />}
              <span aria-hidden className="size-2.5 rounded-full" style={{ background: kategorie.farbe }} />
              {kategorie.name}
              {kategorie.id === wert && <Icon name="check" className="ml-auto text-akzent-text" label="gewählt" />}
            </Option>
          ))}
        </div>
        <p className="hinweis mt-2 border-t border-linie px-2.5 pt-2">{leerText}</p>
      </PopoverContent>
    </Popover>
  )
}
