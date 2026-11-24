import { Link } from '@tanstack/react-router'
import type { BelegListeneintrag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatDatum, formatEuro } from '@/lib/format'
import { cn } from '@/lib/utils'
import { useBelegAuswahl } from './auswahl'
import { auswahlLabel, gruppeGrau, TagChips, useOriginalHerunterladen, Vorschaubild } from './bausteine'

const meta =
  'flex items-center gap-1.5 text-[13px] whitespace-nowrap text-gedaempft-stark [&_.material-symbols-outlined]:text-[17px] [&_.material-symbols-outlined]:[vertical-align:0]'

/** Raster kleiner Karten: Vorschaubild mit Auswahl, Tags und Format, darunter Eckdaten und Aktionen. */
export function KleineKarten({
  eintraege,
  onVorschau,
}: {
  eintraege: BelegListeneintrag[]
  onVorschau: (beleg: BelegListeneintrag) => void
}) {
  const auswahl = useBelegAuswahl()
  const herunterladen = useOriginalHerunterladen()

  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(176px,1fr))] gap-3.5">
      {eintraege.map((beleg) => {
        const gewaehlt = auswahl.istGewaehlt(beleg.id)
        return (
          <article
            key={beleg.id}
            aria-label={[beleg.korrespondent?.name, beleg.titel].filter(Boolean).join(': ')}
            className={cn('karte flex flex-col overflow-hidden', gewaehlt && 'border-2 border-akzent')}
          >
            <div className="relative flex h-[196px] items-center justify-center overflow-hidden bg-segment">
              <Vorschaubild beleg={beleg} />
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
              <div className="absolute top-2 right-2 flex flex-col items-end gap-1">
                <TagChips tags={beleg.tags} className="shadow-[0_1px_2px_rgba(21,25,34,0.15)]" />
              </div>
              <span className="absolute bottom-2 left-2 shadow-[0_1px_2px_rgba(21,25,34,0.15)]">
                <Chip ton="grau">{beleg.format}</Chip>
              </span>
            </div>
            <div className="flex flex-1 flex-col gap-2 border-t border-rand px-3 pt-2.5 pb-1">
              <Link
                to="/belege/$belegId"
                params={{ belegId: beleg.id }}
                className="text-sm leading-[1.35] text-text no-underline"
              >
                {beleg.korrespondent && (
                  <strong className="font-semibold text-akzent-text">{beleg.korrespondent.name}:</strong>
                )}{' '}
                {beleg.titel}
              </Link>
              <span className="flex-1" />
              <span className={meta}>
                <Icon name="category" />
                {beleg.kategorie?.name ?? '–'}
              </span>
              <span className={meta}>
                <Icon name="calendar_today" />
                {beleg.rechnungsdatum ? formatDatum(beleg.rechnungsdatum) : '–'}
              </span>
              <span className={cn(meta, beleg.ueberfaellig && 'font-semibold !text-rot-text')}>
                <Icon name="euro" />
                {beleg.brutto !== null && beleg.brutto !== undefined ? formatEuro(beleg.brutto, beleg.waehrung) : '–'}
                {beleg.ueberfaellig && ' · überfällig'}
              </span>
            </div>
            <div className="px-3 pt-2.5 pb-3">
              <div className={cn(gruppeGrau, 'flex [&>*]:flex-1')}>
                <Link to="/belege/$belegId" params={{ belegId: beleg.id }} aria-label="Beleg öffnen" title="Öffnen">
                  <Icon name="edit_document" />
                </Link>
                <button type="button" aria-label="Vorschau" title="Vorschau" onClick={() => onVorschau(beleg)}>
                  <Icon name="visibility" />
                </button>
                <button
                  type="button"
                  aria-label="Original herunterladen"
                  title="Herunterladen"
                  onClick={() => void herunterladen(beleg.id)}
                >
                  <Icon name="download" />
                </button>
              </div>
            </div>
          </article>
        )
      })}
    </div>
  )
}
