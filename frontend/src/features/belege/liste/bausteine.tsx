import { useQueryClient } from '@tanstack/react-query'
import type { ComponentProps } from 'react'
import { belegeKeys } from '@/api/belege'
import { api, daten } from '@/api/client'
import type { BelegListeneintrag, TagKurz } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatDatum } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Knopfgruppe mit Akzent-Rahmen (`.grp` im Design). */
export const gruppe =
  'inline-flex overflow-hidden rounded-[7px] border border-akzent bg-karte [&>*]:inline-flex [&>*]:h-8 [&>*]:cursor-pointer [&>*]:items-center [&>*]:justify-center [&>*]:gap-1.5 [&>*]:border-0 [&>*]:border-l [&>*]:border-akzent [&>*]:bg-karte [&>*]:px-[11px] [&>*]:text-sm [&>*]:font-medium [&>*]:whitespace-nowrap [&>*]:text-akzent-text [&>*]:no-underline [&>*:first-child]:border-l-0 [&>*:hover]:bg-akzent-zart [&>*:disabled]:cursor-default [&>*:disabled]:opacity-50'

/** Graue Knopfgruppe (`.grp-g` im Design): Aktionen auf Karten und Seitennavigation. */
export const gruppeGrau =
  'inline-flex overflow-hidden rounded-[7px] border border-eingabe-rand bg-karte [&>*]:inline-flex [&>*]:h-[30px] [&>*]:cursor-pointer [&>*]:items-center [&>*]:justify-center [&>*]:gap-1.5 [&>*]:border-0 [&>*]:border-l [&>*]:border-eingabe-rand [&>*]:bg-karte [&>*]:px-[11px] [&>*]:text-[13px] [&>*]:font-medium [&>*]:whitespace-nowrap [&>*]:text-gedaempft-stark [&>*]:no-underline [&>*:first-child]:border-l-0 [&>*:hover]:bg-akzent-zart [&>*:disabled]:cursor-default [&>*:disabled]:opacity-50 [&_.material-symbols-outlined]:text-[18px] [&_.material-symbols-outlined]:[vertical-align:0]'

/**
 * Kombi-Knopf: mehrere Knöpfe in einem gemeinsamen Akzent-Rahmen, durch Linien getrennt
 * (z. B. Split-Button „Seite auswählen | ▾“). Die Knöpfe darin nutzen `kombiTeil`.
 */
export const kombi =
  'inline-flex h-8 overflow-hidden rounded-[7px] border border-akzent bg-karte [&>*+*]:border-l [&>*+*]:border-akzent'

/** Ein Knopf innerhalb von `kombi`. */
export const kombiTeil =
  'inline-flex h-full cursor-pointer items-center gap-1.5 bg-karte px-2.5 text-sm font-medium whitespace-nowrap text-akzent-text hover:bg-akzent-zart data-[state=open]:bg-akzent-zart [&_.material-symbols-outlined]:text-[18px]'

/** Menü-Knopf mit Pfeil (`.dd`); `aktiv` füllt ihn, `anzahl` zeigt eine Plakette. */
export function MenueKnopf({
  icon,
  label,
  aktiv = false,
  anzahl,
  className,
  ...props
}: ComponentProps<'button'> & { icon: string; label: string; aktiv?: boolean; anzahl?: number }) {
  return (
    <button
      type="button"
      className={cn(
        'relative inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-[7px] border border-akzent pr-2 pl-2.5 text-sm font-medium whitespace-nowrap [&_.material-symbols-outlined]:text-[18px]',
        aktiv ? 'bg-akzent text-auf-akzent' : 'bg-karte text-akzent-text hover:bg-akzent-zart',
        className,
      )}
      aria-label={anzahl ? `${label}, aktiv: ${anzahl}` : undefined}
      {...props}
    >
      <Icon name={icon} />
      {label}
      <Icon name="arrow_drop_down" />
      {anzahl !== undefined && anzahl > 0 && (
        <span
          aria-hidden="true"
          className="absolute -top-2 -right-2 flex h-[18px] min-w-[18px] items-center justify-center rounded-[9px] border-2 border-karte bg-gedaempft px-1 text-[11px] font-semibold text-auf-akzent"
        >
          {anzahl}
        </span>
      )}
    </button>
  )
}

