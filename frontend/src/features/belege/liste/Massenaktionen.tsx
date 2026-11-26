import { useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearch } from '@tanstack/react-router'
import { useState } from 'react'
import { belegeKeys, useBelege, useMassenaktion } from '@/api/belege'
import { api, daten } from '@/api/client'
import { useTags } from '@/api/tags'
import type { BelegListeneintrag, Massenaktion } from '@/api/typen'
import { KategorieAuswahl } from '@/components/auswahl/KategorieAuswahl'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { formatEuro } from '@/lib/format'
import { useBelegAuswahl } from './auswahl'
import { sucheValidieren, zuAbfrage } from './suche'
import { istZuPruefen } from './Zeilenmenue'

const belegeText = (n: number) => (n === 1 ? '1 Beleg' : `${n} Belege`)
const belegenText = (n: number) => (n === 1 ? '1 Beleg' : `${n} Belegen`)

/**
 * Flacher, heller Knopf auf der dunklen Leiste. Ist die Leiste schmal (Container-Query), bleibt nur das Icon
 * sichtbar; die Beschriftung bleibt für Screenreader erhalten und erscheint als Tooltip.
 */
const knopf =
  '[--btn-laden-farbe:#fff] inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md border-0 bg-transparent px-2.5 text-[13px] font-semibold whitespace-nowrap text-white hover:bg-white/10 disabled:cursor-default disabled:opacity-50 data-[state=open]:bg-white/10 @max-5xl:px-2 [&_.material-symbols-outlined]:text-[18px]'

function Knopf({ icon, label, ...rest }: { icon: string; label: string } & React.ComponentProps<'button'>) {
  return (
    <button type="button" className={knopf} title={label} {...rest}>
      <Icon name={icon} />
      <span className="@max-5xl:sr-only">{label}</span>
    </button>
  )
}

/** Senkrechter Trenner zwischen Knopfgruppen. */
const Trenner = () => <span aria-hidden className="mx-1.5 h-5 w-px shrink-0 bg-white/20 @max-5xl:mx-0.5" />

/**
 * Dunkle Leiste bei vorhandener Auswahl (ersetzt die Filterleiste): Anzahl, Summe der gewählten Belege und
 * „Alle N auswählen“; Tags, Kategorie | Als bezahlt markieren, Exportieren; rechts „Bestätigen“ (nur wenn
 * ein gewählter Beleg noch „zu prüfen“ ist) und ✕ (Auswahl aufheben). NurLesen sieht keine ändernden Aktionen.
 */
