import { Link } from '@tanstack/react-router'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatDatum, formatEuro } from '@/lib/format'
import { cn } from '@/lib/utils'
import { AehnlichePopover } from './AehnlichePopover'
import { useBelegAuswahl } from './auswahl'
import { auswahlLabel, faelligText, gruppeGrau, TagChips, useOriginalHerunterladen, Vorschaubild } from './bausteine'

const meta =
  'flex items-center gap-1.5 text-[13px] whitespace-nowrap text-gedaempft-stark [&_.material-symbols-outlined]:text-[17px] [&_.material-symbols-outlined]:[vertical-align:0]'

/** Große Karten untereinander: Vorschau links, rechts Titel mit Tags, Auszug, Aktionen und Eckdaten. */
export function GrosseKarten({
  eintraege,
  onVorschau,
}: {
  eintraege: BelegListeneintrag[]
  onVorschau: (beleg: BelegListeneintrag) => void
}) {
  const auswahl = useBelegAuswahl()
  const herunterladen = useOriginalHerunterladen()

  return (
    <div className="flex flex-col gap-3.5">
      {eintraege.map((beleg) => {
        const gewaehlt = auswahl.istGewaehlt(beleg.id)
        return (
          <article
            key={beleg.id}
            aria-label={[beleg.korrespondent?.name, beleg.titel].filter(Boolean).join(': ')}
            className={cn('karte flex flex-wrap overflow-hidden', gewaehlt && 'border-2 border-akzent')}
          >
            <div className="relative flex h-[196px] flex-[0_0_248px] items-start justify-center overflow-hidden border-r border-rand bg-segment">
              <Vorschaubild beleg={beleg} gross />
              <label
                title="Auswählen"
                className="absolute top-2 left-2 flex h-[22px] w-[22px] items-center justify-center rounded-[5px] bg-white/90 shadow-[0_0_0_1px_#C9CED6]"
              >
                <input
                  type="checkbox"
                  aria-label={auswahlLabel(beleg)}
                  checked={gewaehlt}
                  onChange={() => auswahl.umschalten(beleg.id)}
                  className="m-0 h-[15px] w-[15px]"
                />
              </label>
            </div>
            <div className="flex min-w-0 flex-[1_1_420px] flex-col gap-2.5 px-5 py-4">
              <div className="flex flex-wrap items-center gap-2">
                <Link
                  to="/belege/$belegId"
                  params={{ belegId: beleg.id }}
                  className="text-[19px] font-medium text-text no-underline"
                >
                  {beleg.korrespondent && (
                    <span className="font-semibold text-akzent-text">{beleg.korrespondent.name}:</span>
                  )}{' '}
                  {beleg.titel}
                </Link>
                <TagChips tags={beleg.tags} />
              </div>
              <p className="m-0 line-clamp-3 text-sm leading-[1.55] text-gedaempft-stark">{beleg.auszug}</p>
              <span className="flex-1" />
              <div className="flex flex-wrap items-center justify-between gap-x-5 gap-y-2.5">
                <div className={gruppeGrau}>
                  <AehnlichePopover beleg={beleg} />
                  <Link to="/belege/$belegId" params={{ belegId: beleg.id }}>
                    <Icon name="edit_document" />
                    Öffnen
                  </Link>
                  <button type="button" onClick={() => onVorschau(beleg)}>
                    <Icon name="visibility" />
                    Vorschau
                  </button>
                  <button type="button" onClick={() => void herunterladen(beleg.id)}>
                    <Icon name="download" />
                    Herunterladen
                  </button>
                </div>
                <div className="flex flex-wrap gap-x-[18px] gap-y-1.5">
                  <span className={meta}>
                    <Icon name="category" />
                    {beleg.kategorie?.name ?? '–'}
                  </span>
                  <span className={meta}>
                    <Icon name="calendar_today" />
                    {beleg.rechnungsdatum ? formatDatum(beleg.rechnungsdatum) : '–'}
                  </span>
                  <span className={cn(meta, beleg.ueberfaellig && 'font-semibold !text-rot-text')}>
                    <Icon name="event" />
                    fällig {faelligText(beleg)}
                  </span>
                  <span className={meta}>
                    <Icon name="description" />
                    {beleg.seitenanzahl === 1 ? '1 Seite' : `${beleg.seitenanzahl} Seiten`}
                  </span>
                  <span className={cn(meta, 'font-semibold !text-text')}>
                    <Icon name="euro" />
                    {beleg.brutto !== null && beleg.brutto !== undefined
                      ? formatEuro(beleg.brutto, beleg.waehrung)
                      : '–'}
                  </span>
                </div>
              </div>
            </div>
          </article>
        )
      })}
    </div>
  )
}