/** Tag-Chips eines Belegs. */
export function TagChips({ tags, className }: { tags: TagKurz[]; className?: string }) {
  return (
    <>
      {tags.map((t) => (
        <span key={t.id} className={className}>
          <Chip ton={t.farbe as ChipTon}>{t.name}</Chip>
        </span>
      ))}
    </>
  )
}

/** „TT.MM.JJJJ“, bei Überfälligkeit mit Zusatz „· überfällig“. */
export function faelligText(beleg: BelegListeneintrag): string {
  if (!beleg.faelligkeitsdatum) return '–'
  const datum = formatDatum(beleg.faelligkeitsdatum)
  return beleg.ueberfaellig ? `${datum} · überfällig` : datum
}

/** „<Korrespondent> <Nr.> auswählen“ für die Auswahlbox. */
export function auswahlLabel(beleg: BelegListeneintrag): string {
  return [beleg.korrespondent?.name ?? beleg.titel, beleg.rechnungsnummer, 'auswählen'].filter(Boolean).join(' ')
}

/** Stilisierte Rechnungsseite, wenn es kein Vorschaubild gibt. */
function Platzhalterseite({ gross, gescannt }: { gross: boolean; gescannt: boolean }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'flex flex-col gap-1 shadow-blatt',
        gross ? 'mt-[104px] h-[290px] w-[212px] p-3.5' : 'h-[178px] w-[132px] px-[9px] py-2.5',
        gescannt ? '-rotate-1 bg-[#FBFAF4]' : 'bg-white',
      )}
    >
      <div className="flex items-center gap-1">
        <span className={cn('rounded-[3px] bg-akzent', gross ? 'h-5 w-5' : 'h-3.5 w-3.5')} />
        <span className="h-[3px] w-11 rounded-[1px] bg-akzent opacity-55" />
        <span className="flex-1" />
        <span className={cn('font-bold tracking-[0.06em] text-gedaempft-stark', gross ? 'text-[7px]' : 'text-[5px]')}>
          RECHNUNG
        </span>
      </div>
      <span className="mt-1 h-[3px] w-[52%] rounded-[1px] bg-[#DDE1E7]" />
      <span className="h-[3px] w-[40%] rounded-[1px] bg-[#DDE1E7]" />
      <span className="h-[3px] w-[46%] rounded-[1px] bg-[#DDE1E7]" />
      <div className="mt-1 flex flex-col gap-1 border-t-2 border-akzent pt-1">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className="flex gap-1.5">
            <span className="h-[3px] flex-1 rounded-[1px] bg-[#DDE1E7]" />
            <span className="h-[3px] w-[18px] rounded-[1px] bg-[#DDE1E7]" />
          </div>
        ))}
      </div>
    </div>
  )
}

/** Vorschaubild der ersten Seite (lazy) oder die Platzhalterseite. */
export function Vorschaubild({ beleg, gross = false }: { beleg: BelegListeneintrag; gross?: boolean }) {
  if (beleg.vorschauUrl)
    return (
      <img
        src={beleg.vorschauUrl}
        alt=""
        loading="lazy"
        className={cn('bg-white object-contain shadow-blatt', gross ? 'h-[180px] w-[212px]' : 'h-[178px] w-[132px]')}
      />
    )
  return <Platzhalterseite gross={gross} gescannt={beleg.format === 'Scan'} />
}

/**
 * Lädt das Original eines Belegs herunter. Die Listeneinträge kennen die Datei-ID nicht;
 * sie kommt aus dem Beleg-Detail (aus dem Cache, sonst einmal geladen).
 */
export function useOriginalHerunterladen() {
  const queryClient = useQueryClient()
  return async (id: string) => {
    const beleg = await queryClient.fetchQuery({
      queryKey: belegeKeys.detail(id),
      queryFn: ({ signal }) => daten(api.GET('/api/belege/{id}', { params: { path: { id } }, signal })),
    })
    const original = beleg.dateien.find((d) => d.rolle === 'Original')
    if (!original) return
    const link = document.createElement('a')
    link.href = original.url
    link.download = ''
    link.click()
  }
}
