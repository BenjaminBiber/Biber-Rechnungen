import { useKategorien, useTags } from '@/api/tags'
import { Icon } from '@/components/Icon'
import { Seitenkopf } from '@/components/Seitenkopf'
import { useRolle } from '@/components/shell/useRolle'
import { Button } from '@/components/ui/button'
import { BearbeitenDialog } from './BearbeitenDialog'
import type { Reiter } from './gemeinsam'
import { KategorieTabelle } from './KategorieTabelle'
import { TagTabelle } from './TagTabelle'

export type TagsWahl = {
  reiter?: Reiter
  bearbeiten?: string
  neu?: boolean
}

/** Verwaltung von Tags und Kategorien mit zwei Reitern und gemeinsamem Bearbeiten-Dialog. */
export function TagsSeite({
  reiter,
  bearbeiten,
  neu,
  jahr,
  onWahl,
}: {
  reiter: Reiter
  bearbeiten?: string
  neu: boolean
  jahr: number
  onWahl: (wahl: TagsWahl) => void
}) {
  const { darfBearbeiten } = useRolle()
  const tags = useTags()
  const kategorien = useKategorien(jahr)
  const alleTags = tags.data ?? []
  const alleKategorien = kategorien.data ?? []
  const istTags = reiter === 'tags'
  const schliessen = () => onWahl({ bearbeiten: undefined, neu: undefined })
  const oeffnen = darfBearbeiten ? (id: string) => onWahl({ bearbeiten: id, neu: undefined }) : undefined

  const tag = istTags ? alleTags.find((t) => t.id === bearbeiten && !t.istSystem) : undefined
  const kategorie = istTags ? undefined : alleKategorien.find((k) => k.id === bearbeiten)
  const geladen = istTags ? !tags.isPending : !kategorien.isPending

  const reiterKnopf = (r: Reiter, text: string) => (
    <button
      type="button"
      role="tab"
      aria-selected={reiter === r}
      className={`-mb-px h-10 cursor-pointer border-0 border-b-2 bg-transparent px-3 text-sm font-medium ${
        reiter === r ? 'border-[var(--color-akzent)] text-text' : 'border-transparent text-gedaempft-stark'
      }`}
      onClick={() => onWahl({ reiter: r, bearbeiten: undefined, neu: undefined })}
    >
      {text}
    </button>
  )

  return (
    <main className="p-6">
      <Seitenkopf
        titel="Tags & Kategorien"
        untertitel="Tags sind frei und mehrfach je Beleg. Jeder Beleg hat höchstens eine Kategorie, optional mit EÜR-Zeile für den Export."
        aktionen={
          darfBearbeiten && (
            <Button variant="primaer" onClick={() => onWahl({ neu: true, bearbeiten: undefined })}>
              <Icon name="add" />
              {istTags ? 'Neuer Tag' : 'Neue Kategorie'}
            </Button>
          )
        }
      />
      <div role="tablist" aria-label="Verwaltung" className="mb-4 flex gap-1 border-b border-rand">
        {reiterKnopf('tags', `Tags (${alleTags.length})`)}
        {reiterKnopf('kategorien', `Kategorien (${alleKategorien.length})`)}
      </div>
      {/* relative: Die absolut positionierten sr-only-Spaltenköpfe bleiben im Scrollbereich und verbreitern die Seite nicht. */}
      <div className="karte relative overflow-x-auto p-0">
        {!geladen ? (
          <p className="gedaempft m-0 p-6 text-sm">Lädt …</p>
        ) : istTags ? (
          <TagTabelle tags={alleTags} gewaehlt={bearbeiten} onBearbeiten={oeffnen} />
        ) : (
          <KategorieTabelle kategorien={alleKategorien} jahr={jahr} gewaehlt={bearbeiten} onBearbeiten={oeffnen} />
        )}
      </div>
      <p className="gedaempft m-0 mt-4 flex items-center gap-1.5 text-xs">
        <Icon name="lock" className="text-base" />
        „zu prüfen“ ist ein System-Tag: Ihn setzt nur die Pipeline, entfernt wird er nur durch „Bestätigen“. Er kann
        nicht bearbeitet oder gelöscht werden.
      </p>
      {darfBearbeiten && geladen && istTags && (neu || tag) && (
        <BearbeitenDialog
          key={tag?.id ?? 'neu'}
          art="tag"
          tag={neu ? undefined : tag}
          alleTags={alleTags}
          onSchliessen={schliessen}
        />
      )}
      {darfBearbeiten && geladen && !istTags && (neu || kategorie) && (
        <BearbeitenDialog
          key={kategorie?.id ?? 'neu'}
          art="kategorie"
          kategorie={neu ? undefined : kategorie}
          onSchliessen={schliessen}
        />
      )}
    </main>
  )
}
