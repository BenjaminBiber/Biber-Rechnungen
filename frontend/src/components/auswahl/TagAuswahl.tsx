import { useId, useState, type KeyboardEvent, type ReactNode } from 'react'
import { useTagAnlegen, useTags } from '@/api/tags'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { SkelettListe } from '@/components/ui/skelett'
import { Option, Suchfeld, useListenNavigation } from './gemeinsam'

/** Tag-Auswahl (Beleg-Detail, Massenaktion, Filter): Mehrfachauswahl mit Suche und „neu anlegen“. */
export function TagAuswahl({
  gewaehlt,
  onAendern,
  ausloeser,
  nurZahlungsTags = false,
  neuAnlegenErlaubt = false,
  anfangsOffen = false,
  systemTagsWaehlbar = false,
}: {
  gewaehlt: string[]
  onAendern: (ids: string[]) => void
  ausloeser: ReactNode
  nurZahlungsTags?: boolean
  neuAnlegenErlaubt?: boolean
  /** Beim ersten Anzeigen gleich geöffnet (z. B. frisch hinzugefügte Filter-Pill). */
  anfangsOffen?: boolean
  /** System-Tags (z. B. „zu prüfen“) vergibt die Verarbeitung; wählbar sind sie nur zum Filtern. */
  systemTagsWaehlbar?: boolean
}) {
  const listId = useId()
  const [suche, setSuche] = useState('')
  const { data: tags = [], isPending } = useTags()
  const anlegen = useTagAnlegen()
  const toast = useToast()

  const begriff = suche.trim()
  const treffer = tags.filter(
    (t) =>
      (!nurZahlungsTags || t.istZahlungsTag) &&
      t.name.toLocaleLowerCase('de').includes(begriff.toLocaleLowerCase('de')),
  )
  const neuMoeglich =
    neuAnlegenErlaubt &&
    begriff !== '' &&
    !tags.some((t) => t.name.toLocaleLowerCase('de') === begriff.toLocaleLowerCase('de'))

  function umschalten(id: string) {
    onAendern(gewaehlt.includes(id) ? gewaehlt.filter((g) => g !== id) : [...gewaehlt, id])
  }

  async function neuAnlegen() {
    if (anlegen.isPending) return
    try {
      const tag = await anlegen.mutateAsync({
        name: begriff,
        farbe: 'grau',
        istInbox: false,
        istZahlungsTag: nurZahlungsTags,
        matching: { algorithmus: 'Keiner', muster: null, grossKlein: false },
      })
      setSuche('')
      onAendern([...gewaehlt, tag.id])
    } catch {
      toast.fehler(`Tag „${begriff}“ konnte nicht angelegt werden`)
    }
  }

  const anzahl = treffer.length + (neuMoeglich ? 1 : 0)
  const navigation = useListenNavigation(
    anzahl,
    (i) => (i < treffer.length ? umschalten(treffer[i].id) : void neuAnlegen()),
    (i) => i >= treffer.length || systemTagsWaehlbar || !treffer[i].istSystem,
  )

  // Enter ohne markierten Eintrag legt den eingetippten Namen direkt als neuen Tag an.
  function onKeyDown(e: KeyboardEvent) {
    if (e.key === 'Enter' && navigation.aktiv < 0 && neuMoeglich) {
      e.preventDefault()
      void neuAnlegen()
      return
    }
    navigation.onKeyDown(e)
  }

  return (
    <Popover defaultOpen={anfangsOffen}>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
      <PopoverContent className="p-3">
        <Suchfeld
          wert={suche}
          onAendern={setSuche}
          platzhalter="Tag suchen"
          ladend={anlegen.isPending}
          onKeyDown={onKeyDown}
          listId={listId}
          aktivId={navigation.aktiv >= 0 ? `${listId}-${navigation.aktiv}` : undefined}
        />
        {isPending && <SkelettListe zeilen={4} symbol={false} kompakt label="Tags werden geladen" />}
        <div
          id={listId}
          role="listbox"
          aria-label="Tags"
          aria-multiselectable="true"
          aria-busy={isPending || undefined}
        >
          {treffer.map((tag, i) => {
            const an = gewaehlt.includes(tag.id)
            return (
              <Option
                key={tag.id}
                id={`${listId}-${i}`}
                gewaehlt={an}
                aktiv={navigation.aktiv === i}
                deaktiviert={tag.istSystem && !systemTagsWaehlbar}
                hinweis={tag.istSystem && !systemTagsWaehlbar ? 'System-Tag – wird automatisch vergeben' : undefined}
                onWaehlen={() => umschalten(tag.id)}
              >
                <Icon name={an ? 'check_box' : 'check_box_outline_blank'} />
                <Chip ton={tag.farbe as ChipTon}>{tag.name}</Chip>
                {(tag.istZahlungsTag || tag.istSystem) && (
                  <span className="ml-auto text-xs text-gedaempft">{tag.istSystem ? 'System' : 'Zahlung'}</span>
                )}
              </Option>
            )
          })}
          {neuMoeglich && (
            <Option
              id={`${listId}-${treffer.length}`}
              gewaehlt={false}
              aktiv={navigation.aktiv === treffer.length}
              onWaehlen={() => void neuAnlegen()}
            >
              <Icon name="add" />
              {`„${begriff}“ als neuen Tag anlegen`}
            </Option>
          )}
        </div>
      </PopoverContent>
    </Popover>
  )
}
