import { useId, useState, type ReactNode } from 'react'
import { useKorrespondenten } from '@/api/korrespondenten'
import type { KorrespondentListeneintrag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Option, Suchfeld, useEntprellt, useListenNavigation } from './gemeinsam'

/** Name ohne Umlaute, Rechtsform-Abstände und Satzzeichen – „Bürowelt Krämer KG“ → „bueroweltkraemerkg“. */
function vergleichsname(name: string): string {
  return name
    .toLocaleLowerCase('de')
    .replace(/ä/g, 'ae')
    .replace(/ö/g, 'oe')
    .replace(/ü/g, 'ue')
    .replace(/ß/g, 'ss')
    .replace(/[^a-z0-9]/g, '')
}

/** Ohne USt-IdNr. und mit ähnlichem Namen wie ein anderer Eintrag → „mögliche Dublette“. */
export function istMoeglicheDublette(k: KorrespondentListeneintrag, alle: KorrespondentListeneintrag[]): boolean {
  if (k.ustIdNr) return false
  const eigener = vergleichsname(k.name)
  return alle.some((anderer) => {
    if (anderer.id === k.id) return false
    const fremder = vergleichsname(anderer.name)
    return eigener.length > 0 && (fremder.startsWith(eigener) || eigener.startsWith(fremder))
  })
}

function Logo({ korrespondent }: { korrespondent: KorrespondentListeneintrag }) {
  return <KorrespondentLogo logo={korrespondent.logo} groesse={24} />
}

/** Korrespondent-Auswahl (Beleg-Detail, Artikel-Filter): Suche am Server, entprellt. */
export function KorrespondentAuswahl({
  wert,
  onAendern,
  ausloeser,
  onNeuAusBelegdaten,
  anfangsOffen = false,
}: {
  wert: string | null
  onAendern: (id: string) => void
  ausloeser: ReactNode
  onNeuAusBelegdaten?: () => void
  /** Beim ersten Rendern geöffnet (neu hinzugefügte Filter-Pill). */
  anfangsOffen?: boolean
}) {
  const listId = useId()
  const [offen, setOffen] = useState(anfangsOffen)
  const [suche, setSuche] = useState('')
  const entprellt = useEntprellt(suche.trim(), 250)
  const { data: treffer = [] } = useKorrespondenten(entprellt ? { suche: entprellt } : {})

  function waehlen(id: string) {
    onAendern(id)
    setOffen(false)
  }

  function neuAnlegen() {
    setOffen(false)
    onNeuAusBelegdaten?.()
  }

  const anzahl = treffer.length + (onNeuAusBelegdaten ? 1 : 0)
  const navigation = useListenNavigation(anzahl, (i) => (i < treffer.length ? waehlen(treffer[i].id) : neuAnlegen()))

  return (
    <Popover open={offen} onOpenChange={setOffen}>
      <PopoverContent className="w-80 p-3">
        <Suchfeld
          wert={suche}
          onAendern={setSuche}
          platzhalter="Korrespondent suchen"
          onKeyDown={navigation.onKeyDown}
          listId={listId}
          aktivId={navigation.aktiv >= 0 ? `${listId}-${navigation.aktiv}` : undefined}
        />
        <div id={listId} role="listbox" aria-label="Korrespondenten">
          {treffer.map((k, i) => (
            <Option
              key={k.id}
              id={`${listId}-${i}`}
              gewaehlt={k.id === wert}
              aktiv={navigation.aktiv === i}
              onWaehlen={() => waehlen(k.id)}
            >
              <Logo korrespondent={k} />
              <span className="min-w-0 flex-1 leading-tight">
                <span className="block truncate">{k.name}</span>
                {k.ustIdNr && <span className="mono block text-xs text-gedaempft">{k.ustIdNr}</span>}
              </span>
              {istMoeglicheDublette(k, treffer) && <Chip ton="orange">mögliche Dublette</Chip>}
              {k.id === wert && <Icon name="check" className="text-akzent-text" label="gewählt" />}
            </Option>
          ))}
          {onNeuAusBelegdaten && (
            <Option
              id={`${listId}-${treffer.length}`}
              gewaehlt={false}
              aktiv={navigation.aktiv === treffer.length}
              onWaehlen={neuAnlegen}
            >
              <Icon name="add_business" />
              Neu aus Belegdaten anlegen
            </Option>
          )}
        </div>
      </PopoverContent>
      <PopoverTrigger asChild>{ausloeser}</PopoverTrigger>
    </Popover>
  )
}