export function Massenaktionen() {
  const auswahl = useBelegAuswahl()
  const suche = sucheValidieren(useSearch({ strict: false }) as Record<string, unknown>)
  const abfrage = zuAbfrage(suche)
  const { data: liste } = useBelege(abfrage)
  const queryClient = useQueryClient()
  const { data: tags = [] } = useTags()
  const { darfBearbeiten } = useRolle()
  const massenaktion = useMassenaktion()
  const navigate = useNavigate()
  const toast = useToast()
  // Einträge aus „Alle N auswählen“ – damit Summe und „Bestätigen“ auch Belege anderer Seiten kennen.
  const [alleEintraege, setAlleEintraege] = useState<BelegListeneintrag[]>([])
  const [alleLaden, setAlleLaden] = useState(false)

  const anzahl = auswahl.ids.length
  const bekannt = new Map([...alleEintraege, ...(liste?.eintraege ?? [])].map((b) => [b.id, b]))
  const gewaehlt = auswahl.ids.flatMap((id) => bekannt.get(id) ?? [])
  const summe = gewaehlt.reduce((s, b) => s + (b.brutto ?? 0), 0)
  // Unbekannte Belege könnten „zu prüfen“ sein – dann lieber anbieten.
  const etwasZuBestaetigen = gewaehlt.length < anzahl || gewaehlt.some(istZuPruefen)
  // Ausgangszustand der Tag-Auswahl: Tags, die alle gewählten Belege tragen.
  const gemeinsameTags =
    gewaehlt.length === 0
      ? []
      : gewaehlt[0].tags.map((t) => t.id).filter((id) => gewaehlt.every((b) => b.tags.some((t) => t.id === id)))
  const zahlungsTags = tags.filter((t) => t.istZahlungsTag)
  const gesamt = liste?.gesamt ?? 0

  async function alleWaehlen() {
    if (gesamt === 0) return
    const alleAbfrage = { ...abfrage, seite: 1, seitengroesse: gesamt }
    setAlleLaden(true)
    try {
      const alle = await queryClient.fetchQuery({
        queryKey: belegeKeys.liste(alleAbfrage),
        queryFn: ({ signal }) => daten(api.GET('/api/belege', { params: { query: alleAbfrage }, signal })),
      })
      setAlleEintraege(alle.eintraege)
      auswahl.alleSetzen(alle.eintraege.map((b) => b.id))
    } finally {
      setAlleLaden(false)
    }
  }

  /** Läuft gerade eine der Aktionen? Nur deren Knopf zeigt den Drehkreis, die übrigen sind bloß gesperrt. */
  const laeuft = (...aktionen: Massenaktion['aktion'][]) =>
    (massenaktion.isPending && aktionen.includes(massenaktion.variables.aktion)) || undefined

  function ausfuehren(eingabe: Omit<Massenaktion, 'belegIds'>, erfolg: (n: number) => string) {
    const belegIds = auswahl.ids
    massenaktion.mutate(
      { ...eingabe, belegIds },
      {
        onSuccess: ({ geaendert }) => {
          auswahl.leeren()
          toast.erfolg(erfolg(geaendert))
        },
        onError: () =>
          toast.fehler('Die Massenaktion ist fehlgeschlagen.', { erneut: () => ausfuehren(eingabe, erfolg) }),
      },
    )
  }

  function tagsAendern(neu: string[]) {
    const hinzu = neu.filter((id) => !gemeinsameTags.includes(id))
    const weg = gemeinsameTags.filter((id) => !neu.includes(id))
    if (hinzu.length) ausfuehren({ aktion: 'TagsSetzen', tagIds: hinzu }, (n) => `Tags bei ${belegenText(n)} gesetzt`)
    if (weg.length) ausfuehren({ aktion: 'TagsEntfernen', tagIds: weg }, (n) => `Tags bei ${belegenText(n)} entfernt`)
  }

  function alsBezahlt(tagId: string) {
    ausfuehren({ aktion: 'TagsSetzen', tagIds: [tagId] }, (n) => `${belegeText(n)} als bezahlt markiert`)
  }

  function exportieren() {
    void navigate({
      to: '/export',
      search: {
        von: suche.von,
        bis: suche.bis,
        kategorien: suche.kategorien,
        korrespondenten: suche.korrespondenten,
        tags: suche.tags,
      },
    } as never)
  }

  return (
    // Der Container misst die Leistenbreite (nicht das Fenster) – mit Vorschau-Panel ist sie schmaler.
    <div className="@container">
      <div
        role="toolbar"
        aria-label="Massenaktionen"
        className="flex min-h-[58px] flex-wrap items-center gap-1 rounded-[10px] bg-seitenleiste py-2 pr-2 pl-4 text-white"
      >
        <div className="mr-2 flex min-w-0 flex-col leading-tight @max-5xl:mr-0.5" aria-live="polite">
          <span className="text-sm font-semibold">
            {anzahl === 1 ? '1 Beleg ausgewählt' : `${anzahl} Belege ausgewählt`}
          </span>
          <span className="text-xs text-seitenleiste-gedaempft">
            Summe <span className="num">{formatEuro(summe)}</span>
            {anzahl < gesamt && (
              <>
                {' · '}
                <button
                  type="button"
                  onClick={() => void alleWaehlen()}
                  disabled={alleLaden}
                  aria-busy={alleLaden || undefined}
                  className="cursor-pointer border-0 bg-transparent p-0 text-xs font-semibold text-white underline underline-offset-2 hover:no-underline"
                >
                  Alle {gesamt} auswählen
                </button>
              </>
            )}
          </span>
        </div>
        <Trenner />
        {darfBearbeiten && (
          <>
            <TagAuswahl
              gewaehlt={gemeinsameTags}
              onAendern={tagsAendern}
              ausloeser={
                <Knopf
                  icon="sell"
                  label="Tags"
                  disabled={massenaktion.isPending}
                  aria-busy={laeuft('TagsSetzen', 'TagsEntfernen')}
                />
              }
            />
            <KategorieAuswahl
              wert={null}
              onAendern={(kategorieId) =>
                kategorieId &&
                ausfuehren({ aktion: 'KategorieSetzen', kategorieId }, (n) => `Kategorie bei ${belegenText(n)} gesetzt`)
              }
              ausloeser={
                <Knopf
                  icon="category"
                  label="Kategorie"
                  disabled={massenaktion.isPending}
                  aria-busy={laeuft('KategorieSetzen')}
                />
              }
            />
            <Trenner />
            {zahlungsTags.length === 1 ? (
              <Knopf
                icon="paid"
                label="Als bezahlt markieren"
                disabled={massenaktion.isPending}
                aria-busy={laeuft('TagsSetzen')}
                onClick={() => alsBezahlt(zahlungsTags[0].id)}
              />
            ) : (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Knopf
                    icon="paid"
                    label="Als bezahlt markieren"
                    disabled={massenaktion.isPending}
                    aria-busy={laeuft('TagsSetzen')}
                  />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" aria-label="Zahlungs-Tag wählen">
                  {zahlungsTags.length === 0 ? (
                    <>
                      <DropdownMenuItem disabled>Noch kein Zahlungs-Tag angelegt</DropdownMenuItem>
                      <DropdownMenuItem onSelect={() => void navigate({ to: '/tags' })}>
                        <Icon name="add" />
                        Zahlungs-Tag anlegen …
                      </DropdownMenuItem>
                    </>
                  ) : (
                    zahlungsTags.map((t) => (
                      <DropdownMenuItem key={t.id} onSelect={() => alsBezahlt(t.id)}>
                        {t.name}
                      </DropdownMenuItem>
                    ))
                  )}
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </>
        )}
        <Knopf icon="download" label="Exportieren" onClick={exportieren} />
        <span className="flex-1" />
        {darfBearbeiten && etwasZuBestaetigen && (
          <button
            type="button"
            title="Bestätigen"
            disabled={massenaktion.isPending}
            aria-busy={laeuft('Bestaetigen')}
            onClick={() => ausfuehren({ aktion: 'Bestaetigen' }, (n) => `${belegeText(n)} bestätigt`)}
            className="[--btn-laden-farbe:var(--color-seitenleiste)] inline-flex h-9 shrink-0 cursor-pointer items-center gap-1.5 rounded-lg border-0 bg-white px-4 text-[13px] font-semibold whitespace-nowrap text-seitenleiste hover:bg-white/90 disabled:cursor-default disabled:opacity-60 @max-2xl:px-2.5 [&_.material-symbols-outlined]:text-[18px]"
          >
            <Icon name="check_circle" />
            <span className="@max-2xl:sr-only">Bestätigen</span>
          </button>
        )}
        <button
          type="button"
          onClick={auswahl.leeren}
          aria-label="Auswahl aufheben"
          title="Auswahl aufheben"
          className="ml-1 inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-white hover:bg-white/10 [&_.material-symbols-outlined]:text-[20px]"
        >
          <Icon name="close" />
        </button>
      </div>
    </div>
  )
}
