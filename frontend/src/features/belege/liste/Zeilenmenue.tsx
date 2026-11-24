import { useNavigate, useRouter } from '@tanstack/react-router'
import { useEffect, useRef, useState } from 'react'
import {
  belegDateiUrl,
  useBeleg,
  useBelegAendern,
  useBelegBestaetigen,
  useErneutAuslesen,
  useTagEntfernen,
  useTagHinzufuegen,
} from '@/api/belege'
import { useTags } from '@/api/tags'
import type { BelegDetail, BelegListeneintrag } from '@/api/typen'
import { KategorieAuswahl } from '@/components/auswahl/KategorieAuswahl'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { aufZeileOeffnenHoeren } from './useListenKuerzel'

/** Beleg trägt den System-Tag „zu prüfen“. */
export function istZuPruefen(beleg: BelegListeneintrag): boolean {
  return beleg.tags.some((t) => t.istSystem)
}

/** Kopfdaten eines Belegs als vollständige Eingabe für `PUT /api/belege/{id}` mit neuer Kategorie. */
function mitKategorie(d: BelegDetail, kategorieId: string | null) {
  return {
    id: d.id,
    korrespondentId: d.korrespondent?.id ?? null,
    belegart: d.belegart,
    rechnungsnummer: d.rechnungsnummer ?? null,
    titel: d.titel,
    rechnungsdatum: d.rechnungsdatum ?? null,
    leistungsdatum: d.leistungsdatum ?? null,
    leistungVon: d.leistungVon ?? null,
    leistungBis: d.leistungBis ?? null,
    faelligkeitsdatum: d.faelligkeitsdatum ?? null,
    kategorieId,
    waehrung: d.waehrung,
    netto: d.netto ?? null,
    steuer: d.steuer ?? null,
    brutto: d.brutto ?? null,
    steuerzeilen: d.steuerzeilen.map(({ satz, netto, steuer }) => ({ satz, netto, steuer })),
  }
}

/** Lädt das Beleg-Detail erst, wenn es gebraucht wird (Kategorie setzen, Original herunterladen). */
function DetailLader({ id, onGeladen }: { id: string; onGeladen: (d: BelegDetail) => void }) {
  const { data } = useBeleg(id)
  useEffect(() => {
    if (data) onGeladen(data)
  }, [data, onGeladen])
  return null
}

/**
 * Zeilenmenü der Belegliste: ⋮-Knopf (oder Rechtsklick auf die Zeile) öffnet Öffnen, In neuem Tab, Tags …,
 * Kategorie …, Als bezahlt markieren ▸, Bestätigen (nur bei „zu prüfen“), Erneut auslesen, Original herunterladen.
 */
