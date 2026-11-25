import { useNavigate, useSearch } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { useBelege, useMassenaktion } from '@/api/belege'
import { useTags } from '@/api/tags'
import type { Massenaktion } from '@/api/typen'
import { KategorieAuswahl } from '@/components/auswahl/KategorieAuswahl'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { formatEuro } from '@/lib/format'
import { useBelegAuswahl } from './auswahl'
import { sucheValidieren, zuAbfrage } from './suche'

const belegeText = (n: number) => (n === 1 ? '1 Beleg' : `${n} Belege`)
const belegenText = (n: number) => (n === 1 ? '1 Beleg' : `${n} Belegen`)

const knopf =
  '[--btn-laden-farbe:var(--color-auf-akzent)] inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md border border-white/25 bg-white/10 px-3 text-[13px] font-medium text-auf-akzent hover:bg-white/20 [&_.material-symbols-outlined]:text-[18px]'

function Knopf({ icon, children, ...rest }: { icon: string; children: ReactNode } & React.ComponentProps<'button'>) {
  return (
    <button type="button" className={knopf} {...rest}>
      <Icon name={icon} />
      {children}
    </button>
  )
}

/**
 * Leiste bei vorhandener Auswahl (ersetzt die Filterleiste): Anzahl und Summe der gewählten Belege der Seite,
 * Tags, Kategorie, Als bezahlt markieren, Bestätigen, Exportieren, Auswahl aufheben. NurLesen sieht nur
 * Exportieren und Auswahl aufheben.
 */
export function Massenaktionen() {
  const auswahl = useBelegAuswahl()
  const suche = sucheValidieren(useSearch({ strict: false }) as Record<string, unknown>)
  const { data: liste } = useBelege(zuAbfrage(suche))
  const { data: tags = [] } = useTags()
  const { darfBearbeiten } = useRolle()
  const massenaktion = useMassenaktion()
  const navigate = useNavigate()
  const toast = useToast()

  const anzahl = auswahl.ids.length
  const gewaehlt = (liste?.eintraege ?? []).filter((b) => auswahl.istGewaehlt(b.id))
  const summe = gewaehlt.reduce((s, b) => s + (b.brutto ?? 0), 0)
  // Ausgangszustand der Tag-Auswahl: Tags, die alle gewählten Belege tragen.
  const gemeinsameTags =
    gewaehlt.length === 0
      ? []
      : gewaehlt[0].tags.map((t) => t.id).filter((id) => gewaehlt.every((b) => b.tags.some((t) => t.id === id)))
  const zahlungsTags = tags.filter((t) => t.istZahlungsTag)

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
    <div
      role="toolbar"
      aria-label="Massenaktionen"
      className="flex min-h-[60px] flex-wrap items-center gap-2 rounded-[10px] bg-akzent py-3 pr-3 pl-4 text-auf-akzent"
    >
      <span className="mr-2 text-sm font-semibold" aria-live="polite">
        {anzahl === 1 ? '1 Beleg ausgewählt' : `${anzahl} Belege ausgewählt`}
        <span className="num font-normal text-[#D7D6EE]"> · {formatEuro(summe)}</span>
      </span>
      {darfBearbeiten && (
        <>
          <TagAuswahl
            gewaehlt={gemeinsameTags}
            onAendern={tagsAendern}
            ausloeser={
              <Knopf icon="sell" disabled={massenaktion.isPending}>
                Tags
              </Knopf>
            }
          />
          <KategorieAuswahl
            wert={null}
            onAendern={(kategorieId) =>
              kategorieId &&
              ausfuehren({ aktion: 'KategorieSetzen', kategorieId }, (n) => `Kategorie bei ${belegenText(n)} gesetzt`)
            }
            ausloeser={
              <Knopf icon="folder" disabled={massenaktion.isPending}>
                Kategorie
              </Knopf>
            }
          />
          {zahlungsTags.length === 1 ? (
            <Knopf
              icon="payments"
              disabled={massenaktion.isPending}
              aria-busy={massenaktion.isPending && massenaktion.variables?.aktion === 'TagsSetzen'}
              onClick={() => alsBezahlt(zahlungsTags[0].id)}
            >
              Als bezahlt markieren
            </Knopf>
          ) : (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Knopf icon="payments" disabled={massenaktion.isPending}>
                  Als bezahlt markieren
                </Knopf>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" aria-label="Zahlungs-Tag wählen">
                {zahlungsTags.map((t) => (
                  <DropdownMenuItem key={t.id} onSelect={() => alsBezahlt(t.id)}>
                    {t.name}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
          <Knopf
            icon="check_circle"
            disabled={massenaktion.isPending}
            aria-busy={massenaktion.isPending && massenaktion.variables?.aktion === 'Bestaetigen'}
            onClick={() => ausfuehren({ aktion: 'Bestaetigen' }, (n) => `${belegeText(n)} bestätigt`)}
          >
            Bestätigen
          </Knopf>
        </>
      )}
      <Knopf icon="download" onClick={exportieren}>
        Exportieren
      </Knopf>
      <span className="flex-1" />
      <button
        type="button"
        onClick={auswahl.leeren}
        className="cursor-pointer border-0 bg-transparent text-[13px] font-medium text-[#D7D6EE] hover:text-auf-akzent"
      >
        Auswahl aufheben
      </button>
    </div>
  )
}