export function Zeilenmenue({ beleg }: { beleg: BelegListeneintrag }) {
  const navigate = useNavigate()
  const router = useRouter()
  const toast = useToast()
  const { darfBearbeiten } = useRolle()
  const { data: tags = [] } = useTags()
  const tagHinzufuegen = useTagHinzufuegen()
  const tagEntfernen = useTagEntfernen()
  const aendern = useBelegAendern()
  const bestaetigen = useBelegBestaetigen()
  const erneutAuslesen = useErneutAuslesen()
  const [offen, setOffen] = useState(false)
  const [detail, setDetail] = useState<BelegDetail | null>(null)
  const [detailNoetig, setDetailNoetig] = useState(false)
  const tagKnopf = useRef<HTMLButtonElement>(null)
  const kategorieKnopf = useRef<HTMLButtonElement>(null)
  const zahlungsTags = tags.filter((t) => t.istZahlungsTag)
  const tagIds = beleg.tags.map((t) => t.id)
  const pfad = { to: '/belege/$belegId', params: { belegId: beleg.id } } as const

  useEffect(
    () =>
      aufZeileOeffnenHoeren((id, was) => {
        if (id !== beleg.id) return
        if (was === 'menue') setOffen(true)
        else if (!darfBearbeiten) return
        else if (was === 'tags') tagKnopf.current?.click()
        else {
          setDetailNoetig(true)
          kategorieKnopf.current?.click()
        }
      }),
    [beleg.id, darfBearbeiten],
  )

  function fehler(erneut: () => void) {
    return () => toast.fehler('Die Aktion ist fehlgeschlagen.', { erneut })
  }

  function tagsAendern(neu: string[]) {
    for (const tagId of neu.filter((t) => !tagIds.includes(t)))
      tagHinzufuegen.mutate({ id: beleg.id, tagId }, { onError: fehler(() => tagsAendern(neu)) })
    for (const tagId of tagIds.filter((t) => !neu.includes(t)))
      tagEntfernen.mutate({ id: beleg.id, tagId }, { onError: fehler(() => tagsAendern(neu)) })
  }

  function alsBezahlt(tagId: string, name: string) {
    tagHinzufuegen.mutate(
      { id: beleg.id, tagId },
      { onSuccess: () => toast.erfolg(`Als „${name}“ markiert`), onError: fehler(() => alsBezahlt(tagId, name)) },
    )
  }

  function kategorieSetzen(kategorieId: string | null) {
    if (!detail) return
    aendern.mutate(mitKategorie(detail, kategorieId), {
      onSuccess: () => toast.erfolg('Kategorie gesetzt'),
      onError: fehler(() => kategorieSetzen(kategorieId)),
    })
  }

  function bestaetigenAusfuehren() {
    bestaetigen.mutate(
      { id: beleg.id },
      { onSuccess: () => toast.erfolg('Beleg bestätigt'), onError: fehler(bestaetigenAusfuehren) },
    )
  }

  function erneutAuslesenAusfuehren() {
    erneutAuslesen.mutate(beleg.id, {
      onSuccess: () => toast.info('Beleg wird erneut ausgelesen'),
      onError: fehler(erneutAuslesenAusfuehren),
    })
  }

  const original = detail?.dateien.find((d) => d.rolle === 'Original')
  const versteckt = 'pointer-events-none absolute top-0 right-0 h-full w-0 opacity-0'

  return (
    <span className="relative inline-flex">
      {detailNoetig && <DetailLader id={beleg.id} onGeladen={setDetail} />}
      <DropdownMenu
        open={offen}
        onOpenChange={(o) => {
          setOffen(o)
          if (o) setDetailNoetig(true)
        }}
      >
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            aria-label={`Aktionen für ${beleg.titel}`}
            className="btn btn-klein w-[30px] border-transparent px-0 text-gedaempft"
          >
            <Icon name="more_vert" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent aria-label={`Aktionen für ${beleg.titel}`}>
          <DropdownMenuItem onSelect={() => void navigate(pfad)}>
            <Icon name="open_in_full" />
            Öffnen
            <DropdownMenuShortcut>Enter</DropdownMenuShortcut>
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={() => window.open(router.buildLocation(pfad).href, '_blank', 'noopener')}>
            <Icon name="open_in_new" />
            In neuem Tab
          </DropdownMenuItem>
          {darfBearbeiten && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => setTimeout(() => tagKnopf.current?.click())}>
                <Icon name="sell" />
                Tags …<DropdownMenuShortcut>T</DropdownMenuShortcut>
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setTimeout(() => kategorieKnopf.current?.click())}>
                <Icon name="folder" />
                Kategorie …<DropdownMenuShortcut>K</DropdownMenuShortcut>
              </DropdownMenuItem>
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <Icon name="payments" />
                  Als bezahlt markieren
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent aria-label="Zahlungs-Tag wählen">
                  {zahlungsTags.map((t) => (
                    <DropdownMenuItem key={t.id} onSelect={() => alsBezahlt(t.id, t.name)}>
                      {t.name}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>
              {istZuPruefen(beleg) && (
                <DropdownMenuItem onSelect={bestaetigenAusfuehren}>
                  <Icon name="check_circle" />
                  Bestätigen
                </DropdownMenuItem>
              )}
              <DropdownMenuItem onSelect={erneutAuslesenAusfuehren}>
                <Icon name="refresh" />
                Erneut auslesen
              </DropdownMenuItem>
            </>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem
            disabled={!original}
            onSelect={() => {
              if (original) window.location.assign(belegDateiUrl(beleg.id, original.id))
            }}
          >
            <Icon name="download" />
            Original herunterladen
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {darfBearbeiten && (
        <>
          <TagAuswahl
            gewaehlt={tagIds}
            onAendern={tagsAendern}
            ausloeser={<button ref={tagKnopf} type="button" tabIndex={-1} aria-hidden="true" className={versteckt} />}
          />
          <KategorieAuswahl
            wert={detail?.kategorie?.id ?? beleg.kategorie?.id ?? null}
            onAendern={kategorieSetzen}
            ausloeser={
              <button ref={kategorieKnopf} type="button" tabIndex={-1} aria-hidden="true" className={versteckt} />
            }
          />
        </>
      )}
    </span>
  )
}
